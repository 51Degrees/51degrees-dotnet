param (
    [Parameter(Mandatory)][string]$RepoName
)
# Hosts each .NET Framework example site with IIS Express, asks for its page
# with no settings, as someone would after a fresh clone, and checks that the
# page answers 200 and shows the text each site must show. A cloud site has
# no resource key here, so it must say how to get one rather than fail. The
# sites must already have been built with msbuild.
$ErrorActionPreference = "Stop"

$IisExpress = Join-Path $env:ProgramFiles "IIS Express/iisexpress.exe"
$FirstPort = 8181
$RequestTimeoutSeconds = 300
$UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
$NoKey = @(
    "_51DEGREES_RESOURCE_KEY",
    "https://51degrees.com/configure/",
    "Find out more")
# Each page's Find out more section carries a contact us link tagged for an
# example. The message about what a paid data file adds is shown only with a
# free data file, which this run cannot count on, because the build gives the
# examples the paid files, so it is not expected here.
$FindOutMore = @(
    "Find out more",
    "51degrees.com/contact-us?utm_source=code&amp;utm_medium=example")
$Sites = @(
    @{
        Path = "examples/device-detection/OnPremise/Framework-Web"
        Expect = @("Detection results") + $FindOutMore
    },
    @{
        Path = "examples/ip-intelligence/OnPremise/Framework-Web"
        Expect = @("Detection results") + $FindOutMore
    },
    @{
        Path = "examples/device-detection/Cloud/Framework-Web"
        Expect = $NoKey
    },
    @{
        Path = "examples/ip-intelligence/Cloud/Framework-Web"
        Expect = $NoKey
    }
)

# IIS Express takes a moment to start listening, so the page is asked for
# once the port accepts a connection, rather than retried.
$StartTimeoutSeconds = 60
function Wait-ForPort([int]$Port) {
    $deadline = (Get-Date).AddSeconds($StartTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $client = [Net.Sockets.TcpClient]::new()
        try {
            $client.Connect("localhost", $Port)
            return
        } catch {
            Start-Sleep -Milliseconds 250
        } finally {
            $client.Dispose()
        }
    }
    throw "IIS Express did not listen on port $Port within $StartTimeoutSeconds seconds"
}

if (-not (Test-Path $IisExpress)) {
    Write-Host "::error::IIS Express was not found at $IisExpress"
    exit 1
}

# No settings means no resource key either, so every variable a cloud
# example could read one from is removed for the sites and put back after.
$saved = @{}
foreach ($variable in Get-ChildItem env: |
    Where-Object { $_.Name -like '*RESOURCE_KEY*' }) {
    $saved[$variable.Name] = $variable.Value
    Remove-Item "env:$($variable.Name)"
}

$failed = @()
$port = $FirstPort
try {
    foreach ($site in $Sites) {
        $path = (Resolve-Path (Join-Path $RepoName $site.Path)).Path
        $output = Join-Path ([IO.Path]::GetTempPath()) "iisexpress-$port.txt"
        Write-Host "::group::$($site.Path) on port $port"
        $server = Start-Process $IisExpress `
            -ArgumentList "/path:$path", "/port:$port", "/clr:v4.0" `
            -PassThru -NoNewWindow -RedirectStandardOutput $output
        $problems = @()
        $watch = [Diagnostics.Stopwatch]::StartNew()
        try {
            Wait-ForPort $port
            $response = Invoke-WebRequest "http://localhost:$port/" `
                -SkipHttpErrorCheck `
                -TimeoutSec $RequestTimeoutSeconds `
                -Headers @{ "User-Agent" = $UserAgent }
            if ($response.StatusCode -ne 200) {
                $problems += "answered $($response.StatusCode)"
            }
            foreach ($text in $site.Expect) {
                if (-not $response.Content.Contains($text)) {
                    $problems += "does not show '$text'"
                }
            }
            Write-Host ("Build log: $($site.Path) answered " +
                "$($response.StatusCode) in $($watch.ElapsedMilliseconds) ms.")
        } catch {
            $problems += "could not be asked for its page: $_"
        } finally {
            Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
        }
        if ($problems) {
            $failed += "$($site.Path) $($problems -join ', ')"
            if ($response) {
                $response.Content -replace '<[^>]+>', ' ' -replace '\s+', ' '
            }
            Get-Content $output -ErrorAction SilentlyContinue
        }
        Write-Host "::endgroup::"
        $response = $null
        $port++
    }
} finally {
    foreach ($name in $saved.Keys) {
        Set-Item "env:$name" $saved[$name]
    }
}

if ($failed) {
    foreach ($failure in $failed) {
        Write-Host "::error::$failure"
    }
    exit 1
}
Write-Host "All $($Sites.Count) .NET Framework example sites answered as expected."
exit 0
