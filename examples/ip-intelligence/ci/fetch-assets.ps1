param (
    [string]$DeviceDetection,
    [string]$IpIntelligenceUrl,
    [switch]$SkipEvidence,
    [string[]]$Assets = @("TAC-HashV41.hash", "51Degrees-EnterpriseIpiV41.ipi")
)
$ErrorActionPreference = "Stop"

$ipIntelligenceData = "$PSScriptRoot/../ip-intelligence-data"

./steps/fetch-assets.ps1 -DeviceDetection:$DeviceDetection -IpIntelligenceUrl:$IpIntelligenceUrl -Assets $Assets

Write-Host "Assets hashes:"
Get-FileHash -Algorithm MD5 -Path assets/*

if ("TAC-HashV41.hash" -in $Assets) {
    Copy-Item "assets/TAC-HashV41.hash" "$ipIntelligenceData/51Degrees-EnterpriseV41.hash"
}
if ("51Degrees-EnterpriseIpiV41.ipi" -in $Assets) {
    Copy-Item "assets/51Degrees-EnterpriseIpiV41.ipi" $ipIntelligenceData
}
if (!$SkipEvidence) {
    Push-Location $ipIntelligenceData
    try {
        ./evidence-gen.ps1 -v4 10000 -v6 10000
    } finally {
        Pop-Location
    }
}
