param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$Version
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$RepoPath = [IO.Path]::Combine($pwd, $RepoName)
$summary = New-Item -ItemType Directory -Force "$RepoPath/test-results/performance-summary"
$references = $Version ? @() : @("-p:FiftyOneReferences=Project")
$runConfiguration = $Configuration.Contains("Release") ? "Release" : "Debug"

# The device detection console example, with the TAC file and the evidence
# records. Its result is the one the shared performance comparison reads.
$tacFile = "$RepoPath/cxx/device-detection-data/TAC-HashV41.hash"
$evidence = "$RepoPath/cxx/device-detection-data/20000 Evidence Records.yml"
if (Test-Path $tacFile) {
    Write-Host "::group::Device detection console performance"
    $project = "$RepoPath/examples/device-detection/OnPremise/Performance-Console"
    dotnet build $project -c $runConfiguration "-p:Platform=$Arch" "-p:OutDir=output" "-p:BuiltOnCI=true" @references
    Push-Location "$project/output"
    try {
        dotnet FiftyOne.DeviceDetection.Examples.OnPremise.Performance.dll -d $tacFile -u $evidence -j summary.json
        $results = Get-Content summary.json | ConvertFrom-Json
        ConvertTo-Json @{
            'HigherIsBetter' = @{ 'DetectionsPerSecond' = $results.MaxPerformance.DetectionsPerSecond }
            'LowerIsBetter' = @{ 'MsPerDetection' = $results.MaxPerformance.MsPerDetection }
        } | Set-Content "$summary/results_$Name.json"
    } finally {
        Pop-Location
    }
    Write-Host "::endgroup::"
} else {
    Write-Host "::warning title=No TAC data file::The device detection performance example needs the TAC file, so it was not run."
}

# The IP Intelligence console example, with the Enterprise file.
$ipiFile = "$RepoPath/cxx/ip-intelligence-data/51Degrees-EnterpriseIpiV41.ipi"
$ipiEvidence = "$RepoPath/cxx/ip-intelligence-data/evidence.yml"
if (Test-Path $ipiFile) {
    Write-Host "::group::IP Intelligence console performance"
    $project = "$RepoPath/examples/ip-intelligence/OnPremise/Performance-Console"
    dotnet build $project -c $runConfiguration "-p:Platform=$Arch" "-p:OutDir=output" "-p:BuiltOnCI=true" @references
    Push-Location "$project/output"
    try {
        dotnet FiftyOne.IpIntelligence.Examples.OnPremise.Performance.dll -d $ipiFile -a $ipiEvidence -j summary.json
        $results = Get-Content summary.json | ConvertFrom-Json
        ConvertTo-Json @{
            'HigherIsBetter' = @{ 'DetectionsPerSecond' = $results.MaxPerformance.DetectionsPerSecond }
            'LowerIsBetter' = @{ 'MsPerDetection' = $results.MaxPerformance.MsPerDetection }
        } | Set-Content "$summary/results_${Name}_ipi.json"
    } finally {
        Pop-Location
    }
    Write-Host "::endgroup::"
} else {
    Write-Host "::warning title=No IP Intelligence data file::The IP Intelligence performance example needs the Enterprise file, so it was not run."
}

# The web overhead of the pipeline, measured with the shared httpbench script
# against the device detection performance site. More than 200 ms a request
# fails the run.
if ($env:SKIP_WEB_PERFORMANCE -eq 'true') {
    Write-Host "Skipping the web performance test (SKIP_WEB_PERFORMANCE)."
    return
}
Write-Host "::group::Web performance"
$webProject = "$RepoPath/device-detection/performance-tests"
$env:PipelineOptions__Elements__0__BuildParameters__DataFile = $env:_51DEGREES_DD_PATH
dotnet build $webProject -c $Configuration "-p:Platform=$Arch" @references
try {
    $server = dotnet "$webProject/bin/$Arch/$Configuration/net8.0/performance_tests.dll" &
    $results = ./scripts/httpbench.ps1 -HostPort '127.0.0.1:5000' -Endpoint '/process' -CalibrateEndpoint '/calibrate' -UaFile 'assets/20000 User Agents.csv'
} finally {
    Receive-Job $server
    Stop-Job $server
}
if ($results.overhead_ms -gt 200) {
    throw "Unacceptable request overhead: $($results.overhead_ms) ms"
} elseif ($results.overhead_ms -lt 0) {
    throw "The request overhead cannot be negative: $($results.overhead_ms) ms"
}
ConvertTo-Json @{
    'HigherIsBetter' = @{ 'DetectionsPerSecond' = 1 / ($results.overhead_ms / 1000) }
    'LowerIsBetter' = @{ 'MsPerDetection' = $results.overhead_ms }
} | Set-Content "$summary/results_${Name}_web.json"
Write-Host "::endgroup::"
