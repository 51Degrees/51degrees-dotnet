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

./dotnet/run-unit-tests.ps1 `
    -RepoName $RepoName `
    -Name $Name `
    -Configuration $Configuration `
    -Arch $Arch `
    -BuildMethod $BuildMethod `
    -BlameHangTimeout "20m" `
    -Filter $Filter
exit $LASTEXITCODE
