param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet"
)

# The library test assemblies. The example tests are integration tests, and
# the test helper packages are not tests.
$Filter = "^FiftyOne\.(?!.*Examples?\.Tests)(?!.*TestHelpers).*Tests?(\.Core|\.Framework)?\.dll$"
# A run can be narrowed to some assemblies by setting FIFTYONE_TEST_FILTER,
# which the probe workflow does.
if ($env:FIFTYONE_TEST_FILTER) {
    $Filter = $env:FIFTYONE_TEST_FILTER
}
# The shared step gives vstest the architecture, /Platform:x86 say, only when
# the filter does not contain the text "dll", so on the Windows jobs the
# extension is spelt without it. An x86 build needs an x86 test host, and the
# example test assemblies are Any CPU, which would otherwise run as x64.
if ($BuildMethod -eq "msbuild") {
    $Filter = $Filter.Replace('.dll$', '.d[l]l$')
}

./dotnet/run-unit-tests.ps1 `
    -RepoName $RepoName `
    -Name $Name `
    -Configuration $Configuration `
    -Arch $Arch `
    -BuildMethod $BuildMethod `
    -BlameHangTimeout "20m" `
    -Filter $Filter
exit $LASTEXITCODE
