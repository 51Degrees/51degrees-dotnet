param(
    [Parameter(Mandatory)][string]$DDLicenseKey,
    [Parameter(Mandatory)][string]$IPIFileURL,
    [switch]$Clean
)

$JsonFileName = "$PSScriptRoot/appsettings.json"

if ($Clean) {
    git checkout HEAD -- $JsonFileName
}

$ConfigData = Get-Content $JsonFileName | ConvertFrom-Json

foreach ($element in $ConfigData.PipelineOptions.Elements) {
    if ($element.PSObject.Properties.Name -contains "BuildParameters") {
        if ($element.BuilderName.StartsWith("DeviceDetectionHashEngine")) {
            $element.BuildParameters.DataUpdateLicenseKey = $DDLicenseKey
        }
        if ($element.BuilderName.StartsWith("IpiOnPremiseEngine")) {
            $element.BuildParameters.DataUpdateURL = $IPIFileURL
        }
    }
}

$ConfigData | ConvertTo-Json -Depth 10 | Set-Content $JsonFileName
