# Plugin SDK — Initial Design

**Status:** The SDK, host, and sample plugin are implemented. The shell loads a plugin only after the trust warning is accepted and that plugin is enabled. Block descriptors are not registered.

## Trust

A ClickStitch plugin is trusted in-process .NET code. It runs with the same permissions as the application and the user. It is not a sandbox.

`AssemblyLoadContext` does not create a security boundary. A collectible load context, if a later phase uses one, is an attempt to unload a plugin. Native handles and threads can keep that unload from succeeding. Unload is best-effort.

The product tells the user, before a third-party plugin is enabled, that the plugin can do what the user can do. The same warning is part of first-run. ClickStitch does not imply that a plugin is scanned, isolated, or safe because it loaded.

Every plugin follows the product boundary: automate work the user is already allowed to do. Plugins are not a place for stealth, anti-cheat bypass, or defeating access controls.

## Where plugins live

Plugins are files in a `Plugins` directory beside the executable. Each plugin has its own subdirectory. The application starts normally when `Plugins` is missing or empty. Plugins are not downloaded. Enabling a plugin is a local decision stored in settings.

## Manifest

File name: `clickstitch-plugin.json`.

```json
{
  "id": "com.example.sample",
  "name": "Sample",
  "version": "1.0.0",
  "apiVersion": "1.0",
  "entryAssembly": "Sample.dll",
  "entryType": "Sample.Plugin"
}
```

| Field | Rule |
|---|---|
| `id` | Required. Reverse-DNS, 1–100 characters, unique among loaded plugins. |
| `name` | Required display name, 1–120 characters. |
| `version` | Required `major.minor.patch`. |
| `apiVersion` | Required `major.minor`. This is the plugin API, not the macro format version. |
| `entryAssembly` | Required file name inside the plugin directory. A path that leaves the directory is invalid. |
| `entryType` | Required type name in that assembly. |

A broken manifest is invalid. `PluginManifests.Read` returns the issues and no manifest. The reader does not open the entry assembly. A document larger than 64 KiB is rejected. Unknown members are errors. `version` is `major.minor.patch` with no leading zeros. `apiVersion` is `major.minor`. An id is two or more reverse-DNS labels, 1–100 characters. A name is 1–120 characters with no surrounding whitespace. `entryAssembly` is a `.dll` file name. A path, including `..`, is invalid. `entryType` is a dotted type name. Duplicate ids are reported by `PluginManifests.DuplicateIds`.

`PluginDiscovery.Discover` reads each subdirectory of a `Plugins` folder. A missing or empty folder returns an empty catalog. A broken manifest, a missing manifest, an incompatible API version, or a duplicate id is recorded and that folder is skipped. The first copy of an id is kept. Discovery does not load an assembly.

`PluginSettingsStore` reads `%LOCALAPPDATA%\ClickStitch\settings.json`. A missing or unreadable file leaves every plugin off and does not create the file. `thirdPartyPluginsAcknowledged` must be true before `Enable` will accept an id. `PluginHost.LoadEnabled` loads only those ids. A disabled plugin stays on disk. The shell uses that path at startup. A plugin that fails to load is recorded, and the window still opens. The first 20 faults are written into the tryout log.

## API version

The host has one API version. Phase 4 starts at `1.0`.

- The major number must match.
- The plugin's minor number must be less than or equal to the host's minor number.
- A plugin built for a newer minor, or any other major, is not loaded.

Macro `formatVersion` and plugin `apiVersion` change independently.

## Lifecycle

1. Discover subdirectories of `Plugins`.
2. Read and validate each manifest.
3. Check the API version.
4. Load the entry assembly from that directory.
5. Construct the entry type. It implements `IMacroPlugin`.
6. Call `Initialize` with the host.
7. Accept registrations only during `Initialize`.
8. On shutdown, call `Shutdown` in reverse initialization order, then release owned inputs if a plugin action left any. The engine owns that release. Plugin code is not on the release path.
9. A disabled plugin is not loaded on the next start. Its files stay on disk.

Initialization failure, a duplicate id, or a missing assembly skips that plugin, records the error, and leaves the rest of the app running.

## Registration

Phase 4 starts with one contract. Further contracts are added when a real extension needs them, rather than as a catalog of unused interfaces.

```csharp
public interface IMacroPlugin
{
    PluginMetadata Metadata { get; }
    void Initialize(ICoreHost host);
    void Shutdown();
}
```

`ICoreHost` can log, report a `PluginFault`, register an action, and subscribe to `MacroStarted` or `MacroFinished`. Registration and subscription after `Initialize` returns throw. Unsubscribe may be called later and does nothing if the handler is already gone. A second action with the same id is rejected, that plugin is skipped, and the action it tried to add is not kept. An action id is 1–64 characters, starts with a letter, and uses letters, digits, dots, underscores, or hyphens. The parameter schema is a JSON object of at most 16 KiB. It is not executed. Block descriptors are not registered. Emergency stop is not an event, and a plugin cannot replace it.

```csharp
host.RegisterAction(descriptor, context =>
{
    context.Cancellation.ThrowIfCancellationRequested();
    context.Report("Sample action ran.");
});
```

`PluginHost.Execute` passes a cancellation token and a JSON object of arguments. A throw is returned as a failed `PluginCallResult` and recorded as a fault. The host then calls `ReleaseOwnedInputs` when that callback is set. The tryout wires it to the engine, which releases inputs the engine owns. One event handler that throws is recorded the same way, and the later handlers still run. A plugin that never returns can delay the caller. That is a limit of in-process code. Shutdown calls `Shutdown` in reverse order. One plugin that throws during shutdown is recorded, and the rest still run. A plugin that failed `Initialize` is not shut down.

## Sample

`samples/ClickStitch.SamplePlugin` references only the SDK. It registers `sample.ping`, which reports `Sample action ran.` It does not send input. The application project does not reference it. Copy `clickstitch-plugin.json` and the built DLL into a subdirectory of `Plugins`, accept the trust warning, and enable `com.clickstitch.sample`.

## What the core does without plugins

Startup, the macro document, recording, playback, emergency stop, and the built-in actions work with no plugin installed. A plugin cannot replace emergency stop.
