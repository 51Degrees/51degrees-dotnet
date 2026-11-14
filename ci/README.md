# CI

The scripts here are what the shared 51Degrees workflows call, in the order
the [common-ci](https://github.com/51Degrees/common-ci) README describes.
[`packages.json`](packages.json) lists the NuGet packages, with their
folders, the packages each depends on and the property holding each one's
version. [`families.json`](families.json) lists the folders that came from
separate repositories, with the solutions each builds. `packages.ps1` reads
both and holds the rest of what is particular to this repository, which is
the native engines and the examples.

## Versions

Each NuGet package has its own version, continuing from the version it last
published. A package is published when a file that counts under its folders
changed since its last tag, and so is every package that depends on it, so
that it requires the new version. Documentation, tests and examples do not count,
which common-ci's plan decides, and nor does a project file or nuspec whose
only changes are to package metadata such as the description or links, which
[`release-plan-filter.ps1`](release-plan-filter.ps1) decides. NuGet takes the lowest version that meets a requirement, so a fix
reaches the users of a dependent only when the dependent is published too.
Each is published at its next patch version and tagged `<package>/<version>`,
for example `FiftyOne.Pipeline.Core/4.5.131`. Until a package has a tag of its
own, it continues from its family's last tag, such as `pipeline/4.5.130`,
which every package of the family was last published at. The publish tags it
at that commit in its own checkout only, and pushes only the tags of the
packages it releases.
common-ci's
[`steps/release-plan.ps1`](https://github.com/51Degrees/common-ci/blob/main/steps/release-plan.ps1)
works out the plan from `packages.json`, and the publish prints it. The run's
single version, which the shared workflow asks for, is that of
`FiftyOne.Pipeline.Core`. After a publish, a pull request records the
published versions in the root `Directory.Build.props`, one property for each
package, which is what Package mode restores against.

The scripts only ever move the patch number. A major or minor version moves
only by a tag pushed by hand, such as `FiftyOne.Pipeline.Core/4.6.0`, after
which the patch numbers follow it.

## What runs where

| Script | Pull request | Publish |
| --- | --- | --- |
| `fetch-assets.ps1` | Data files into the `cxx` data submodules | Same |
| `setup-environment.ps1` | Keys, paths and the strong name key | Same |
| `build-project.ps1` | Every family and the examples, Project mode, and on the x64 msbuild job the pages of the .NET Framework sites | |
| `run-unit-tests.ps1` | The library tests | |
| `get-next-package-version.ps1` | | Prints the plan, each package's last and next version and why it is or is not released |
| `release-plan-filter.ps1` | | Called by the plan, leaves out a project file or nuspec whose only changes are to package metadata |
| `package-update-required.ps1` | | Whether the plan releases any package |
| `check-version-bump.ps1` | | Refuses a package whose major or minor number moves |
| `build-package-requirements.ps1` | | The native libraries, one job per platform |
| `build-package.ps1` | | Packs the packages the plan releases, signs once |
| `install-package.ps1` | | Rebuilds the examples against the packages |
| `run-integration-tests.ps1` | The example tests and the Selenium contract | Same, against the packages |
| `run-performance-tests.ps1` | The console examples and the web overhead | Same |
| `publish-package.ps1` | | Pushes, tags each package in one push, opens the versions pull request |

`select-options.ps1` chooses the entries a pull request runs. The standard set
is `Ubuntu_x64_Release`, with the Selenium contract, and `Windows_x64_Release`,
the only build of the .NET Framework projects. Both leave out the IP
Intelligence tests that load the Enterprise data file and the web example
tests, but still run each IP Intelligence example with no settings on the free
data files (the `WithoutSettings` category) and that assembly's `Unit` tests,
which need no data file. `run-integration-tests.Tests.ps1` checks this with
Pester 5 or later. A native
change adds Linux ARM and Windows x86, an IP Intelligence change
runs the Enterprise tests, a web change runs the web example tests on Linux, and
a change outside the package folders adds every entry but macOS. macOS and
performance run only when `rc` itself or the release pull request is built,
which `.github/workflows/release-pull-request.yml` does on every entry.
`select-options.Tests.ps1` tests the selection, run with Pester 5 or later.

The secrets are the ones the separate repositories used: `ACCESS_TOKEN`,
`NUGET_API_KEY`, the `CODE_SIGNING_KEY_VAULT_*` set, `STRONG_NAME_KEY_BASE64`,
`DEVICE_DETECTION_KEY`, `DEVICE_DETECTION_URL`, `IPI_DATA_FILE_URL`,
`_51DEGREES_RESOURCE_KEY_BESPOKE` and the `ACCEPTCH_*_KEY` set.
