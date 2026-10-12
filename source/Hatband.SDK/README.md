# Hatband SDK

Hatband loads built-in packages from `plugins` next to its executable and external
packages from `.hatband/plugins`. Each plugin has its own directory containing
`hatband-plugin.json`, the entry assembly, its `.deps.json`, resources and private
dependencies. Restart the application after adding a package.

```json
{
  "id": "example.connector",
  "name": "Example connector",
  "version": "1.0.0",
  "apiVersion": 3,
  "entryAssembly": "ConnectorPlugin.dll",
  "entryType": "ConnectorPlugin.ExamplePlugin"
}
```

## Dependencies and registration

A public parameterless entry type derives from `HatbandPlugin`. Its
`ConfigureServices` registers private services and HTTP clients. Scrutor discovers
supported capabilities in the entry assembly and registers each implementation as
one singleton shared by its capability interfaces. Do not register those same
capabilities a second time. Use `CapabilityImplementations` to select an explicit
set when the assembly contains alternative implementations.

Plugins reference `Hatband.SDK` and Core contracts. UI plugins additionally
reference `Hatband.SDK.UI`, using the same Avalonia version as the host. Do not
reference `Hatband.App`, `Hatband.Infrastructure`, or another store plugin.
Built-in projects are included in the application's packaging references; the
shared build target collects the entire plugin output for build and publish.

IDs are stable lowercase strings and unique across built-in and external plugins.
API version 3 separates connector UI identity from game store identity and lets a
connector request synchronization for an explicit source ID. Earlier plugins must
be rebuilt. The loader shares Core, SDK, SDK.UI, Avalonia and host framework
contracts, while resolving private managed and native dependencies from the
package. Plugins run in the application process; this is not a security sandbox.

## Connector UI and authentication

`IConnectorUiProvider` contributes a panel with its own stable `Id` and display name.
That ID is independent of a game store source. The app owns the Connectors section
and tabs; the plugin owns its views, ViewModels and authentication flow.
`ConnectorUiContext.SyncAsync(sourceId, cancellationToken)` starts host-owned library synchronization
for the source explicitly requested by the connector. Panels
are cached, activated when shown and deactivated when left. Deactivation should
cancel interactive login, not an already started background sync.

`PluginPanel` exposes lifecycle, initial focus and back handling. The host handles
normal directional focus and keyboard/gamepad navigation. Keep focus in existing
controls when updating panel state. The SDK UI includes QR rendering, credentials
fields and browser authorization/code-entry controls; token exchange and protocol
validation remain in the plugin.

`IConnectorSessionProvider` exposes account and connection state. Store private,
versioned credential payloads using the encrypted `IConnectorSessionStore` generic
methods. The plugin validates its own payload and owns refresh/reauthentication.
Do not persist passwords entered in the shared form or put tokens in game data.

Declare preferences with `ISettingsSection`. Set `ShowInSettingsNavigation` to
false when the plugin edits those preferences inside its own panel; persistence
remains available through `ISettingsApi`.

## Compatibility tool provisioning

Compatibility tool discovery and installation are plugin capabilities in API
version 2. Implement `ICompatibilityToolDiscoveryProvider` to report an `Id`,
the host operating systems it supports, and the installed tools it discovers.
The host calls only providers that support the current host and combines their
results. Implement `ICompatibilityToolInstallationProvider` to install a release;
its `ReleaseProviderIds` list declares which release catalog IDs it handles. The
host dispatches each install request by `CompatibilityToolRelease.ProviderId`.
Discovery IDs and installation provider IDs must each be unique within their
capability, and each release catalog ID must map to one installer. Failures and
cancellation from a provider reach the caller.

`ICompatibilityToolLocationProvider` contributes search or install locations and
identifies the provisioning implementation it belongs to with `ProvisionerId`.
Keep operating system checks, tool layouts and installation details in the plugin
that owns provisioning. The host continues to expose generic discovery,
installation and release catalog services to the application.

## Launch and platform selection

A management provider prepares a `GameLaunchPlan` using `GameLaunchContext`.
Delegated plans retain store-client launching. Executable plans contain the actual
game command, the installed executable's target OS and an optional runtime ID.
A helper CLI that prepares launch parameters runs on the host; it is not the game
executable and should not be wrapped in Proton.

The runtime registry resolves the host/target pair and optional explicit runtime.
Multiple matching runtimes require an explicit selection. Runtimes declare whether
they need a compatibility tool and support post-install plans. Native execution
supports the current host; UMU supports Windows executables on Linux. Missing tools
and unsupported post-install environments fail explicitly. Steam Original keeps
launching through Steam; Custom prepares a command for UMU on Linux.

Keep source-specific launch parameters in the plugin and prefix/process/preparation
work in the runtime. Source metadata platforms describe availability, not
necessarily the platform of the installed build.

## Data and examples

`IPluginDataStore` stores versioned JSON per game, plugin ID and plugin-owned key.
Deleting a game also deletes its plugin data. Credentials use the encrypted session
store; preferences use settings sections.

See [the connector example](../../samples/ConnectorPlugin/README.md) for an external
package that contributes a capability and its own panel without app dependencies.
It is not distributed with the application.
