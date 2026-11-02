# Working in this repository

Notes on what is not obvious from the code. Keep them short and current.
Building, testing and the layout are described in the [README](README.md).

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
