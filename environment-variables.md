# Environment variable names

These are the environment variables that the packages, examples, tests and CI
scripts of this repository read. Each name 51Degrees defines starts with
`_51DEGREES_` and holds one thing, and no name is filled from another.
[`common/Test/EnvironmentVariableNamesTests.cs`](common/Test/EnvironmentVariableNamesTests.cs)
reads the tables below. It fails on a `_51DEGREES_` name in the code that is
not listed here, on a listed name that nothing uses, and on any other name
that is read or set and is not in the last table.

## Keys

| Name | What it holds | Read by |
| --- | --- | --- |
| `_51DEGREES_RESOURCE_KEY` | The resource key a person running a cloud example sets. No CI job sets it. A test that runs a cloud example gives the example its key under this name. | The cloud examples |
| `_51DEGREES_RESOURCE_KEY_BESPOKE` | A resource key that carries every property | The live cloud tests, and the tests that run the cloud examples |
| `_51DEGREES_RESOURCE_KEY_BASIC` | A resource key on the basic data tier | The 51Did entitlement test |
| `_51DEGREES_RESOURCE_KEY_51DID` | A resource key carrying 51Did | The live 51Did tests |
| `_51DEGREES_LICENSE_KEY` | A 51Degrees license key. CI downloads the paid data files with it. | The data file update examples, and the web examples that read a result with a license key |
| `_51DEGREES_LICENSE_KEY_51DID` | The license key that reads results sealed with `_51DEGREES_RESOURCE_KEY_51DID` | The live 51Did tests |
| `_51DEGREES_LICENSE_KEY_BESPOKE` | A license key that carries every property | The Accept-CH tests of the cloud web examples |

## Settings

| Name | What it holds | Default when it is not set |
| --- | --- | --- |
| `_51DEGREES_CLOUD_ENDPOINT` | The address of the cloud service to call, ending `/api/v4/` | The public cloud service |
| `_51DEGREES_DD_PATH` | The path of a Device Detection data file | The free file in `cxx/data/device-detection` |
| `_51DEGREES_IPI_PATH` | The path of an IP Intelligence data file | The free file in `cxx/data/ip-intelligence` |
| `_51DEGREES_EVIDENCE_YAML` | The path of a YAML evidence file for the on-premise example tests | `20000 Evidence Records.yml` in `cxx/data/device-detection` |
| `_51DEGREES_EXAMPLE_HTTP_PORTS` | The HTTP port, or ports, a web example listens on | 5151 |
| `_51DEGREES_EXAMPLE_HTTPS_PORTS` | The HTTPS ports a web example listens on | 5001 and 5002 |
| `_51DEGREES_RUN_NETWORK_TESTS` | Set to 1 to let the agent signature tests fetch live key directories | The tests that need the network are skipped |
| `_51DEGREES_TEST_FILTER` | A pattern of the test assemblies the CI scripts run | Every test assembly |
| `_51DEGREES_ALLOW_VERSION_BUMP` | `true` to let a publish move a package's major or minor number, read by common-ci's version check | The move is refused |

## Build options

MSBuild reads these as properties, given as `-p:<name>=<value>`, in a
`Directory.Build.local.props` file beside the root `Directory.Build.props`, or
as environment variables. Each package's version is a build option too, named
`_51DEGREES_<package>_VERSION` in the root `Directory.Build.props` and listed
in `ci/packages.json`.

| Name | What it holds | Default when it is not set |
| --- | --- | --- |
| `_51DEGREES_REFERENCES` | `Project` to build against the projects of the other folders, `Package` to build against the published packages | `Package` |
| `_51DEGREES_BUILT_ON_CI` | `true` when CI builds, so that an on-premise engine package packs the native library of every platform | `false` |
## Secrets only CI reads

| Name | What it holds | Read by |
| --- | --- | --- |
| `_51DEGREES_ACCESS_TOKEN` | The GitHub token the publish pushes tags and makes releases with | The Package job in `.github/workflows/publish.yml` |
| `_51DEGREES_PUBLISH_NUGET_API_KEY` | The nuget.org key, from nuget.org trusted publishing | `ci/publish-package.ps1` |
| `_51DEGREES_SIGN_STRONG_NAME_KEY_BASE64` | The strong name key, base64 | `ci/build-package.ps1` |
| `_51DEGREES_SIGN_KEY_VAULT_URL` | The address of the code signing key vault | `ci/build-package.ps1` |
| `_51DEGREES_SIGN_KEY_VAULT_CLIENT_ID` | The client id the package build signs in to the key vault with | `ci/build-package.ps1` |
| `_51DEGREES_SIGN_KEY_VAULT_TENANT_ID` | The tenant of that client | `ci/build-package.ps1` |
| `_51DEGREES_SIGN_KEY_VAULT_CLIENT_SECRET` | The secret of that client | `ci/build-package.ps1` |
| `_51DEGREES_SIGN_KEY_VAULT_CERTIFICATE_NAME` | The name of the code signing certificate in the key vault | `ci/build-package.ps1` |

## Names other tools define

These are read or set under the name the tool gives them.

| Name | Defined by |
| --- | --- |
| `ASPNETCORE_URLS` | ASP.NET Core |
| `CHROME_BIN` | Chrome and the tools that start it |
| `CHROMEWEBDRIVER` | The GitHub runner images |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | The .NET SDK |
| `DOTNET_NOLOGO` | The .NET SDK |
| `EXAMPLE_LANG` | The browser test suite, selenium-api-tests |
| `EXAMPLE_URL` | The browser test suite, selenium-api-tests |
| `GITHUB_ACTIONS` | GitHub Actions |
| `GITHUB_JOB` | GitHub Actions |
| `PATH` | The operating system |
| `ProgramFiles` | Windows |
| `PSModulePath` | PowerShell |
| `SE_BROWSER_VERSION` | Selenium |
| `SE_SKIP_DRIVER_IN_PATH` | Selenium |
| `windir` | Windows |
