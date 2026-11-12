# IP Intelligence Engine Examples

![51Degrees](https://raw.githubusercontent.com/51Degrees/51degrees-dotnet/main/.github/images/logo-160.png "Data rewards the curious")

This repository contains the examples for [[ip-intelligence-dotnet](https://github.com/51Degrees/51degrees-dotnet/tree/main/ip-intelligence)].

Projects can be found in the `Examples/` folder. Currently, only on-premise examples are available. See below for a list of examples.

## Cloud (coming soon)

Cloud examples will be added once the Cloud service for IP Intelligence becomes available.

## ⚠️ Required files

See the [data](https://github.com/51Degrees/data) repository, which is the `cxx/data` submodule, on how to download and generate the necessary files.

### Mixed Examples Data Files

For the **Mixed examples** that combine Device Detection and IP Intelligence:

1. **IP Intelligence data file**: Place `51Degrees-EnterpriseIpiV41.ipi` in the `cxx/data/ip-intelligence/` directory, or use the free `51Degrees-LiteV41.ipi`, which the data repository's scripts download there.

2. **Device Detection data file**: Place `51Degrees-EnterpriseV41.hash` in the `cxx/data/ip-intelligence/` directory, or use the free `51Degrees-LiteV4.1.hash`, which the data repository's scripts download into `cxx/data/device-detection/`.

The console example downloads the free files itself when it needs them and they are missing.

## On-premise data file

The on-premise examples need an IP Intelligence data file. The examples locate
the file in the following order:

1. The "_51DEGREES_IPI_PATH" environment variable, which can be set to an
   explicit path to the data file.
2. The data submodule of this repository, `cxx/data`. When an example asks
   for a free 'Lite' file that is missing, it downloads the file there with
   the data repository's script, and prints the command it runs.
3. A search of the folder hierarchy, walking up from the working directory,
   for the expected data file name.

Most of the examples ask for the Enterprise file,
`51Degrees-EnterpriseIpiV41.ipi`, which is not free and has to be supplied.
See the Required files section above on how to download and generate the free
files.

## 📦 NuGet Package

Examples currently depend on a pre-release version of the [FiftyOne.IpIntelligence](https://www.nuget.org/packages/FiftyOne.IpIntelligence) package.  

❗Make sure to enable pre-release packages when installing it:
* Using the .NET CLI:
```sh
dotnet add package FiftyOne.IpIntelligence --prerelease
```

* In Visual Studio: check the “Include prerelease” box in the NuGet Package Manager UI.

## On-Premise

| Example                          | Target               | Use case                                                                |
| -------------------------------- | -------------------- | ----------------------------------------------------------------------- |
| Compare-Console                  | .NET 10.0            | Compare IP-to-location results against known and true records.          |
| Framework-Web                    | .NET Framework 4.6.2 | ASP.NET Framework project.                                              |
| GettingStarted-Console           | .NET 10.0            | Simple console app.                                                     |
| GettingStarted-Web               | .NET 10.0            | ASP.NET Core project.                                                   |
| LocationAnalysis-Console         | .NET 10.0            | Analysis of the location data in a data file, grouped and output as CSV.|
| Metadata-Console                 | .NET 10.0            | Accessing data file's metadata (e.g. listing properties).               |
| Metrics-Console                  | .NET 10.0            | Geographic area metrics per group, sampled across the data file.        |
| OfflineProcessing-Console        | .NET 10.0            | Batch-processing of IP addresses from a YAML file.                      |
| Performance-Console              | .NET 10.0            | "Clock-time" benchmark for assessing detection speed.                   |
| Suspicious-Console               | .NET 10.0            | Using diversity properties to assess how suspicious an IP address is.   |
| UpdateDataFile-Console           | .NET 10.0            | Auto-update features: Daily / on Start-Up / Filesystem Watcher          |
| **Mixed/GettingStarted-Console** | **.NET 10.0**        | **Combined Device Detection and IP Intelligence console app.**          |
| **Mixed/GettingStarted-Web**     | **.NET 10.0**        | **Combined Device Detection and IP Intelligence ASP.NET Core project.** |

`Compare-Console`, `Metrics-Console` and `Suspicious-Console` need an enterprise
data file, because the free lite file does not carry the location data they
analyse. `LocationAnalysis-Console` runs against the lite
file too, reporting the properties it does not carry as `NotAvailable`.

`Compare-Console` takes the data file, the truth CSV file, the output path and
the performance profile, all optional and in that order. The profile defaults
to `LowMemory`, which keeps the data file on disk. Pass `MaxPerformance` for
speed where the machine has as much free RAM as the data file is large.

`Metrics-Console` samples the address ranges in the data file rather than
visiting every address. Optional command line arguments follow the data file and
output path:

| Argument | Default | Effect |
| -------- | ------- | ------ |
| Sample percentage        | 0.1            | Proportion of each range to sample, where 1 is 100% and analyses every address in the range. |
| Max samples per range    | 8              | Most addresses taken from any one range. |
| Range percentage         | 1              | Proportion of the ranges in the data file to include. |
| Performance profile      | LowMemory      | Profile the engine is built with. Pass MaxPerformance for speed. |
| Max profiles without range | 100000       | Give up scanning once this many profiles in a row carry no range. 0 scans the whole file. |

The per-range cap is what makes a run finish at all: a single IPv6 range holds
more addresses than could ever be enumerated, so the sample percentage on its
own does not limit the work.

Choosing `MaxPerformance` and the last argument are what make a run over a
whole enterprise data file take minutes rather than hours.

The data file holds two kinds of network profile: the ranges the example needs
are on registration profiles carrying `IpRangeStart`, `IpRangeEnd` and who the
range is registered to, and those are followed by a much larger set of location
profiles carrying the geographic detail but no range at all. Once the scan has
left the registration profiles there are no more ranges to find, so it stops
rather than reading through the rest. Measured on an enterprise file, that is
601,443 ranges in the first 603,531 profiles followed by 993,628 profiles that
can add nothing.

`LowMemory`, the default, pages the data file from disk, so the example runs on
a machine with less free RAM than the data file is large. `MaxPerformance`
holds the data file in memory instead. Reading a profile's values measured 166
a second under `LowMemory` against 2,013 under `MaxPerformance`, so the scan is
far more sensitive to the profile than detection is. Pass `MaxPerformance` for
speed where the machine has as much free RAM as the data file is large.

Lower the range percentage on top of these when an approximate analysis is
enough. Discarding a range before its values are read takes almost all of its
cost with it.


## Cloud

A resource key configured with the properties needed to run the cloud examples
can be created for free [here](https://configure.51degrees.com/1QWJwHxl?utm_source=github&utm_medium=readme&utm_campaign=51degrees-dotnet&utm_content=readme.md&utm_term=cloud). To
use the resource key in the examples it can be supplied as an environment
variable called "_51DEGREES_RESOURCE_KEY".

* In order to test cloud examples against a custom endpoint - you need to launch `OnPremise/Mixed/GettingStarted-API` example 
and keep it running while launching other examples.  Depending on the IDE you use this can be either done conveniently from the IDE, or 
just using `dotnet run` in the `OnPremise/Mixed/GettingStarted-API` directory from the command line.  

| Example                          | Target               | Use case                                                                  |
| -------------------------------- | -------------------- | ------------------------------------------------------------------------- |
| Framework-Web                    | .NET Framework 4.6.2 | ASP.NET Framework project.                                                |
| GettingStarted-Console           | .NET 10.0            | Simple console app.                                                       |
| GettingStarted-Web               | .NET 10.0            | ASP.NET Core project.                                                     |
| Metadata-Console                 | .NET 10.0            | Get the available properties and evidence keys information from the Cloud |
| GetAllProperties                 | .NET 10.0            | Get all the available properties for an IP address from the Cloud         |
| **Mixed/GettingStarted-Console** | **.NET 10.0**        | **Combined Device Detection and IP Intelligence console app.**            |
| **Mixed/GettingStarted-Web**     | **.NET 10.0**        | **Combined Device Detection and IP Intelligence ASP.NET Core project.**   |


### Mixed Examples

The **Mixed examples** demonstrate how to combine Device Detection and IP Intelligence engines within a single application:

- **Mixed/GettingStarted-Console**: A console application that processes both device detection (from User-Agent / User-Agent Client Hints) and IP intelligence (from IP address), one after the other, using the 51Degrees Pipeline API.

- **Mixed/GettingStarted-Web**: An ASP.NET Core web application featuring:
  - Combined device detection and IP intelligence results in a two-column layout
  - IP address lookup functionality with custom IP input
  - Client-side evidence collection for enhanced device detection
  - Client hints support for improved browser detection
  - All device detection and IP intelligence properties displayed

## Testing

Automated tests live in the `Tests/` folder and run as part of CI:

| Test project                                                         | Covers                                              |
| ---------------------------------------------------------------------| --------------------------------------------------- |
| `FiftyOne.IpIntelligence.Example.Tests.Cloud`                        | The Cloud console examples and Cloud web examples   |
| `FiftyOne.IpIntelligence.Example.Tests.OnPremise`                    | The on-premise console examples                     |
| `FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedAPI.Tests` | The on-premise `Mixed/GettingStarted-API` example   |

Most runnable examples have an associated test. The exceptions, and why:

- **`Cloud/Framework-Web` and `OnPremise/Framework-Web`** are legacy **.NET
  Framework 4.6.2** ASP.NET (MVC / full-framework) applications. They are excluded
  from the cross-platform `net10.0` solution filter
  (`FiftyOne.IpIntelligence.Examples.Core.slnf`) and from the CI test run (which
  targets .NET on Linux, macOS and Windows), cannot be hosted in-process by the
  `net10.0` test harness, and are therefore verified by compilation only.
- **`OnPremise/GettingStarted-Web` and `OnPremise/Mixed/GettingStarted-Web`** are
  exercised manually rather than by an automated test. Unlike the Cloud web
  examples (which are tested in-process), these resolve their on-premise data file
  through a relative-path configuration override; hosting them in-process from the
  test project corrupts the pipeline element configuration. They run correctly
  when launched normally (`dotnet run`), which is how they should be verified
  until the in-process hosting issue is addressed.

### Test prerequisites

The example tests are integration tests and need the same inputs the examples do:

- **Cloud example tests** require a resource key in the `_51DEGREES_RESOURCE_KEY`
  environment variable (see the [Cloud](#cloud) section). Without it these tests
  fail with a message explaining how to obtain one.
- **On-premise example tests** require the enterprise IP Intelligence data file
  (located automatically, or via `_51DEGREES_IPI_PATH`) and the **Mixed** examples
  additionally require a device detection data file. When a required data file is
  absent, those tests report **inconclusive** rather than failing, so the gap is
  visible without being masked as a regression.
