using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClickStitch.Plugins;

/// <summary>
/// A trusted in-process plugin. This is not a sandbox. The type runs with the application's permissions.
/// </summary>
public interface IMacroPlugin
{
    PluginMetadata Metadata { get; }

    void Initialize(ICoreHost host);

    void Shutdown();
}

/// <summary>
/// What a plugin may use while <see cref="IMacroPlugin.Initialize"/> is running.
/// Calls after <c>Initialize</c> returns are rejected.
/// </summary>
public interface ICoreHost
{
    void Log(string message);

    void Report(PluginFault fault);

    void RegisterAction(PluginActionDescriptor action, PluginActionHandler handler);

    void Subscribe(PluginEventKind kind, PluginEventHandler handler);

    void Unsubscribe(PluginEventKind kind, PluginEventHandler handler);
}

/// <summary>Moments a plugin may observe. Emergency stop is not one of them.</summary>
public enum PluginEventKind
{
    MacroStarted,
    MacroFinished,
}

public delegate void PluginEventHandler(PluginEventKind kind);

public delegate void PluginActionHandler(PluginActionContext context);

/// <summary>The arguments for one plugin action. The handler must not keep this object after it returns.</summary>
public sealed class PluginActionContext
{
    private string? _detail;

    internal PluginActionContext(CancellationToken cancellation, string arguments)
    {
        Cancellation = cancellation;
        Arguments = arguments;
    }

    public CancellationToken Cancellation { get; }

    public string Arguments { get; }

    /// <summary>A short note returned to the caller. A later call replaces the earlier note.</summary>
    public void Report(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (message.Length > 400 || !string.Equals(message, message.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("The action note must be 1–400 characters and must not start or end with whitespace.", nameof(message));
        }

        _detail = message;
    }

    internal string? Detail => _detail;
}

/// <summary>A failure recorded against a plugin id or a plugin folder name.</summary>
public sealed class PluginFault
{
    public PluginFault(string source, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (source.Length > 200)
        {
            throw new ArgumentException("Source must be 1–200 characters.", nameof(source));
        }

        if (message.Length > 1000)
        {
            throw new ArgumentException("Message must be 1–1,000 characters.", nameof(message));
        }

        Source = source;
        Message = message;
    }

    public string Source { get; }

    public string Message { get; }
}

/// <summary>
/// One action a plugin adds. The editor reads this later. The descriptor is data, and it is not executed here.
/// </summary>
public sealed class PluginActionDescriptor
{
    private const int MaxSchemaCharacters = 16 * 1024;

    public PluginActionDescriptor(string id, string name, string description, string parameterSchema)
    {
        if (!IsActionId(id))
        {
            throw new ArgumentException("Action id must be 1–64 characters, start with a letter, and use letters, digits, dots, underscores, or hyphens.", nameof(id));
        }

        if (!PluginManifests.IsName(name))
        {
            throw new ArgumentException("Name must be 1–120 characters and must not start or end with whitespace.", nameof(name));
        }

        if (description is null || description.Length > 400 || !string.Equals(description, description.Trim(), StringComparison.Ordinal) || description.Any(char.IsControl))
        {
            throw new ArgumentException("Description must be 0–400 characters and must not start or end with whitespace.", nameof(description));
        }

        if (!IsSchema(parameterSchema))
        {
            throw new ArgumentException("Parameter schema must be a JSON object of at most 16 KiB.", nameof(parameterSchema));
        }

        Id = id;
        Name = name;
        Description = description;
        ParameterSchema = parameterSchema;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public string ParameterSchema { get; }

    private static bool IsActionId(string id)
    {
        if (id is null || id.Length is < 1 or > 64 || !char.IsAsciiLetter(id[0]))
        {
            return false;
        }

        foreach (char character in id)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSchema(string schema)
    {
        if (schema is null || schema.Length is < 2 or > MaxSchemaCharacters)
        {
            return false;
        }

        try
        {
            return JsonNode.Parse(schema, documentOptions: new JsonDocumentOptions
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
}
