# Hatband SDK

External plugins are loaded from one directory per plugin under `.hatband/plugins`.
Each directory contains `hatband-plugin.json`, the entry assembly, and its private
dependencies (including the entry assembly's `.deps.json`).

Example manifest:

```json
{
  "id": "example.store",
  "name": "Example Store",
  "version": "1.0.0",
  "apiVersion": 1,
  "entryAssembly": "Example.HatbandPlugin.dll",
  "entryType": "ExampleStore.ExamplePlugin"
}
```

The entry type must be public, derive from `HatbandPlugin`, and have a public
parameterless constructor. `ConfigureServices` registers plugin-specific services.
The host discovers game, metadata, artwork, installation, management,
post-install, authentication, and compatibility capabilities from the plugin
assembly and registers them as singletons. Capability providers should not also
be registered explicitly by `ConfigureServices`.

Built-in plugins may share an assembly and list their provider types in
`CapabilityImplementations`. External plugin folders are isolated packages, so
the host discovers supported provider types throughout the entry assembly by default.

Plugins reference `Hatband.SDK` and implement the existing strongly typed Hatband
capability interfaces. Their IDs are lowercase strings and must be unique across
built-in and external plugins. The manifest API version currently supported is
`HatbandSdk.ApiVersion` (`1`).

Use `IPluginDataStore` for versioned per-game plugin data. It stores JSON in a
separate table keyed by game ID, plugin ID, and plugin-owned key; deleting a game
also removes its plugin data. Keep plugin preferences in an `ISettingsSection`.

Plugins execute in-process with the application user's permissions. The separate
assembly load context resolves private managed and native dependencies; it is not
a security sandbox.
