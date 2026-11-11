param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet"
)

# The two old-style .NET Framework test projects. Their binding redirects are
# only honoured by vstest, so they run with it, on Windows, where msbuild
# built them. Every other test project is SDK-style and runs with dotnet
# test, whose host also honours the redirects an SDK project generates for
# its net462 build, which the x64 vstest host does not.
$OldStyle = "^FiftyOne\.(Pipeline\.Web\.Framework\.Tests|DeviceDetection\.Tests\.Framework)\.dll$"

# The library test assemblies. The example tests are integration tests, and
# the test helper packages are not tests. A native library is loaded by a
# host of the assembly's own platform, which the engine test projects set.
$Filter = "^FiftyOne\.(?!Pipeline\.Web\.Framework\.Tests\.dll$)(?!DeviceDetection\.Tests\.Framework\.dll$)(?!.*Examples?\.Tests)(?!.*TestHelpers).*Tests?(\.Core|\.Framework)?\.dll$"
# A run can be narrowed to some assemblies by setting _51DEGREES_TEST_FILTER,
# which the probe workflow does.
if ($env:_51DEGREES_TEST_FILTER) {
    $Filter = $env:_51DEGREES_TEST_FILTER
}

# The SDK-style assemblies run in parallel lanes, which ci/run-test-lanes.ps1
# describes.
& "$PSScriptRoot/run-test-lanes.ps1" `
    -RepoName $RepoName `
    -Name $Name `
    -Configuration $Configuration `
    -BlameHangTimeout "20m" `
    -Filter $Filter `
    -OutputFolder "unit"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($BuildMethod -eq "msbuild" -and -not $env:_51DEGREES_TEST_FILTER) {
    ./dotnet/run-unit-tests.ps1 `
        -RepoName $RepoName `
        -Name $Name `
        -Configuration $Configuration `
        -Arch $Arch `
        -BuildMethod "msbuild" `
        -BlameHangTimeout "20m" `
        -Filter $OldStyle
}
exit $LASTEXITCODE
