# Plugin SDK — Initial Design

**Status:** Design for review. No plugin project, host, or sample is included in Phase 0.

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

A broken manifest is skipped. The app lists the error and still opens.

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

`ICoreHost` in that first milestone can log, report a structured error, and register an action descriptor. Registration after `Initialize` returns throws. A second registration with the same id is rejected.

Action descriptors carry a stable id, a display name, a description, and a parameter schema. The editor shows plugin actions from those descriptors. Plugin blocks are not hard-coded into the editor.

The host passes a cancellation token into action execution. A plugin that throws is reported, the action fails, and the engine releases inputs it still owns. A plugin that never returns can delay shutdown. That is a documented limit of in-process code. Emergency stop still releases inputs the engine itself submitted, and it does not call the plugin to do so.

## Sample

Phase 4 includes a separate sample plugin that registers one action and is developed against this document. The sample is not part of the application project. Phase 0 contains no sample.

## What the core does without plugins

Startup, the macro document, recording, playback, emergency stop, and the built-in actions work with no plugin installed. A plugin cannot replace emergency stop.
