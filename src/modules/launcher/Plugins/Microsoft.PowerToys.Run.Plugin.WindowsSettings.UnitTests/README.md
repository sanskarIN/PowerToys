# PowerToys Run – Windows Settings unit tests

This project covers the PowerToys Run Windows Settings plugin, which searches the
embedded `WindowsSettings.json` catalogue and converts matching entries into
launcher results.

## Coverage

| Suite | Primary responsibility |
| --- | --- |
| `WindowsSettingsCatalogTests` | Embedded resource loading, catalogue integrity, and repeated/concurrent reads |
| `WindowsSettingsPathTests` | Display-path generation, ordering, non-ASCII areas, metadata preservation |
| `SettingsPathQueryTests` | `>` hierarchy matching, prefixes, bounds, and case comparison |
| `WindowsSettingsMainQueryTests` | Name, area, alias, path, action-keyword queries, and result ordering |
| `WindowsSettingsResultTests` | Scores, result identity, notes, tooltips, and duplicate titles |
| `WindowsSettingsBuildFilterTests` | Introduction/deprecation boundaries independent of the host registry |

## Running the tests

1. On Windows, install the PowerToys development prerequisites documented under
   `doc/devdocs/development`.
2. Open the root `PowerToys.slnx` in Visual Studio.
3. Select `x64` and `Debug`; restore and build
   `Microsoft.PowerToys.Run.Plugin.WindowsSettings.UnitTests`.
4. Run the unit-test project through Test Explorer and inspect the results.
5. Run the relevant PowerToys CI build/test pipelines before requesting merge.

The tests do **not** invoke any Windows Settings command: launch actions are only
inspected, never executed. The build-filter tests supply an explicit build number,
so they do not read registry values. The other suites use synthetic settings
wherever possible. The catalogue suite intentionally reads the real embedded
resource and checks repeated and concurrent deserialization.

## Regression fixed in the production helper

`JsonSettingsListHelper` previously added a converter to its shared
`JsonSerializerOptions` instance on every call. Serializer options become
read-only after their first use, so a later catalogue read could fail and return
an empty settings collection. The converter is now registered once when the
static options are constructed. The catalogue tests exercise repeated loads,
concurrent loads, and independence between returned instances.

## Scope and limitations

- This is a unit-test contribution; it does not alter result actions or Windows
  Settings commands.
- Windows-specific builds and CI results must be verified by a contributor
  before claiming tests pass.
- The Windows Settings integration can change as new Windows versions are
  released; test fixtures avoid relying on an exact catalogue item count.
