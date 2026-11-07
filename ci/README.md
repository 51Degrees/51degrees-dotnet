# CI

The scripts here are what the shared 51Degrees workflows call, in the order
the [common-ci](https://github.com/51Degrees/common-ci) README describes.
[`packages.json`](packages.json) lists the package families, with their
folders, the families each depends on and their solutions. `packages.ps1`
reads it and holds the rest of what is particular to this repository, which
is the native engines, the nuspec package and the version of each family.

## Versions

Each family keeps the version stream its own repository had. A family is
published when something under its folders changed since its last tag, and
so is every family that depends on it, so that their packages require the
new version. NuGet takes the lowest version that meets a requirement, so a fix
reaches the users of a dependent only when the dependent is published too.
Each is published at its next patch version and tagged `<family>/<version>`.
common-ci's
[`steps/release-plan.ps1`](https://github.com/51Degrees/common-ci/blob/main/steps/release-plan.ps1)
works out the plan from `packages.json`, and the publish prints it. The run's
single version, which the shared workflow asks for, is the Pipeline API's.
After a publish, a pull request records the published versions in the root
`Directory.Build.props`, which is what Package mode restores against.

The scripts only ever move the patch number. A major or minor version moves
only by a tag pushed by hand, such as `pipeline/4.6.0`, after which the patch
numbers follow it.

## What runs where

| Script | Pull request | Publish |
| --- | --- | --- |
| `fetch-assets.ps1` | Data files into the `cxx` data submodules | Same |
| `setup-environment.ps1` | Keys, paths and the strong name key | Same |
| `build-project.ps1` | Every family and the examples, Project mode | |
| `run-unit-tests.ps1` | The library tests | |
| `get-next-package-version.ps1` | | Prints the plan, each family's last and next version and why it is or is not released |
| `package-update-required.ps1` | | Whether the plan releases any family |
| `check-version-bump.ps1` | | Refuses a family whose major or minor number moves |
| `build-package-requirements.ps1` | | The native libraries, one job per platform |
| `build-package.ps1` | | Packs the families the plan releases, signs once |
| `install-package.ps1` | | Rebuilds the examples against the packages |
| `run-integration-tests.ps1` | The example tests and the Selenium contract | Same, against the packages |
| `run-performance-tests.ps1` | The console examples and the web overhead | Same |
| `publish-package.ps1` | | Pushes, tags each family, opens the versions pull request |

The secrets are the ones the separate repositories used: `ACCESS_TOKEN`,
`NUGET_API_KEY`, the `CODE_SIGNING_KEY_VAULT_*` set, `STRONG_NAME_KEY_BASE64`,
`DEVICE_DETECTION_KEY`, `DEVICE_DETECTION_URL`, `IPI_DATA_FILE_URL`,
`_51DEGREES_RESOURCE_KEY_BESPOKE` and the `ACCEPTCH_*_KEY` set.
