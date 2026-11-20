param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Arch = "x64",
    [string]$Configuration = "Release",
    [string]$BuildMethod = "dotnet",
    [string]$StrongNameKeyBase64
)
$ErrorActionPreference = "Stop"

$RepoPath = [IO.Path]::Combine($pwd, $RepoName)

if ($BuildMethod -ne "dotnet") {
    ./environments/setup-msbuild.ps1
    ./environments/setup-vstest.ps1
}
if ($IsLinux) {
    ./environments/setup-multilib.ps1
}
if ($IsWindows) {
    # The example test output paths are long.
    reg add "HKLM\SYSTEM\CurrentControlSet\Control\FileSystem" /v LongPathsEnabled /t REG_DWORD /d 1 /f
}

# The web examples and their tests run over https.
dotnet dev-certs https

# The data files fetch-assets.ps1 put in the data submodule of cxx.
$env:_51DEGREES_DD_PATH = "$RepoPath/cxx/data/device-detection/TAC-HashV41.hash"
$env:_51DEGREES_IPI_PATH = "$RepoPath/cxx/data/ip-intelligence/51Degrees-EnterpriseIpiV41.ipi"

# The keys the tests and examples read are not set here. The shared
# set-resource-keys step exports each one under its _51DEGREES_ name before
# this script runs, _51DEGREES_RESOURCE_KEY and _51DEGREES_LICENSE_KEY among
# them, and setting one here from an option would replace or clear it.

# A chromedriver on PATH is used even when it no longer matches the installed
# Chrome, and the images ship ones that lag behind. Skipping PATH makes
# Selenium Manager fetch a matching driver for the browser tests.
$env:SE_SKIP_DRIVER_IN_PATH = "true"
$env:SE_BROWSER_VERSION = "stable"

# The strong name key, which the assemblies are signed with on CI.
if ($StrongNameKeyBase64) {
    [IO.File]::WriteAllBytes("$RepoPath/51Degrees.snk", [Convert]::FromBase64String($StrongNameKeyBase64))
}
