param(
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$ProjectDir = ".",
    [string]$Name = "Release_x64",
    [string]$Arch = "x64",
    [string]$Configuration = "Release",
    [string]$StrongNameKeyBase64,
    [hashtable]$Keys
)

Write-Output "Setting SUPER_RESOURCE_KEY "
$env:SUPER_RESOURCE_KEY = $Keys.TestResourceKey

[IO.File]::WriteAllBytes("$PSScriptRoot/../51Degrees.snk", [Convert]::FromBase64String($StrongNameKeyBase64))
