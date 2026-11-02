# Release notes

## First release from this repository

The packages that used to come from eight repositories now come from this one:
`FiftyOne.Common`, `FiftyOne.Caching`, the `FiftyOne.Pipeline.*` and
`FiftyOne.Did*` packages, `FiftyOne.DeviceDetection*`, `FiftyOne.IpIntelligence*`
and `FiftyOne.Geolocation*`. Package ids are unchanged. Consumers upgrade as for
any other version.

### Changes carried from the C and C++ code

The on-premise engines build from [51degrees-cxx](https://github.com/51Degrees/51degrees-cxx)
4.5.100, which replaces the separate device-detection-cxx and ip-intelligence-cxx
repositories. These changes arrive with it.

IP Intelligence:

- An IPv4-mapped IPv6 address such as `::ffff:8.8.8.8` gets the same answers as
  `8.8.8.8`, in every text form and as 16 bytes, as the
  [address forms section of the specification](https://github.com/51Degrees/specifications/blob/main/ip-intelligence-specification/pipeline-elements/ip-intelligence-on-premise.md)
  describes. `::8.8.8.8` and `64:ff9b::8.8.8.8` stay IPv6 addresses.
  ([51degrees-cxx#55](https://github.com/51Degrees/51degrees-cxx/pull/55))
- An address below the lowest key in a graph gets no value for that graph's
  properties rather than the answer for an unrelated network. A property that
  is mandatory with a default reads as that default. (51degrees-cxx#55)
- The Balanced performance profile keeps in memory what the named required
  properties need, so naming properties gives faster lookups.
  ([51degrees-cxx#49](https://github.com/51Degrees/51degrees-cxx/pull/49))

Device Detection:

- A single header longer than `maxMatchedUserAgentLength`, 500 bytes by
  default, is ignored rather than matched on.
  ([51degrees-cxx#19](https://github.com/51Degrees/51degrees-cxx/pull/19))
- `DeviceId` can be named as a required property, and naming it alongside
  properties that do not cover every component gives the full id rather than
  zeros. ([51degrees-cxx#21](https://github.com/51Degrees/51degrees-cxx/pull/21))

Both products:

- A data file reload that fails leaves the data set in use and reports the
  failure, so the engine goes on answering from the old data. `RefreshData`
  throws as before, now with the status message.
  ([51degrees-cxx#60](https://github.com/51Degrees/51degrees-cxx/pull/60))
- A rejected data file reports `INCORRECT_VERSION` or `CORRUPT_DATA`, and the
  corrupt data message asks to check the file is complete and uncompressed.
  ([51degrees-cxx#35](https://github.com/51Degrees/51degrees-cxx/pull/35))
- A value or property with no description or URL gives an empty string, where
  data files since June 2025 gave the first string in the file.
  ([51degrees-cxx#37](https://github.com/51Degrees/51degrees-cxx/pull/37))

### Changes in the .NET code

- `Pipeline.Process` on a disposed pipeline throws `ObjectDisposedException`,
  as `FlowData.Process` has always documented.
- The on-premise engine projects build the native library for the machine's
  architecture when built as AnyCPU, so they build from any solution.
- The test helpers find the data files in the `cxx` data folders.
