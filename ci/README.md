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

`packages.Tests.ps1` checks `packages.json` and the version properties against the project files, and runs common-ci's release plan on a throwaway repository. It needs Pester 5 or later and, for the plan, a checkout of common-ci in a folder named `common-ci` beside this repository, without which those tests are skipped. From the repository root:

```
Invoke-Pester ci/packages.Tests.ps1, ci/release-plan-filter.Tests.ps1
```

[`.github/workflows/ci-script-tests.yml`](../.github/workflows/ci-script-tests.yml) runs every `ci/*.Tests.ps1` this way on Linux and Windows, for a pull request that changes `ci` and for a push to `rc` or `main`.

## What runs where

| Script | Pull request | Publish |
| --- | --- | --- |
| `fetch-assets.ps1` | Data files into the `cxx` data submodules | Same |
| `setup-environment.ps1` | The data file paths, the https certificate and the browser driver settings | Same |
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
| `run-performance-tests.ps1` | The console examples, and the web overhead with the User-Agents `evidence-user-agents.ps1` writes from the YAML evidence | Same |
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

A test that measures something, such as the memory an engine keeps after it is
disposed, writes a line starting `Build log: ` to its output. The output of a
test that passes is kept only in its results file, so `run-test-lanes.ps1`
ends by printing those lines, which `build-log-lines.ps1` reads from the
results files, and each job's log then holds what its runner measured.
`build-log-lines.Tests.ps1` tests the reading, run with Pester 5 or later.

The workflows pass the secrets to the shared workflows with `secrets: inherit`,
and each job exports every `_51DEGREES_` secret and organisation variable
under its own name, with nothing copied from one name to another. The signing
secrets reach only the job that builds the packages, and the publishing ones
only the job that publishes. Each test reads the key it needs by its name:

- `_51DEGREES_LICENSE_KEY`, the data license. The shared `fetch-assets.ps1`
  downloads `TAC-HashV41.hash` and `51Degrees-EnterpriseIpiV41.ipi` from the
  Distributor with it, and fails naming it when it is not set. The data file
  update example tests read it too.
- `_51DEGREES_RESOURCE_KEY_BESPOKE`, the resource key that carries every
  property, for the live cloud tests. A test that runs a cloud example gives
  it this key as `_51DEGREES_RESOURCE_KEY`, the name a customer sets, which
  no job sets. `run-integration-tests.ps1` does the same for the cloud example
  that the Selenium contract runs against.
- `_51DEGREES_RESOURCE_KEY_BASIC`, `_51DEGREES_RESOURCE_KEY_51DID` and
  `_51DEGREES_LICENSE_KEY_51DID`, for the live 51Did tests. The license key
  reads only results sealed with the 51Did resource key.
  `.github/workflows/cloud-51did.yml` runs those tests on their own, nightly,
  with the same names.
- `_51DEGREES_LICENSE_KEY_BESPOKE`, for the Accept-CH tests of the cloud web
  examples, which ask for one SetHeader property each with `values`.
- `_51DEGREES_CLOUD_ENDPOINT`, an organisation variable naming the cloud the
  tests and examples call. When it is not set they call the public cloud, as
  a customer's copy does.

A live cloud test with no key skips and names the variable it needs.

`build-package.ps1` reads the strong name key as
`STRONG_NAME_KEY_BASE64` and the code signing key vault as the
five `CODE_SIGNING_KEY_VAULT_` names, and stops naming the first one that is
not set. It writes the strong name key to `51Degrees.snk`, and each project
signs with the private key only when that file is there, so every other build
public signs with `51Degrees.publickey`, as a local build does. The Package
job in `.github/workflows/publish.yml` gets its NuGet key from nuget.org
trusted publishing and gives it to `publish-package.ps1` as
`NUGET_API_KEY`, with `CI_ACCESS_TOKEN` for git and
the GitHub releases.

`run-unit-tests.ps1` and `run-integration-tests.ps1` each begin by running
`start-chrome-once.ps1`, which asks the Chrome driver for its version and
starts Chrome once, headless, and then runs one session through the driver as
a browser test does (the driver started and ready, a session, a blank page and
the session ended), printing how long each part took. The first start of
Chrome and the first driver session on a runner take many times longer than
the ones after them, so both are made before the browser tests and count
against no test's limit. A part that takes more than 300 seconds is stopped,
and one that fails is reported, both as warnings, and the tests then run as
they would have. `start-chrome-once.Tests.ps1` tests it with stand-ins for
Chrome and the driver, run with Pester 5 or later.
