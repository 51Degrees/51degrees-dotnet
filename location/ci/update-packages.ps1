
param(
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$ProjectDir = ".",
    [string]$Name
)

./dotnet/run-update-dependencies.ps1 -RepoName $RepoName -ProjectDir $Solution -Name $Name


exit $LASTEXITCODE