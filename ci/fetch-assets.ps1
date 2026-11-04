param (
    [string]$DeviceDetection,
    [string]$DeviceDetectionUrl,
    [string]$IpIntelligenceUrl
)
$ErrorActionPreference = "Stop"

# PreBuild only compiles the native libraries, which need no data.
if ($env:GITHUB_JOB -eq "PreBuild") {
    Write-Host "Skipping assets fetching"
    exit 0
}

# The tests and examples find the data files in the data submodule of cxx.
$data = "$PSScriptRoot/../cxx/data"
$deviceDetectionData = "$data/device-detection"
$ipIntelligenceData = "$data/ip-intelligence"

$assets = @("51Degrees-LiteV4.1.hash", "20000 Evidence Records.yml", "20000 User Agents.csv")
if ($DeviceDetection) {
    $assets += "TAC-HashV41.hash"
} else {
    Write-Host "::warning title=No License Key::No device detection license, so the TAC data file is not downloaded and the tests that need it are skipped."
}
./steps/fetch-assets.ps1 -DeviceDetection:$DeviceDetection -DeviceDetectionUrl:$DeviceDetectionUrl -Assets $assets
foreach ($asset in $assets) {
    Copy-Item "assets/$asset" $deviceDetectionData
}

if ($IpIntelligenceUrl) {
    $asset = "51Degrees-EnterpriseIpiV41.ipi"
    ./steps/fetch-assets.ps1 -IpIntelligenceUrl:$IpIntelligenceUrl -Assets $asset
    New-Item -ItemType SymbolicLink -Force -Target "$PWD/assets/$asset" -Path "$ipIntelligenceData/$asset"
    # The mixed examples read the device detection file from the same folder.
    if ($DeviceDetection) {
        Copy-Item "assets/TAC-HashV41.hash" "$ipIntelligenceData/51Degrees-EnterpriseV41.hash"
    }
} else {
    Write-Host "::warning title=No License Key::No IP Intelligence data file URL, so the Enterprise file is not downloaded and the tests that need it are skipped."
}

# The free files the tests and examples read. The data repository's script
# keeps the ones copied in above and downloads the rest, which include the
# Asn file the IP Intelligence tests use.
pwsh -NoProfile -File "$data/get-data-files.ps1" "51Degrees-LiteV4.1.hash" "20000 Evidence Records.yml" "20000 User Agents.csv" "51Degrees-IPIV4AsnIpiV41.ipi"
if ($LASTEXITCODE -ne 0) {
    throw "The data repository's script did not download every free data file."
}

# The evidence files the IP Intelligence tests and examples read.
Push-Location $ipIntelligenceData
try {
    ./evidence-gen.ps1 -v4 10000 -v6 10000
    ./evidence-gen.ps1 -v4 10000 -v6 10000 -csv -path "evidence.csv"
} finally {
    Pop-Location
}
