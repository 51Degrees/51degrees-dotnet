param (
    [Parameter(Mandatory)][string]$RepoName,
    [string]$Name = "Release_x64",
    [string]$Configuration = "Release",
    [string]$Arch = "x64",
    [string]$BuildMethod = "dotnet",
    [string]$Version,
    [string]$TestResourceKey
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

. "$PSScriptRoot/packages.ps1"

# The example tests. On a pull request they ran against the projects, built
# by build-project.ps1. On a publish, install-package.ps1 has rebuilt the
# examples against the packages about to be published, which is what a
# customer installs.
$Filter = "^FiftyOne\..*Examples?\.Tests.*\.dll$"
./dotnet/run-integration-tests.ps1 `
    -RepoName $RepoName `
    -Name $Name `
    -Configuration $Configuration `
    -Arch $Arch `
    -BuildMethod $BuildMethod `
    -Filter $Filter
if ($LASTEXITCODE -ne 0) {
    throw "The example tests failed."
}

# The shared Selenium contract tests, run against the device detection web
# examples. Lite data has neither the DeviceType property nor the JavaScript
# properties the contract needs, so on-premise runs only with the TAC file.
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

$references = $Version ? @() : @("-p:FiftyOneReferences=Project")
$failed = @()
$cloudProject = "$RepoName/examples/device-detection/Cloud/GettingStarted-Web"
$onPremiseProject = "$RepoName/examples/device-detection/OnPremise/GettingStarted-Web"
try {
    Invoke-ContractTests -Label 'cloud' -Project $cloudProject -Port 8095 `
        -BuildArgs (@('-c', 'Release') + $references) `
        -ExampleEnv @{ FIFTYONE_CLOUD_ENDPOINT = "https://cloud.51degrees.com/api/v4/" }
} catch {
    Write-Host "::error title=Selenium contract (cloud)::$_"
    $failed += 'cloud'
}
$tacFile = "$PWD/$RepoName/cxx/device-detection-data/TAC-HashV41.hash"
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
