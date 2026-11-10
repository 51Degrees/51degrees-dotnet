param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

Push-Location $RepoName
try {
    dotnet build pipeline/Tests/Web.Tests/FiftyOne.Pipeline.Web.Tests.csproj -p:FiftyOneReferences=Project -p:BuiltOnCI=true -c $Configuration
    msbuild pipeline/Tests/Web.Framework.Tests/FiftyOne.Pipeline.Web.Framework.Tests.csproj -t:build -restore -verbosity:minimal "-p:Configuration=$Configuration" "-p:FiftyOneReferences=Project" "-p:BuiltOnCI=true"
} finally {
    Pop-Location
}
