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

./dotnet/run-unit-tests.ps1 `
    -RepoName $RepoName `
    -Name $Name `
    -Configuration $Configuration `
    -Arch $Arch `
    -BuildMethod $BuildMethod `
    -BlameHangTimeout "20m" `
    -Filter $Filter
exit $LASTEXITCODE
