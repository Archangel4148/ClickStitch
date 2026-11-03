using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClickStitch.Plugins;

/// <summary>The outcome of one plugin action. A failure is recorded and does not escape this result.</summary>
public sealed class PluginCallResult
{
    private PluginCallResult(bool succeeded, bool cancelled, string? detail, PluginFault? fault)
    {
        Succeeded = succeeded;
        Cancelled = cancelled;
        Detail = detail;
        Fault = fault;
    }

    public bool Succeeded { get; }

    public bool Cancelled { get; }

    public string? Detail { get; }

    public PluginFault? Fault { get; }

    internal static PluginCallResult Completed(string? detail) => new(true, false, detail, null);

    internal static PluginCallResult WasCancelled() => new(false, true, null, null);

    internal static PluginCallResult Failed(PluginFault fault) => new(false, false, null, fault);
}

/// <summary>
/// Loads discovered plugins into this process and shuts them down in reverse order.
/// <see cref="Assembly.LoadFrom(string)"/> does not isolate the plugin. A plugin can do what this process can do.
/// Exceptions from plugin code are recorded. They are not thrown to the caller.
/// </summary>
public sealed class PluginHost : IDisposable
{
    private readonly List<LoadedPlugin> _loaded = [];
    private readonly List<RegisteredAction> _actions = [];
    private readonly List<Subscription> _subscriptions = [];
    private readonly List<string> _log = [];
    private readonly List<PluginFault> _faults = [];
    private bool _shutDown;

    private PluginHost()
    {
    }

    /// <summary>
    /// Called after a plugin action fails or is cancelled, and after an event handler throws.
    /// The engine releases inputs it owns. Plugin code is not on that path.
    /// </summary>
    public Action? ReleaseOwnedInputs { get; set; }

    public IReadOnlyList<PluginActionDescriptor> Actions => _actions.Select(action => action.Descriptor).ToArray();

    public IReadOnlyList<string> Log => _log;

    public IReadOnlyList<PluginFault> Faults => _faults;

    public IReadOnlyList<string> LoadedIds => _loaded.Select(plugin => plugin.Id).ToArray();

    public static PluginHost Load(PluginCatalog catalog) => Load(catalog, PluginApi.Current);

    public static PluginHost Load(PluginCatalog catalog, PluginApiVersion host)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var result = new PluginHost();
        foreach (PluginFault fault in catalog.Faults)
        {
            result._faults.Add(fault);
        }

        foreach (DiscoveredPlugin plugin in catalog.Plugins)
        {
            if (!plugin.Manifest.Metadata.ApiVersion.IsSupportedBy(host))
            {
                result.Fault(plugin.Manifest.Metadata.Id, "Plugin API " + plugin.Manifest.Metadata.ApiVersion + " is not compatible with host API " + host + ".");
                continue;
            }

            result.TryLoad(plugin);
        }

        return result;
    }

    /// <summary>
    /// Loads only plugins that settings has enabled, and only after the trust warning was accepted.
    /// A disabled plugin stays on disk. A bad plugin is skipped.
    /// </summary>
    public static PluginHost LoadEnabled(PluginCatalog catalog, PluginSettings settings) =>
        LoadEnabled(catalog, settings, PluginApi.Current);

    public static PluginHost LoadEnabled(PluginCatalog catalog, PluginSettingsLoad loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        PluginHost host = LoadEnabled(catalog, loaded.Settings);
        if (loaded.Fault is not null)
        {
            host._faults.Add(loaded.Fault);
        }

        return host;
    }

    public static PluginHost LoadEnabled(PluginCatalog catalog, PluginSettings settings, PluginApiVersion host)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.ThirdPartyPluginsAcknowledged)
        {
            var refused = new PluginHost();
            foreach (PluginFault fault in catalog.Faults)
            {
                refused._faults.Add(fault);
            }

            if (settings.EnabledIds.Count > 0)
            {
                refused.Fault("plugins", "Plugins stay off until the trust warning is accepted.");
            }

            return refused;
        }

        var enabled = new HashSet<string>(settings.EnabledIds, StringComparer.Ordinal);
        var chosen = new List<DiscoveredPlugin>();
        foreach (DiscoveredPlugin plugin in catalog.Plugins)
        {
            if (enabled.Contains(plugin.Manifest.Metadata.Id))
            {
                chosen.Add(plugin);
            }
        }

        var filtered = new PluginCatalog(chosen, catalog.Faults);
        PluginHost result = Load(filtered, host);
        foreach (string id in settings.EnabledIds)
        {
            if (!catalog.Plugins.Any(plugin => plugin.Manifest.Metadata.Id == id))
            {
                result.Fault(id, "Plugin id " + id + " is enabled but was not found.");
            }
        }

        return result;
    }

    public PluginCallResult Execute(string actionId, string arguments, CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return Fail(Fault("plugins", "Action id is required."));
        }

        if (_shutDown)
        {
            return Fail(Fault("plugins", "The plugin host is shut down."));
        }

        if (!IsJsonObject(arguments))
        {
            return Fail(Fault("plugins", "Arguments must be a JSON object."));
        }

        RegisteredAction? action = _actions.FirstOrDefault(candidate => candidate.Descriptor.Id == actionId);
        if (action is null)
        {
            return Fail(Fault("plugins", "Action id " + actionId + " is not registered."));
        }

        if (cancellation.IsCancellationRequested)
        {
            Release();
            return PluginCallResult.WasCancelled();
        }

        try
        {
            var context = new PluginActionContext(cancellation, arguments);
            action.Handler(context);
            if (cancellation.IsCancellationRequested)
            {
                Release();
                return PluginCallResult.WasCancelled();
            }

            return PluginCallResult.Completed(context.Detail);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Release();
            return PluginCallResult.WasCancelled();
        }
        catch (Exception error)
        {
            PluginFault fault = Fault(action.PluginId, "Plugin action failed. " + Text(error));
            Release();
            return PluginCallResult.Failed(fault);
        }
    }

    /// <summary>Raises one event. One handler that throws is recorded, and the remaining handlers still run.</summary>
    public void Publish(PluginEventKind kind)
    {
        if (_shutDown)
        {
            return;
        }

        Subscription[] handlers = _subscriptions.Where(subscription => subscription.Kind == kind).ToArray();
        foreach (Subscription subscription in handlers)
        {
            if (_shutDown)
            {
                return;
            }

            try
            {
                subscription.Handler(kind);
            }
            catch (Exception error)
            {
                Fault(subscription.PluginId, "Plugin event failed. " + Text(error));
                Release();
            }
        }
    }

    public void Shutdown()
    {
        if (_shutDown)
        {
            return;
        }

        _shutDown = true;
        _subscriptions.Clear();
        for (int index = _loaded.Count - 1; index >= 0; index--)
        {
            LoadedPlugin plugin = _loaded[index];
            try
            {
                plugin.Instance.Shutdown();
            }
            catch (Exception error)
            {
                Fault(plugin.Id, "Plugin shutdown failed. " + Text(error));
            }
        }

        _loaded.Clear();
    }

    public void Dispose() => Shutdown();

    private void TryLoad(DiscoveredPlugin discovered)
    {
        string id = discovered.Manifest.Metadata.Id;
        string assemblyPath = Path.GetFullPath(Path.Combine(discovered.Directory, discovered.Manifest.EntryAssembly));
        if (!PluginDiscovery.IsInside(discovered.Directory, assemblyPath))
        {
            Fault(id, "The entry assembly escapes the plugin directory.");
            return;
        }

        if (!File.Exists(assemblyPath))
        {
            Fault(id, "The entry assembly was not found.");
            return;
        }

        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(assemblyPath);
        }
        catch (Exception error)
        {
            Fault(id, "The entry assembly could not be loaded. " + Text(error));
            return;
        }

        Type? type = assembly.GetType(discovered.Manifest.EntryType, throwOnError: false, ignoreCase: false);
        if (type is null || type.IsAbstract || !typeof(IMacroPlugin).IsAssignableFrom(type))
        {
            Fault(id, type is null ? "The entry type was not found." : "The entry type does not implement IMacroPlugin.");
            return;
        }

        IMacroPlugin instance;
        try
        {
            if (Activator.CreateInstance(type) is not IMacroPlugin created)
            {
                Fault(id, "The entry type does not implement IMacroPlugin.");
                return;
            }

            instance = created;
            if (instance.Metadata is null || instance.Metadata.Id != id)
            {
                Fault(id, "The plugin id does not match the manifest.");
                return;
            }
        }
        catch (Exception error)
        {
            Fault(id, "The plugin could not be constructed. " + Text(error));
            return;
        }

        var gate = new RegistrationGate(this, id);
        int actionsBefore = _actions.Count;
        int subscriptionsBefore = _subscriptions.Count;
        try
        {
            gate.Open = true;
            instance.Initialize(gate);
        }
        catch (Exception error)
        {
            if (_actions.Count > actionsBefore)
            {
                _actions.RemoveRange(actionsBefore, _actions.Count - actionsBefore);
            }

            if (_subscriptions.Count > subscriptionsBefore)
            {
                _subscriptions.RemoveRange(subscriptionsBefore, _subscriptions.Count - subscriptionsBefore);
            }

            Fault(id, "Plugin initialization failed. " + Text(error));
            return;
        }
        finally
        {
            gate.Open = false;
        }

        _loaded.Add(new LoadedPlugin(id, instance, gate));
    }

    private void Release()
    {
        Action? release = ReleaseOwnedInputs;
        if (release is null)
        {
            return;
        }

        try
        {
            release();
        }
        catch (Exception error)
        {
            Fault("plugins", "Owned inputs could not be released. " + Text(error));
        }
    }

    private PluginFault Fault(string source, string message)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            source = "plugins";
        }

        if (source.Length > 200)
        {
            source = source[..200];
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "The plugin failed.";
        }

        if (message.Length > 1000)
        {
            message = message[..1000];
        }

        var fault = new PluginFault(source, message);
        _faults.Add(fault);
        return fault;
    }

    private static PluginCallResult Fail(PluginFault fault) => PluginCallResult.Failed(fault);

    private static string Text(Exception error) =>
        string.IsNullOrWhiteSpace(error.Message) ? "The plugin failed." : error.Message;

    private static bool IsJsonObject(string? arguments)
    {
        if (arguments is null || arguments.Length is < 2 or > 16 * 1024)
        {
            return false;
        }

        try
        {
            return JsonNode.Parse(arguments, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
                MaxDepth = 16,
            }) is JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private readonly record struct LoadedPlugin(string Id, IMacroPlugin Instance, RegistrationGate Gate);

    private sealed class RegisteredAction
    {
        public RegisteredAction(string pluginId, PluginActionDescriptor descriptor, PluginActionHandler handler)
        {
            PluginId = pluginId;
            Descriptor = descriptor;
            Handler = handler;
        }

        public string PluginId { get; }

        public PluginActionDescriptor Descriptor { get; }

        public PluginActionHandler Handler { get; }
    }

    private sealed class Subscription
    {
        public Subscription(string pluginId, PluginEventKind kind, PluginEventHandler handler)
        {
            PluginId = pluginId;
            Kind = kind;
            Handler = handler;
        }

        public string PluginId { get; }

        public PluginEventKind Kind { get; }

        public PluginEventHandler Handler { get; }
    }

    private sealed class RegistrationGate : ICoreHost
    {
        private readonly PluginHost _host;
        private readonly string _id;

        public RegistrationGate(PluginHost host, string id)
        {
            _host = host;
            _id = id;
        }

        public bool Open { get; set; }

        public void Log(string message)
        {
            EnsureOpen("The plugin host is only available during Initialize.");
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            _host._log.Add(_id + ": " + message);
        }

        public void Report(PluginFault fault)
        {
            EnsureOpen("The plugin host is only available during Initialize.");
            ArgumentNullException.ThrowIfNull(fault);
            _host._faults.Add(fault);
        }

        public void RegisterAction(PluginActionDescriptor action, PluginActionHandler handler)
        {
            EnsureOpen("Actions can only be registered during Initialize.");
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(handler);
            if (_host._actions.Any(existing => existing.Descriptor.Id == action.Id))
            {
                throw new InvalidOperationException("Action id " + action.Id + " is already registered.");
            }

            _host._actions.Add(new RegisteredAction(_id, action, handler));
        }

        public void Subscribe(PluginEventKind kind, PluginEventHandler handler)
        {
            EnsureOpen("Events can only be subscribed during Initialize.");
            ArgumentNullException.ThrowIfNull(handler);
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            _host._subscriptions.Add(new Subscription(_id, kind, handler));
        }

        public void Unsubscribe(PluginEventKind kind, PluginEventHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (_host._shutDown)
            {
                return;
            }

            _host._subscriptions.RemoveAll(subscription =>
                subscription.PluginId == _id && subscription.Kind == kind && subscription.Handler == handler);
        }

        private void EnsureOpen(string message)
        {
            if (!Open || _host._shutDown)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
