# Working in this repository

If you have been asked to get an example running, follow
[Run the examples](#run-the-examples). It gives every step in order, from a
fresh clone to the output. The sections after it are notes on what is not
obvious from the code, for changing it. Keep them short and current. The
[README](README.md) describes the build, the tests and the layout.

## Run the examples

Run each command from the repository root unless the step says otherwise.
Device Detection's on-premise `GettingStarted-Console` is the quickest example
to show, because its data file is about 35 MB. IP Intelligence's on-premise
`GettingStarted-Console` needs a download of about 230 MB that unpacks to
about 460 MB, so run it second.

The examples build against the published NuGet packages unless
`FiftyOneReferences` is set to `Project` (see
[References between the folders](#references-between-the-folders)). Those
packages carry the native engines, so running an example needs no C or C++
compiler and no CMake.

### Prerequisites

- Git.
- The .NET 10 SDK, because the getting-started examples target `net10.0`.
- Network access to GitHub, to nuget.org for the packages the examples
  restore, and to `distributor.51degrees.com` for the data files.
- For the data files, `curl`, `gzip` and `md5sum` (`md5` on macOS) on Linux
  and macOS, where the examples download with the shell script, and Windows
  PowerShell 5.1 or PowerShell 7 on Windows, where they use the PowerShell
  script.

Clone the repository with its submodules.

```
git clone --recurse-submodules https://github.com/51Degrees/51degrees-dotnet.git
cd 51degrees-dotnet
```

In a clone made without `--recurse-submodules`, fetch the submodules.

```
git submodule update --init --recursive
```

### Data files

The examples use the free Lite data files, which need no licence key. They are
not stored in this repository. The `cxx` submodule holds the
[data](https://github.com/51Degrees/data) repository as `cxx/data`, which has a
link to each file, and a script downloads them into `cxx/data/device-detection`
and `cxx/data/ip-intelligence`.

On Linux and macOS, run the shell script.

```
sh cxx/data/get-data-files.sh
```

On Windows, run the PowerShell script. Windows PowerShell can refuse to run a
script file under the policy Windows sets by default, so run it the way the
examples do.

```
powershell -NoProfile -ExecutionPolicy Bypass -File cxx/data/get-data-files.ps1
```

Where PowerShell 7 is installed, `pwsh cxx/data/get-data-files.ps1` does the
same on any system. Either script downloads every free file, and keeps a file
that is already there. To download one file, give its name, as in
`sh cxx/data/get-data-files.sh 51Degrees-LiteV4.1.hash`.

#### Check the files

- `cxx/data/device-detection/51Degrees-LiteV4.1.hash` is more than 10 MB.
- `cxx/data/ip-intelligence/51Degrees-LiteV41.ipi` is more than 100 MB.

### Run

`dotnet run` builds an example and runs it. The first run restores the
packages, so it takes longer.

Device Detection looks for `51Degrees-LiteV4.1.hash` in `cxx/data`, which it
finds from the folder it runs in or a folder above it (`FindFile` in
`examples/device-detection/ExampleBase/ExampleUtils.cs`). When the file is not
there, the example runs the download script for it, with Windows PowerShell on
Windows and `sh` on Linux and macOS. Run it from the repository root, on any
system.

```
dotnet run --project examples/device-detection/OnPremise/GettingStarted-Console
```

The IP Intelligence example looks for the paid Enterprise file,
`51Degrees-EnterpriseIpiV41.ipi`, so it never downloads the Lite file. Download
the Lite file first, as "Data files" says, then give its path in the
`51DEGREES_IPI_PATH` environment variable. The name starts with a digit, so on
Linux and macOS set it with `env`.

```
env 51DEGREES_IPI_PATH="$PWD/cxx/data/ip-intelligence/51Degrees-LiteV41.ipi" dotnet run --project examples/ip-intelligence/OnPremise/GettingStarted-Console
```

In PowerShell, on any system, run these.

```
${env:51DEGREES_IPI_PATH} = "$PWD/cxx/data/ip-intelligence/51Degrees-LiteV41.ipi"
dotnet run --project examples/ip-intelligence/OnPremise/GettingStarted-Console
```

`51DEGREES_DD_PATH` does the same for Device Detection, and a path given as
the first argument after `--` takes the place of either variable.

### Correct output

When the Device Detection example downloads its data file, it first prints
`'51Degrees-LiteV4.1.hash' is missing, so downloading it with:` and the
command, then the script's `Downloading` and `Saved` lines.

The Device Detection example processes six sets of evidence, then each device
ID it found as `query.51D_deviceId`. For each it prints `Input values:` and
the evidence, then `Results:` with `Mobile Device`, `Platform Name`,
`Platform Version`, `Browser Name`, `Browser Version`, `DeviceId`,
`Headless Browser`, `Has Web Driver` and `Window Visible`, and then `Matched`
with the parts of the User-Agent that matched. The Lite file has no
`IsHeadless`, `HasWebDriver` or `IsVisible`, so those three lines read
`No value (property missing from the resource key or data file)`. The console
logger then reports the data file, in lines like these.

```
info: FiftyOne.DeviceDetection.Examples.OnPremise.GettingStartedConsole.Program[0]
      Using a 'Lite' data file created '<date>' from location '<path>'
```

It also warns that the example uses the Lite file, and may warn that the file
is more than 30 days old. Both are expected. The exit code is 0.

The IP Intelligence example looks up five IPv4 addresses. For each it prints
`Input values:` with `query.client-ip`, then `Results:` with a line for each
property, starting with `RegisteredName`, `RegisteredOwner` and
`RegisteredCountry`. It reads properties such as `Country` without checking
that the data file has them, and the
[data repository README](https://github.com/51Degrees/data#ip-intelligence)
says the Lite file has only RegisteredCountry, RegisteredName and
RegisteredOwner. Reading a property the data file does not have throws
`PropertyMissingException` (`GetAs` in `pipeline/Engines/Data/AspectDataBase.cs`),
which is why `LocationAnalysis-Console` catches it. So with the Lite file the
example can stop with that exception at the first property the file lacks.
That is a limit of the Lite file, not a fault in your setup. CI runs this
example only with the Enterprise file.

The paid data files have many more properties, as the
[pricing page](https://51degrees.com/pricing?utm_source=github&utm_medium=docs&utm_campaign=51degrees-dotnet&utm_content=agents.md&utm_term=correct-output)
describes.

### Cloud examples

The cloud examples need a resource key, which a person creates in the
configurator, so ask for one if none is set. A free key for the Device
Detection examples can be made
[here](https://configure.51degrees.com/Wkqxf3Bs?utm_source=github&utm_medium=docs&utm_campaign=51degrees-dotnet&utm_content=agents.md&utm_term=cloud-examples),
and one for the IP Intelligence examples
[here](https://configure.51degrees.com/1QWJwHxl?utm_source=github&utm_medium=docs&utm_campaign=51degrees-dotnet&utm_content=agents.md&utm_term=cloud-examples).

The Device Detection cloud example takes the key as its first argument or from
`_51DEGREES_RESOURCE_KEY`, unless the `ResourceKey` in its `appsettings.json`
has been filled in, which then wins.

```
env _51DEGREES_RESOURCE_KEY=<key> dotnet run --project examples/device-detection/Cloud/GettingStarted-Console
```

In PowerShell, set `$env:_51DEGREES_RESOURCE_KEY = '<key>'` first. The output
has the same `Input values:` and `Results:` blocks for five properties. When
the key lacks a property, the example ends by saying that some properties need
a paid subscription. The IP Intelligence cloud example reads its key from
`51DEGREES_RESOURCE_KEY` instead (`examples/ip-intelligence/README.md`).

### Pitfalls

- **A data file that is not there.** `cxx/data` holds a `.link` for each file
  rather than the file. The Device Detection example runs the download script
  for a missing file, so it needs the network and the tools under
  "Prerequisites". The IP Intelligence example does not, so download its file
  first. Check the file sizes as above.
- **Missing submodules.** The Device Detection example then logs `Failed to
  find a device detection data file` and asks for
  `git submodule update --init --recursive`, which fetches them. A zip
  download of the repository has no submodules, so clone with Git.
- **The IP Intelligence example without `51DEGREES_IPI_PATH`.** It searches for
  the Enterprise file name, gives up after 10 seconds and logs `Failed to find
  a IP Intelligence data file`, even when the Lite file is in place.
- **global.json.** `pipeline` and `device-detection` each have a `global.json`
  that asks for the .NET 8 SDK. Run `dotnet` from the repository root, as
  above, unless .NET 8 is installed too.
- **Project references.** `-p:FiftyOneReferences=Project`, which CI uses,
  builds the native engines from `cxx`. That needs CMake 3.24 or later, a
  C++17 compiler, PowerShell 7 and a platform such as `-p:Platform=x64`, as
  the README says. It is not needed to run the examples.
- **Platforms the packages cover.** The published
  `FiftyOne.DeviceDetection.Hash.Engine.OnPremise` 4.5.179 has native
  libraries for Windows x64 and x86, Linux x64 and arm64, and macOS x64 and
  arm64. `FiftyOne.IpIntelligence.Engine.OnPremise` 4.5.154 has Windows x64,
  Linux x64 and macOS x64 and arm64 only. Neither has Windows on Arm. On a
  platform a package lacks, the example cannot load the engine.
- **Warnings are errors.** The example projects set `TreatWarningsAsErrors`,
  so an SDK newer than the one CI uses can stop the build on a new warning.
- **The .NET Framework examples.** The `Framework-Web` examples need `msbuild`
  on Windows, as [Frameworks](#frameworks) says, and `dotnet run` cannot run
  them.
- **What CI does not test.** CI builds and tests the examples with project
  references (`ci/build-project.ps1`), and against the packages only when it
  publishes (`ci/README.md`). It runs the example tests with the paid data
  files that `ci/fetch-assets.ps1` fetches, never the Lite IP Intelligence
  file. The Device Detection example tests send the output to
  `TextWriter.Null`, and the IP Intelligence one checks only a few words of
  it.

## References between the folders

A project that uses a package built in another folder declares a
`FiftyOneReference` item, not a `PackageReference` or a `ProjectReference`.
The root `Directory.Build.targets` turns it into one or the other from the
`FiftyOneReferences` property, which defaults to `Package`. CI builds and
tests with `Project`, so a change that crosses folders is tested together.
When a package's dependency changes, change the item, and when a version
changes, change the `FiftyOne*Version` property in the root
`Directory.Build.props`. The metadata that holds the path is `ProjectPath`,
because `Project` is the name MSBuild gives a `ProjectReference` GUID.

Each folder that came from a separate repository keeps its own
`Directory.Build.props`, which imports the root one on its first line. MSBuild
only imports the nearest file of that name, so a new folder needs the same
line.

## Native builds

The on-premise engine projects run `PreBuild.ps1`, which calls `cmake` on the
engine folder's `CMakeLists.txt`. That file includes `cxx/CMakeLists.txt` with
the other product turned off, so each engine compiles its own copy of common,
which the two products need built differently. The interface files and the
committed SWIG wrappers include through `cxx/src/`, so the repository root is
on the wrapper's include path. `RebuildSwig` is off in CI, so the committed
wrappers are what ships.

## Data files

Tests and examples read the free data files from `cxx/device-detection-data`
and `cxx/ip-intelligence-data`, by searching upward from where they run. The
IP Intelligence translation resources are embedded from
`cxx/ip-intelligence-data/Translations`. CI puts the Enterprise files in the
same folders.

## Layout

Every package project sits directly under its product folder, with that
folder's tests in `Tests` and examples in `Examples`, as the node repository
does. The folder is the package id without the `FiftyOne.<Product>.` prefix
(`pipeline/Core`, `device-detection/Cloud`), and the project file inside keeps
the full name, which sets the assembly and package id. A new project follows
the same rule. The submodules under `pipeline` keep their old names in
`.gitmodules`, because the name is only an identifier. The two example repositories became `examples/device-detection` and
`examples/ip-intelligence`, with their `Cloud`, `OnPremise` and `Tests` folders
at the top. `git log --follow -m` traces a file back through the import.

## Frameworks

`pipeline` and `device-detection` keep a `global.json` that pins the SDK they
were released with. `dotnet` run from the repository root uses the newest SDK
installed, which builds every folder. The .NET Framework projects, which are
the `Web.Framework` integration and the `Framework-Web` examples, are old-style
projects that need `msbuild` on Windows.

## Versions

Until the first release from this repository, each package family keeps the
version its separate repository last published, held in the root
`Directory.Build.props`.

## Pull requests

Pull requests target `rc`, not `main`. Changes collect on `rc` and reach `main`
together in one pull request when a release is wanted, and the publish builds
`main`.

Keep a pull request a draft until it has been reviewed, because the nightly
merges any pull request into `rc` that is not a draft and has no review
requested. The "Only rc into main" check fails a pull request into `main` from
any other branch.
