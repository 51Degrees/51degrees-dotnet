param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet",
    [string]$Version,
    [string]$TestResourceKey,
    # How deeply a pull request is tested, as select-options.ps1 chose.
    # standard leaves out the IP Intelligence on-premise example tests, which
    # load the Enterprise data file. Empty, for the branch itself or a
    # release, runs them all.
    [string]$Depth,
    # False, from select-options.ps1, unless the pull request changes the web
    # integration or the web examples.
    [bool]$WebExamples = $true,
    # False, from select-options.ps1, on every entry of a pull request but
    # one, so the browser contract runs once.
    [bool]$SeleniumContract = $true
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

. "$PSScriptRoot/packages.ps1"

# The example tests. On a pull request they ran against the projects, built
# by build-project.ps1. On a publish, install-package.ps1 has rebuilt the
# examples against the packages about to be published, which is what a
# customer installs.
# FiftyOne.DeviceDetection.Example.Tests.Web is left out. It holds the generic
# base classes the web example tests derive from, and has no tests of its own.
$Filter = "^FiftyOne\.(?!DeviceDetection\.Example\.Tests\.Web\.dll$).*Examples?\.Tests.*\.dll$"
# A run can be narrowed to some assemblies by setting FIFTYONE_TEST_FILTER,
# which the probe workflow does.
if ($env:FIFTYONE_TEST_FILTER) {
    $Filter = $env:FIFTYONE_TEST_FILTER
}
# A test is treated as hung after 20 minutes, the limit the separate examples
# repository allowed, because the IP Intelligence examples load the 6 GB
# Enterprise file.
# Every example test project is SDK-style, so dotnet test runs them on every
# platform. The host is picked from the assembly's platform, which the
# on-premise example test projects set, so an x86 build loads its x86 library.
# They run in parallel lanes, which ci/run-test-lanes.ps1 describes, less
# those a pull request's depth leaves out.
$Exclude = @(
    if ($Depth -eq 'standard') { '^FiftyOne\.IpIntelligence\.Example\.Tests\.OnPremise\.dll$' }
    if (-not $WebExamples) { '^FiftyOne\.DeviceDetection\.Example\.Tests\.Web\.' }
) -join '|'
& "$PSScriptRoot/run-test-lanes.ps1" `
    -RepoName $RepoName `
    -Name $Name `
    -Configuration $Configuration `
    -BlameHangTimeout "20m" `
    -Filter $Filter `
    -Exclude $Exclude `
    -OutputFolder "integration"
if ($LASTEXITCODE -ne 0) {
    throw "The example tests failed."
}

# The shared Selenium contract tests, run against the device detection web
# examples. Lite data has neither the DeviceType property nor the JavaScript
# properties the contract needs, so on-premise runs only with the TAC file.
if (-not $SeleniumContract) {
    Write-Host "Another entry runs the Selenium contract for this pull request."
    return
}
if ($IsLinux -and $Arch -eq 'arm64') {
    Write-Host "::warning title=Selenium skipped::No selenium-manager build for linux-arm64. The contract runs on x64 and macOS."
    return
}
if (-not $TestResourceKey) {
    Write-Host "::warning title=No Resource Key::No resource key, so the Selenium contract is not run."
    return
}
if (-not (Test-Path selenium-api-tests)) {
    git clone --depth 1 https://github.com/51Degrees/selenium-api-tests.git
}

function Invoke-ContractTests {
    param (
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][int]$Port,
        [string[]]$BuildArgs = @(),
        [hashtable]$ExampleEnv = @{}
    )
    Write-Host "::group::Selenium contract against the $Label example on port $Port"
    $example = $null
    try {
        dotnet build $Project @BuildArgs
        foreach ($variable in $ExampleEnv.Keys) {
            Set-Item -Path "Env:$variable" -Value $ExampleEnv[$variable]
        }
        $env:PORT = $Port
        $env:ASPNETCORE_URLS = "http://localhost:$Port"
        $example = dotnet run --no-build --project $Project @BuildArgs --no-launch-profile 2>&1 &
        curl -sS -o $(if ($IsWindows) { 'NUL' } else { '/dev/null' }) --retry 60 --retry-delay 2 --retry-connrefused "http://localhost:$Port"
        $env:CLOUD_ROOT_URL = "https://cloud.51degrees.com/"
        $env:PAID_RESOURCE_KEY = $TestResourceKey
        $env:EXAMPLE_URL = "http://localhost:$Port"
        $env:EXAMPLE_LANG = 'dotnet'
        dotnet test selenium-api-tests -c Release --filter TestCategory=Contract
    } catch {
        if ($example) { Write-Host ">>> $Label example output >>>"; Receive-Job $example | Out-Host; Write-Host "<<< example output <<<" }
        throw
    } finally {
        if ($example) { Remove-Job -Force $example }
        Remove-Item Env:ASPNETCORE_URLS, Env:PORT, Env:EXAMPLE_URL -ErrorAction SilentlyContinue
        foreach ($variable in $ExampleEnv.Keys) {
            Remove-Item -Path "Env:$variable" -ErrorAction SilentlyContinue
        }
        Write-Host "::endgroup::"
    }
}

# The examples build in the job's own configuration, which build-project.ps1
# has already built, so nothing is rebuilt here. Building the cloud example
# as Release on a Debug job rebuilt the pipeline projects in Release while
# an MSBuild node from the earlier build still held their output.
$references = $Version ? @() : @("-p:FiftyOneReferences=Project")
$failed = @()
$cloudProject = "$RepoName/examples/device-detection/Cloud/GettingStarted-Web"
$onPremiseProject = "$RepoName/examples/device-detection/OnPremise/GettingStarted-Web"
try {
    Invoke-ContractTests -Label 'cloud' -Project $cloudProject -Port 8095 `
        -BuildArgs (@('-c', $Configuration) + $references) `
        -ExampleEnv @{ FIFTYONE_CLOUD_ENDPOINT = "https://cloud.51degrees.com/api/v4/" }
} catch {
    Write-Host "::error title=Selenium contract (cloud)::$_"
    $failed += 'cloud'
}
$tacFile = "$PWD/$RepoName/cxx/data/device-detection/TAC-HashV41.hash"
if ($Arch -eq 'x86') {
    Write-Host "::warning title=Selenium on-premise skipped::The native library on this job is x86, so the example would have to run as an x86 process."
} elseif (-not (Test-Path $tacFile)) {
    Write-Host "::warning title=Selenium on-premise skipped::No TAC data file, which needs the device detection licence."
} else {
    try {
        Invoke-ContractTests -Label 'on-premise' -Project $onPremiseProject -Port 8096 `
            -BuildArgs (@('-c', $Configuration, "-p:Platform=$Arch") + $references) `
            -ExampleEnv @{ '51DEGREES_DD_PATH' = (Resolve-Path $tacFile).Path }
    } catch {
        Write-Host "::error title=Selenium contract (on-premise)::$_"
        $failed += 'on-premise'
    }
}
Remove-Item Env:CLOUD_ROOT_URL, Env:PAID_RESOURCE_KEY, Env:EXAMPLE_LANG -ErrorAction SilentlyContinue
if ($failed) {
    throw "The Selenium contract failed against the $($failed -join ' and ') example."
}
