param (
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$Filter,
    [Parameter(Mandatory)][string]$OutputFolder,
    [string]$BlameHangTimeout = "20m"
)
$ErrorActionPreference = "Stop"

# Runs the test assemblies matching $Filter in parallel, in three lanes, where
# the shared common-ci step ran them one after another. Each assembly still
# gets its own 'dotnet test', with the same arguments and results folder as
# the shared step, so the steps that publish the results are unchanged.
#
#   ports    the web example tests, which bind the fixed ports 5001, 5002,
#            5101 and 5225, so only one of them can run at a time.
#   memory   the IP Intelligence tests that load the 6 GB Enterprise file.
#            Two at once would exhaust a 16 GB runner.
#   pool     everything else, as many at once as the runner has cores.
#
# The lanes run alongside each other. Each assembly's output is printed in
# one piece when it finishes, so the log reads as it did before.

$PortsLane = 'Example\.Tests\.Web|IpIntelligence\.Example\.Tests\.Cloud|GettingStartedAPI\.Tests'
$MemoryLane = '^FiftyOne\.IpIntelligence\.(OnPremise\.Tests|Tests\.Core|Example\.Tests\.OnPremise)\.dll$'

$RepoPath = [IO.Path]::Combine($pwd, $RepoName)
$ResultPath = [IO.Path]::Combine($RepoPath, "test-results", $OutputFolder, $Name)
$LogPath = [IO.Path]::Combine($RepoPath, "test-results", "logs", $OutputFolder, $Name)
New-Item -ItemType Directory -Force -Path $LogPath | Out-Null

# The same search as the shared step: any matching file in a bin folder, apart
# from the performance tests.
$assemblies = @(Get-ChildItem -Path $RepoPath -Recurse -File -Filter "*.dll" |
    Where-Object {
        $_.DirectoryName -like "*bin*" -and
        $_.Name -notlike "*performance*" -and
        $_.Name -match $Filter
    } |
    Sort-Object FullName)
if ($assemblies.Count -eq 0) {
    Write-Host "No test assemblies match '$Filter'"
    exit 0
}

$lanes = [ordered]@{ ports = @(); memory = @(); pool = @() }
foreach ($assembly in $assemblies) {
    if ($assembly.Name -match $PortsLane) { $lanes.ports += $assembly.FullName }
    elseif ($assembly.Name -match $MemoryLane) { $lanes.memory += $assembly.FullName }
    else { $lanes.pool += $assembly.FullName }
}
$poolSize = [Math]::Max(1, [Environment]::ProcessorCount - 1)
foreach ($lane in $lanes.Keys) {
    Write-Host "Lane '$lane': $($lanes[$lane].Count) assemblies"
}
Write-Host "The pool runs $poolSize at once"

# Runs one assembly and returns its name, exit code, time and log file. On
# macOS a test host killed within a second of starting is retried, as the
# shared step does.
$runOne = {
    param($Assembly, $Configuration, $ResultPath, $LogPath, $BlameHangTimeout)
    $log = [IO.Path]::Combine($LogPath, [IO.Path]::GetFileNameWithoutExtension($Assembly) + "-" +
        [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($Assembly)) + ".log")
    $attempt = 0
    do {
        $attempt++
        $sw = [Diagnostics.Stopwatch]::StartNew()
        & dotnet test $Assembly --no-build --configuration $Configuration `
            --results-directory $ResultPath `
            --blame-crash --blame-hang-timeout $BlameHangTimeout -l trx *> $log
        $code = $LASTEXITCODE
        $sw.Stop()
        $retry = $IsMacOS -and $code -eq 137 -and $sw.ElapsedMilliseconds -lt 1000 -and $attempt -le 2
    } while ($retry)
    [pscustomobject]@{ Assembly = $Assembly; ExitCode = $code; Seconds = [int]$sw.Elapsed.TotalSeconds; Log = $log }
}

$jobs = @()
foreach ($lane in $lanes.Keys) {
    $items = $lanes[$lane]
    if ($items.Count -eq 0) { continue }
    $throttle = $lane -eq 'pool' ? $poolSize : 1
    $jobs += Start-ThreadJob -Name $lane -ThrottleLimit 3 -ScriptBlock {
        param($Items, $Throttle, $RunOne, $Configuration, $ResultPath, $LogPath, $BlameHangTimeout)
        $Items | ForEach-Object -ThrottleLimit $Throttle -Parallel {
            $run = [scriptblock]::Create($using:RunOne)
            & $run $_ $using:Configuration $using:ResultPath $using:LogPath $using:BlameHangTimeout
        }
    } -ArgumentList $items, $throttle, $runOne.ToString(), $Configuration, $ResultPath, $LogPath, $BlameHangTimeout
}

# Print each assembly's output as it finishes.
$results = @()
$printed = @{}
while ($true) {
    foreach ($job in $jobs) {
        foreach ($result in @(Receive-Job $job)) {
            $results += $result
            $status = $result.ExitCode -eq 0 ? "passed" : "FAILED (exit $($result.ExitCode))"
            Write-Host "::group::$([IO.Path]::GetFileName($result.Assembly)) $status in $($result.Seconds)s ($($job.Name) lane)"
            Get-Content $result.Log | Write-Host
            Write-Host "::endgroup::"
        }
    }
    if (@($jobs | Where-Object { $_.State -in 'NotStarted', 'Running' }).Count -eq 0) {
        foreach ($job in $jobs) {
            foreach ($result in @(Receive-Job $job)) { $results += $result }
            if ($job.State -eq 'Failed') { throw "The $($job.Name) lane failed: $($job.ChildJobs[0].JobStateInfo.Reason)" }
        }
        break
    }
    Start-Sleep -Seconds 2
}
$jobs | Remove-Job

Write-Host "Test assemblies, slowest first:"
$results | Sort-Object Seconds -Descending | ForEach-Object {
    Write-Host ("  {0,5}s  {1}  {2}" -f $_.Seconds, ($_.ExitCode -eq 0 ? "passed" : "FAILED"), [IO.Path]::GetFileName($_.Assembly))
}
if ($results.Count -ne $assemblies.Count) {
    throw "Expected $($assemblies.Count) results but got $($results.Count)."
}
$failed = @($results | Where-Object { $_.ExitCode -ne 0 })
if ($failed.Count -gt 0) {
    Write-Host "::error::$($failed.Count) test assemblies failed."
    exit 1
}
exit 0
