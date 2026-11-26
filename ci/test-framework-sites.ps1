param (
    [Parameter(Mandatory)][string]$RepoName
)
# Hosts each .NET Framework example site with IIS Express, asks for its page
# with no resource key and the free data files, and checks that the page
# answers 200 and shows the text each site must show. A cloud site must say
# how to get a resource key rather than fail. The sites must already have
# been built with msbuild.
$ErrorActionPreference = "Stop"

$IisExpress = Join-Path $env:ProgramFiles "IIS Express/iisexpress.exe"
$FirstPort = 8181
$RequestTimeoutSeconds = 300
$UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
$NoKey = @(
    "_51DEGREES_RESOURCE_KEY",
    "51degrees.com/configure/",
    "Find out more")
# Each page's Find out more section carries a contact us link tagged for an
# example, and each on-premise page says what a paid data file adds when the
# file it loaded is a free one, which is the file this run gives it.
$FindOutMore = @(
    "Find out more",
    "51degrees.com/contact-us?utm_source=code&amp;utm_medium=example")
# The free data file each on-premise site is given, by the variable the site
# reads its data file path from.
$FreeFiles = [ordered]@{
    _51DEGREES_DD_PATH = "51Degrees-LiteV4.1.hash"
    _51DEGREES_IPI_PATH = "51Degrees-LiteV41.ipi"
}
$Sites = @(
    @{
        Path = "examples/device-detection/OnPremise/Framework-Web"
        Expect = @("Detection results", "The paid data file adds") + $FindOutMore
    },
    @{
        Path = "examples/ip-intelligence/OnPremise/Framework-Web"
        Expect = @(
            "Detection results",
            "Need more on-premise properties and features?") + $FindOutMore
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

# CI fetched the free data files into the data submodule of cxx, so the run
# says which file is missing rather than failing inside a site.
$data = Join-Path $RepoName "cxx/data"
$paths = @{}
foreach ($name in $FreeFiles.Keys) {
    $file = $FreeFiles[$name]
    $found = Get-ChildItem -Path $data -Recurse -File -Filter $file `
        -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $found) {
        Write-Host ("::error::$file is not under $data, so the " +
            "on-premise sites cannot run on the free data files")
        exit 1
    }
    Write-Host "Build log: $name is $($found.FullName)"
    $paths[$name] = $found.FullName
}

# Each on-premise site is given its free data file by path, so it never
# downloads one, and says what a paid file adds. The resource key is taken
# out, so each cloud site says how to get one. Every variable is put back
# after the sites.
$saved = @{}
foreach ($name in @('_51DEGREES_RESOURCE_KEY') + @($FreeFiles.Keys)) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}
Remove-Item env:_51DEGREES_RESOURCE_KEY -ErrorAction SilentlyContinue
foreach ($name in $paths.Keys) {
    Set-Item "env:$name" $paths[$name]
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
        if ($null -eq $saved[$name]) {
            Remove-Item "env:$name" -ErrorAction SilentlyContinue
        } else {
            Set-Item "env:$name" $saved[$name]
        }
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
