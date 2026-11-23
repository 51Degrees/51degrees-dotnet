# The data files that need a licence come from the Distributor with
# _51DEGREES_LICENSE_KEY, which the shared fetch-assets step reads itself.
# It fails naming the variable when the key is not set.
param ()
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

$assets = @("51Degrees-LiteV4.1.hash", "20000 Evidence Records.yml", "TAC-HashV41.hash")
./steps/fetch-assets.ps1 -Assets $assets
foreach ($asset in $assets) {
    Copy-Item "assets/$asset" $deviceDetectionData
}

$asset = "51Degrees-EnterpriseIpiV41.ipi"
./steps/fetch-assets.ps1 -Assets $asset
New-Item -ItemType SymbolicLink -Force -Target "$PWD/assets/$asset" -Path "$ipIntelligenceData/$asset"

# The other free files the tests and examples read, which are the Asn file the
# IP Intelligence tests use and the Lite file that the IP Intelligence examples
# use when they are run with no settings. They are assets too, so a test job
# restores them from the run's cache rather than downloading them.
$asn = "51Degrees-IPIV4AsnIpiV41.ipi"
$lite = "51Degrees-LiteIpiV41.ipi"
./steps/fetch-assets.ps1 -Assets $asn, $lite
Copy-Item "assets/$asn" $ipIntelligenceData
# The asset is fetched under its own name, and the examples look for the name
# the data repository saves it as.
New-Item -ItemType SymbolicLink -Force -Target "$PWD/assets/$lite" -Path "$ipIntelligenceData/51Degrees-LiteV41.ipi"

# The evidence file the IP Intelligence tests and examples read.
Push-Location $ipIntelligenceData
try {
    ./evidence-gen.ps1 -v4 10000 -v6 10000
} finally {
    Pop-Location
}
