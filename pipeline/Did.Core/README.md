# 51Degrees Identifier (51Did) core types

![51Degrees](https://raw.githubusercontent.com/51Degrees/51degrees-dotnet/main/.github/images/logo-160.png "Data rewards the curious")

[![NuGet](https://img.shields.io/nuget/v/FiftyOne.Did.Core)](https://www.nuget.org/packages/FiftyOne.Did.Core)
[![License](https://img.shields.io/badge/license-EUPL--1.2-blue)](https://github.com/51Degrees/51degrees-dotnet/blob/main/LICENSE)

Shared types for the **51Degrees Identifier (51Did)**, holding the data
interface that every engine returning a 51Did exposes, together with the
property builder those engines share.

The namespace and assembly are named `FiftyOne.Did.Core` only because a
.NET identifier cannot start with a digit. The product is the 51Did.

## The six forms of a 51Did

A 51Did is issued by the 51Degrees Cloud service in three kinds, each of
which comes either global across all callers or scoped to the caller's
licence key.

| Kind          | Global        | Licence scoped |
|---------------|---------------|----------------|
| Probabilistic | `IdProbGlobal`| `IdProbLic`    |
| Random        | `IdRandGlobal`| `IdRandLic`    |
| Hashed email  | `IdHemGlobal` | `IdHemLic`     |

A probabilistic 51Did is the same for the same device and network. A
random one carries a server generated GUID. A hashed email one is
derived from an email address and a salt supplied by the caller.

## This package

This is a shared dependency rather than something to use directly. To
obtain a 51Did, reference
[FiftyOne.Did.Cloud](https://www.nuget.org/packages/FiftyOne.Did.Cloud),
which is the engine that returns one from the 51Degrees Cloud service.
To read a 51Did that has already been returned, reference
[FiftyOne.Did](https://www.nuget.org/packages/FiftyOne.Did), which
parses it and gives access to the match key.

## Find out more

- [51Did](https://51degrees.com/developers/51did?utm_source=nuget&utm_medium=package&utm_campaign=51degrees-dotnet&utm_content=fiftyone.did.core&utm_term=find-out-more-51did)
- [51Did documentation](https://51degrees.com/documentation/_identifiers_51_did.html?utm_source=nuget&utm_medium=package&utm_campaign=51degrees-dotnet&utm_content=fiftyone.did.core&utm_term=find-out-more-documentation)
- [51Did inspector](https://51degrees.com/developers/51did-inspector?utm_source=nuget&utm_medium=package&utm_campaign=51degrees-dotnet&utm_content=fiftyone.did.core&utm_term=find-out-more-51did-inspector)
- https://github.com/51Degrees/51degrees-dotnet
