param(
    [Parameter(Mandatory=$true)]
    [string]$OrgName,
    [string]$GitHubUser = "Automation51D",
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$ProjectDir = ".",
    [string]$Name = "Release_x64",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet",
    [string]$Configuration = "Release",
    [hashtable]$Keys
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

# Enable Win32 long paths to avoid DLL loading failures on Windows runners
# where the workspace path can exceed MAX_PATH (260 chars).
if ($IsWindows) {
    reg add "HKLM\SYSTEM\CurrentControlSet\Control\FileSystem" /v LongPathsEnabled /t REG_DWORD /d 1 /f
}

$RepoPath = [IO.Path]::Combine($pwd, $RepoName)

if ($BuildMethod -ne "dotnet") {

    # Setup the MSBuild environment if it is required.
    ./environments/setup-msbuild.ps1
    ./environments/setup-vstest.ps1
}

if ($IsLinux) {
    # Shared, because the same call in five repositories carried the same
    # fault: these packages do not exist on arm64, so asking for them on
    # an ARM runner fails the step. See common-ci environments/README.md.
    ./environments/setup-multilib.ps1

}

dotnet dev-certs https

$env:IPINTELLIGENCEDATAFILE = [IO.Path]::Combine($RepoPath, "ip-intelligence-data", "51Degrees-EnterpriseIpiV41.ipi")
$env:RESOURCE_KEY = $Keys.TestResourceKey
# Aligned environment variable names. These are checked first by the examples
# and tests. The legacy names above are retained for backwards compatibility.
${env:51DEGREES_IPI_PATH} = [IO.Path]::Combine($RepoPath, "ip-intelligence-data", "51Degrees-EnterpriseIpiV41.ipi")
${env:51DEGREES_RESOURCE_KEY} = $Keys.TestResourceKey
$env:IPINTELLIGENCELICENSEKEY_DOTNET = $Keys.DeviceDetection # TBD

Write-Debug "env:IPINTELLIGENCEDATAFILE = <$($env:IPINTELLIGENCEDATAFILE)>"

./dotnet/add-nuget-source.ps1 `
    -Source "https://nuget.pkg.github.com/$OrgName/index.json" `
    -UserName $GitHubUser `
    -Key $env:GITHUB_TOKEN


$MixedApiProjPath = [IO.Path]::Combine($RepoPath, "Examples", "OnPremise", "Mixed", "GettingStarted-API")
Push-Location $MixedApiProjPath
try {
    Write-Information "Entered $MixedApiProjPath"
    & ./inject-secrets.ps1 `
        -DDLicenseKey $Keys.DeviceDetection `
        -IPIFileURL $Keys.IpIntelligenceUrl `
        -Clean
} finally {
    Write-Information "Leaving $MixedApiProjPath"
    Pop-Location
}


exit $LASTEXITCODE
