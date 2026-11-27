param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Arch = "x64",
    [string]$Configuration = "Release",
    [string]$BuildMethod = "dotnet",
    # False on the Intel macOS entry, as live cloud tests run once per
    # operating system and the arm64 entry runs them for macOS.
    [bool]$LiveCloudTests = $true,
    # Set on the macOS entries only. ci/install-cmake.ps1 says why.
    [string]$CMakeVersion = ""
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
if ($CMakeVersion) {
    & "$PSScriptRoot/install-cmake.ps1" -Version $CMakeVersion
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

# The later steps run in this process, so what is set here holds for the whole
# job. The tests that would fail without a key skip on LIVE_CLOUD_TESTS, and
# every other live cloud test skips once its key is removed.
if (-not $LiveCloudTests) {
    $env:LIVE_CLOUD_TESTS = "false"
    Write-Host "Live cloud tests are not run here, so LIVE_CLOUD_TESTS is false"
    foreach ($key in @(Get-ChildItem Env:_51DEGREES_RESOURCE_KEY*)) {
        Remove-Item "Env:$($key.Name)"
        Write-Host "Live cloud tests are not run here, so $($key.Name) is removed"
    }
}

# The keys are not set here. The shared workflow exports each secret under
# its own _51DEGREES_ name before this script runs, and each test reads the
# key it needs by that name.

# A chromedriver on PATH is used even when it no longer matches the installed
# Chrome, and the images ship ones that lag behind. Skipping PATH makes
# Selenium Manager fetch a matching driver for the browser tests.
$env:SE_SKIP_DRIVER_IN_PATH = "true"
$env:SE_BROWSER_VERSION = "stable"
