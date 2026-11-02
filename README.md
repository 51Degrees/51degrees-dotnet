# 51Degrees .NET

![51Degrees](https://51degrees.com/img/logo.png?utm_source=github&utm_medium=readme&utm_campaign=51degrees-dotnet&utm_content=readme.md&utm_term=51degrees-dotnet "Data rewards the curious") **.NET packages for the Pipeline API, Device Detection, IP Intelligence and Location**

This repository holds the 51Degrees .NET packages: the Pipeline API, Device
Detection, IP Intelligence and Location, with the common and caching libraries
they build on. Each package is still published to NuGet under its own id.

| Product | Read first | Documentation |
| --- | --- | --- |
| Pipeline API | [pipeline/README.md](pipeline/README.md) | [.NET](https://51degrees.com/documentation/apis/pipeline-dotnet/index.html?utm_source=github&utm_medium=readme&utm_campaign=51degrees-dotnet&utm_content=readme.md&utm_term=pipeline-dotnet) |
| Device Detection | [device-detection/README.md](device-detection/README.md) | [.NET](https://51degrees.com/documentation/apis/device-detection-dotnet/index.html?utm_source=github&utm_medium=readme&utm_campaign=51degrees-dotnet&utm_content=readme.md&utm_term=device-detection-dotnet) |
| IP Intelligence | [ip-intelligence/README.md](ip-intelligence/README.md) | [.NET](https://51degrees.com/documentation/apis/ip-intelligence-dotnet/index.html?utm_source=github&utm_medium=readme&utm_campaign=51degrees-dotnet&utm_content=readme.md&utm_term=ip-intelligence-dotnet) |
| Location | [location/README.md](location/README.md) | [.NET](https://51degrees.com/documentation/apis/location-dotnet/index.html?utm_source=github&utm_medium=readme&utm_campaign=51degrees-dotnet&utm_content=readme.md&utm_term=location-dotnet) |

## What is where

| Path | Contents |
| --- | --- |
| `common` | `FiftyOne.Common`, shared by every 51Degrees .NET product |
| `caching` | `FiftyOne.Caching`, the cache interfaces and the LRU cache |
| `pipeline` | The Pipeline API packages, `FiftyOne.Pipeline.*` and `FiftyOne.Did*` |
| `device-detection` | The Device Detection packages, `FiftyOne.DeviceDetection*` |
| `ip-intelligence` | The IP Intelligence packages, `FiftyOne.IpIntelligence*` |
| `location` | The Location packages, `FiftyOne.Geolocation*` |
| `examples/device-detection`, `examples/ip-intelligence` | The Device Detection and IP Intelligence examples, with their tests |
| `cxx` | Submodule of [51degrees-cxx](https://github.com/51Degrees/51degrees-cxx), with the C and C++ code the on-premise engines build and the data file submodules |
| `FiftyOne.slnx` | A solution holding every project. `FiftyOne.Core.slnf` leaves out the .NET Framework projects for `dotnet build`. Each folder also keeps the solutions it had before |

## Getting the code

```
git clone --recurse-submodules https://github.com/51Degrees/51degrees-dotnet.git
```

If you have already cloned the repository, fetch the submodules with:

```
git submodule update --init --recursive
```

The free Device Detection Lite data file in `cxx/device-detection-data` is
stored with [Git LFS](https://git-lfs.github.com/). Install Git LFS and run
`git lfs pull` in that folder to fetch it. The free IP Intelligence file is in
`cxx/ip-intelligence-data`.

## Building

Everything, from the repository root:

```
dotnet build FiftyOne.Core.slnf
```

On Windows with Visual Studio, `msbuild FiftyOne.slnx -restore` builds the
.NET Framework projects as well.

Or one product, for example `dotnet build pipeline/FiftyOne.Pipeline.sln`.

The on-premise engines, `FiftyOne.DeviceDetection.Hash.Engine.OnPremise` and
`FiftyOne.IpIntelligence.Engine.OnPremise`, compile the C and C++ code in
`cxx` into a native library before the managed build. That needs
[CMake](https://cmake.org/) 3.24 or later, a C++17 compiler and PowerShell 7
on the path, and a platform other than `AnyCPU`, for example:

```
dotnet build device-detection/FiftyOne.DeviceDetection.sln -p:Platform=x64
```

On Windows, Visual Studio 2019 or later, or the C++ Build Tools, with
Platform Toolset `v142` and Windows SDK `10.0.18362.0` or later.

The ASP.NET (.NET Framework) projects, such as `FiftyOne.Pipeline.Web.Framework`
and the `Framework-Web` examples, build with `msbuild` on Windows and are left
out of the `.Core.slnf` solution filters that `dotnet build` uses.

## Package and project references

A project here that uses a package built in another folder of this repository,
for example Device Detection using `FiftyOne.Pipeline.Engines`, declares that
once as a `FiftyOneReference` item, and a property chooses what it becomes:

| `FiftyOneReferences` | The reference | When |
| --- | --- | --- |
| `Package` | A `PackageReference` to the published package, at the version in `Directory.Build.props` | The default, so a checkout builds the way a consumer's project does, and the examples show how the packages are used |
| `Project` | A `ProjectReference` to the project next to it | A change that crosses packages, which is how CI builds and tests the repository |

Choose project references with any of these, the first that is set wins:

```
dotnet build FiftyOne.slnx -p:FiftyOneReferences=Project
```

a `Directory.Build.local.props` file next to this README, which is not
committed:

```xml
<Project>
  <PropertyGroup>
    <FiftyOneReferences>Project</FiftyOneReferences>
  </PropertyGroup>
</Project>
```

or the `FiftyOneReferences` environment variable. `Directory.Build.props` and
`Directory.Build.targets` at the root hold the rule.

## Tests

The tests are MSTest, run with `dotnet test` on a solution, a solution filter
or a project, for example:

```
dotnet test pipeline/FiftyOne.Pipeline.sln
dotnet test device-detection/FiftyOne.DeviceDetection.Core.slnf -p:Platform=x64
```

The on-premise tests find the free data files in `cxx` by searching upward
from the test assembly. Point them at other files, such as the Enterprise
files, with `_51DEGREES_DD_PATH` and `_51DEGREES_IPI_PATH`. The tests that use
the cloud read a resource key from `_51DEGREES_RESOURCE_KEY`, and the 51Did
tests a license key from `_51DEGREES_LICENSE_KEY`. See each folder's README
for the keys and data files its tests use.

## Regenerating the SWIG wrappers

The wrappers in each on-premise engine folder are generated from
`hash_csharp.i` and `ipi_csharp.i`, which include the interface files in
`cxx/src`. Configure the native build with `-DRebuildSwig=On` and SWIG 4.2 or
later on the path to generate them again.

## History

The repositories `common-dotnet`, `caching-dotnet`, `pipeline-dotnet`,
`device-detection-dotnet`, `ip-intelligence-dotnet`, `location-dotnet`,
`device-detection-dotnet-examples` and `ip-intelligence-dotnet-examples` were
brought in with `git subtree add`, without `--squash`, so commit SHAs match the
originals. The imported commits keep their original paths, so to trace a file
back through the import ask git to follow renames through merges:

```
git log --follow -m -- pipeline/README.md
```

## Licence

[European Union Public Licence v1.2](LICENSE)
