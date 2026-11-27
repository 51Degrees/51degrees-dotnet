param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$Version
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

# Every warning is an error in the builds 51Degrees runs. Each CI script
# that builds sets this until the shared workflows set it in every job.
$env:_51DEGREES_WARNINGS_AS_ERRORS = "true"

. "$PSScriptRoot/packages.ps1"

$RepoPath = [IO.Path]::Combine($pwd, $RepoName)
$summary = New-Item -ItemType Directory -Force "$RepoPath/test-results/performance-summary"
# On a publish the examples build against the packages install-package.ps1
# put in the local feed, at the versions being published. Without the
# version properties they would restore the last published versions from
# nuget.org instead.
$references = $Version ? (Get-VersionProperties -Versions (Get-PackageVersions -RepoName $RepoName)) : @("-p:_51DEGREES_REFERENCES=Project")
$runConfiguration = $Configuration.Contains("Release") ? "Release" : "Debug"
# The console examples are built one project at a time. They reach some
# projects through references with different settings, so those projects
# are built more than once, and with OutDir set every build of a project
# writes to the same output folder. Built in parallel, two builds can try to
# write the same file at once and the build fails.
$console = @("-p:OutDir=output", "-p:_51DEGREES_BUILT_ON_CI=true", "-m:1")

# The device detection console example, with the TAC file and the evidence
# records. Its result is the one the shared performance comparison reads.
$tacFile = "$RepoPath/cxx/data/device-detection/TAC-HashV41.hash"
$evidence = "$RepoPath/cxx/data/device-detection/20000 Evidence Records.yml"
if (Test-Path $tacFile) {
    Write-Host "::group::Device detection console performance"
    $project = "$RepoPath/examples/device-detection/OnPremise/Performance-Console"
    dotnet build $project -c $runConfiguration "-p:Platform=$Arch" @console @references
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
$ipiFile = "$RepoPath/cxx/data/ip-intelligence/51Degrees-EnterpriseIpiV41.ipi"
$ipiEvidence = "$RepoPath/cxx/data/ip-intelligence/evidence.yml"
if (Test-Path $ipiFile) {
    Write-Host "::group::IP Intelligence console performance"
    $project = "$RepoPath/examples/ip-intelligence/OnPremise/Performance-Console"
    dotnet build $project -c $runConfiguration "-p:Platform=$Arch" @console @references
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
# against the device detection performance site, which reads its data file
# from _51DEGREES_DD_PATH. httpbench sends the User-Agents of a file with one
# on each line, so those of the evidence records are written to one. More
# than 200 ms a request fails the run.
Write-Host "::group::Web performance"
$webProject = "$RepoPath/device-detection/performance-tests"
dotnet build $webProject -c $Configuration "-p:Platform=$Arch" @references
$userAgents = Join-Path ([IO.Path]::GetTempPath()) "user-agents-$([Guid]::NewGuid()).txt"
& "$PSScriptRoot/evidence-user-agents.ps1" -EvidencePath $evidence -OutputPath $userAgents
try {
    $server = dotnet "$webProject/bin/$Arch/$Configuration/net8.0/performance_tests.dll" &
    $results = ./scripts/httpbench.ps1 -HostPort '127.0.0.1:5000' -Endpoint '/process' -CalibrateEndpoint '/calibrate' -UaFile $userAgents
} finally {
    Receive-Job $server
    Stop-Job $server
    Remove-Item -LiteralPath $userAgents -ErrorAction SilentlyContinue
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
