param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$MetaDataPath,
    [string]$DataType = "HashV41"
)
$ErrorActionPreference = "Stop"

# Regenerates the property accessors of a product from the data file
# metadata, with the shared tools repository, when a data file changes. The
# data type says which product.
$products = @{
    "HashV41" = @{ Interface = "IDeviceData.cs"; Base = "DeviceDataBase.cs"; Shared = "device-detection/Shared" }
    "IpIntelligence" = @{ Interface = "IIpIntelligenceData.cs"; Base = "IpIntelligenceDataBase.cs"; Shared = "ip-intelligence/Shared" }
}
if (-not $products.ContainsKey($DataType)) {
    throw "No product generates accessors for the data type '$DataType'"
}
$product = $products[$DataType]

./tools/ci/generate-accessors.ps1 -RepoName:$RepoName -MetaDataPath:$MetaDataPath -DataType:$DataType

Copy-Item "tools/CSharp/$($product.Interface)" "$PSScriptRoot/../$($product.Shared)/Data/"
Copy-Item "tools/CSharp/$($product.Base)" "$PSScriptRoot/../$($product.Shared)/"
