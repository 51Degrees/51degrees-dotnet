[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    [string]$ProjectDir = ".",
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet",
    [string]$OutputFolder = "unit"
)

Write-Debug "env:IPINTELLIGENCEDATAFILE = <$($env:IPINTELLIGENCEDATAFILE)>"

$RunTestsArgs = @{
    RepoName = $RepoName
    ProjectDir = $ProjectDir
    Name = $Name
    Configuration = $Configuration
    Arch = $Arch
    BuildMethod = $BuildMethod
    OutputFolder = $OutputFolder
    Filter = ".*Tests(|\.OnPremise)(|\.Core)(|\.Cloud)\.dll"

    # vstest's blame collector aborts the whole assembly - and writes a multi-GB
    # hang dump - once a single test goes this long without a test-case event.
    # Its timer resets only on TestCaseStart/TestCaseEnd, so a legitimately long
    # test cannot hold it off by logging progress; console output does not touch
    # it.
    #
    # This is the only per-test ceiling that actually applies here. The
    # TestTimeout in Tests/test.runsettings does not: the filter above selects
    # built assemblies, so common-ci runs "dotnet test <assembly>.dll
    # --no-build", which never goes through MSBuild and so never turns the
    # projects <RunSettingsFilePath> into --settings. (common-ci has a fallback
    # that looks for a test.runsettings, but it probes the process directory
    # rather than the repository, and ours lives under Tests/ regardless. The
    # same gap is written up in device-detection-dotnet/ci/run-integration-tests.ps1.)
    # Below blame there is nothing, and above it only the job timeout.
    #
    # common-ci defaults this to 5m, which is exactly the budget
    # Example_OnPremise_MetricsConsole gives itself before cancelling. That test
    # always spends its budget in full, so every leg finished within a second of
    # the deadline and the two raced; the slower macOS runners lost it
    # consistently and took the nightly red.
    #
    # 20m is ~4x the longest test observed (MetricsConsole, 5m), leaving blame
    # as a genuine-hang backstop rather than something the suite trips in normal
    # operation. Example_OnPremise_CompareConsole's run time depends on the
    # data file, hence the extra margin. Sibling repo ip-intelligence-dotnet
    # raises it for the same reason. Raising the budget in TestExamples.cs
    # means revisiting this.
    BlameHangTimeout = "20m"
}

./dotnet/run-unit-tests.ps1 @RunTestsArgs

Write-Debug "---- DATA FILE PATH DUMPS BEGIN ----"
foreach ($file in (Get-ChildItem -Recurse -File -Include "*_DataFileName.txt")) {
    Write-Debug "[$($file.Name)]: <$(Get-Content -Path $file.FullName)>"
}
Write-Debug "---- DATA FILE PATH DUMPS END ----"

exit $LASTEXITCODE
