param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Arch = "x64",
    [string]$Configuration = "Release",
    [string]$BuildMethod = "dotnet",
    [string]$StrongNameKeyBase64,
    [hashtable]$Keys
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
$env:_51DEGREES_IPI_PATH = $env:_51DEGREES_IPI_PATH
${env:_51DEGREES_IPI_PATH} = $env:_51DEGREES_IPI_PATH

# The resource keys the cloud tests and examples read. The names that start
# with _51DEGREES_RESOURCE_KEY are the convention, and the shared
# set-resource-keys step exports every organisation secret of that shape. The
# older names are still read by some tests.
$env:_51DEGREES_RESOURCE_KEY = $Keys.TestResourceKey
$env:_51DEGREES_RESOURCE_KEY = $Keys.TestResourceKey
$env:_51DEGREES_RESOURCE_KEY = $Keys.TestResourceKey
${env:_51DEGREES_RESOURCE_KEY} = $Keys.TestResourceKey
$env:_51DEGREES_RESOURCE_KEY_CLOUD_V5_BESPOKE = $Keys.TestResourceKey
$env:ACCEPTCH_BROWSER_KEY = $Keys.AcceptCHBrowserKey
$env:ACCEPTCH_HARDWARE_KEY = $Keys.AcceptCHHardwareKey
$env:ACCEPTCH_PLATFORM_KEY = $Keys.AcceptCHPlatformKey
$env:ACCEPTCH_NONE_KEY = $Keys.AcceptCHNoneKey

# The license key the device detection data update example and test use.
# No IP Intelligence key is set. The device detection key was set here once,
# and the IP Intelligence update service refuses it, so the update test
# copied the 6 GB data file and then reported inconclusive on every job.
$env:_51DEGREES_LICENSE_KEY = $Keys.DeviceDetection

# A chromedriver on PATH is used even when it no longer matches the installed
# Chrome, and the images ship ones that lag behind. Skipping PATH makes
# Selenium Manager fetch a matching driver for the browser tests.
$env:SE_SKIP_DRIVER_IN_PATH = "true"
$env:SE_BROWSER_VERSION = "stable"

# The strong name key, which the assemblies are signed with on CI.
if ($StrongNameKeyBase64) {
    [IO.File]::WriteAllBytes("$RepoPath/51Degrees.snk", [Convert]::FromBase64String($StrongNameKeyBase64))
}
