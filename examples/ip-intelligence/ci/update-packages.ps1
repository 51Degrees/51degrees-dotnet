param(
    [Parameter(Mandatory)][string]$RepoName
)
./dotnet/outdated.ps1 `
    -RepoName:$RepoName `
    -Target FiftyOne.IpIntelligence.Examples.sln
