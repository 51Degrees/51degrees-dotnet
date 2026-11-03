# CI

The scripts here are what the shared 51Degrees workflows call, in the order
the [common-ci](https://github.com/51Degrees/common-ci) README describes.
`packages.ps1` holds what is particular to this repository: the package
families, their solutions, the native engines and the nuspec package.

## Versions

Each family keeps the version stream its own repository had. A family is
published when something under its paths changed since its last tag, at the
next patch version, and tagged `<family>/<version>`. The run's single
version, which the shared workflow asks for, is the Pipeline API's. After a
publish, a pull request records the published versions in the root
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
| `build-package-requirements.ps1` | | The native libraries, one job per platform |
| `build-package.ps1` | | Packs the changed families, signs once |
| `install-package.ps1` | | Rebuilds the examples against the packages |
| `run-integration-tests.ps1` | The example tests and the Selenium contract | Same, against the packages |
| `run-performance-tests.ps1` | The console examples and the web overhead | Same |
| `publish-package.ps1` | | Pushes, tags each family, opens the versions pull request |

The secrets are the ones the separate repositories used: `ACCESS_TOKEN`,
`NUGET_API_KEY`, the `CODE_SIGNING_KEY_VAULT_*` set, `STRONG_NAME_KEY_BASE64`,
`DEVICE_DETECTION_KEY`, `DEVICE_DETECTION_URL`, `IPI_DATA_FILE_URL`,
`_51DEGREES_RESOURCE_KEY_BESPOKE` and the `ACCEPTCH_*_KEY` set.
