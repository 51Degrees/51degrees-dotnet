# Starts Chrome and its driver once, waits for them to finish and prints how
# long each took. The first start of Chrome on a fresh runner takes many times
# longer than the starts after it, so the browser tests are run after this
# and that cost falls outside the limits of their tests. A start that
# takes longer than the limit is stopped, and one that exits with an error is
# reported with its error output, both as warnings, and the tests then run as
# they would have without it.
#
# Starting Chrome directly does not take away all of the first test's extra
# time, so it then also runs one session through the driver, as each test
# does: the driver started and ready, a headless session, a blank page and the
# session ended, each part timed.
param(
    # The longest each first start is given.
    [int]$LimitSeconds = 300
)
$ErrorActionPreference = "Stop"

# The first of the names in a folder on the path, taking each folder in turn.
function Find-OnPath {
    param([string[]]$Names)
    foreach ($folder in ($env:PATH -split [System.IO.Path]::PathSeparator)) {
        if (-not $folder) {
            continue
        }
        foreach ($name in $Names) {
            $candidate = Join-Path $folder $name
            if (Test-Path $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }
    return $null
}

# The browser to start, being the file CHROME_BIN names, then the first
# browser on the path, then the place Chrome installs itself, which is where
# the driver looks when a test names no browser.
function Find-Chrome {
    if ($env:CHROME_BIN -and (Test-Path $env:CHROME_BIN -PathType Leaf)) {
        return $env:CHROME_BIN
    }
    $onPath = Find-OnPath 'google-chrome', 'google-chrome-stable', 'chromium-browser', 'chromium', 'chrome.exe'
    if ($onPath) {
        return $onPath
    }
    $installed = if ($IsMacOS) {
        '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
    } elseif ($IsWindows) {
        Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Google\Chrome\Application\chrome.exe'
    }
    if ($installed -and (Test-Path $installed -PathType Leaf)) {
        return $installed
    }
    return $null
}

# The driver to start, being the file CHROMEWEBDRIVER names or the driver in
# the folder it names, then the first driver on the path.
function Find-ChromeDriver {
    $names = 'chromedriver', 'chromedriver.exe'
    $configured = $env:CHROMEWEBDRIVER
    if ($configured) {
        if (Test-Path $configured -PathType Leaf) {
            return $configured
        }
        foreach ($name in $names) {
            $candidate = Join-Path $configured $name
            if (Test-Path $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }
    return Find-OnPath $names
}

# Runs a program, stopping it after the limit. Gives the seconds it took, its
# exit code, or -1 when it was stopped, and the start of its error output.
function Measure-Start {
    param([string]$FilePath, [string[]]$Arguments)
    $output = [System.IO.Path]::GetTempFileName()
    $errors = [System.IO.Path]::GetTempFileName()
    try {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -PassThru -NoNewWindow `
            -RedirectStandardOutput $output -RedirectStandardError $errors
        # Reading the handle now keeps the exit code readable after the exit.
        $null = $process.Handle
        $exitCode = -1
        if ($process.WaitForExit($LimitSeconds * 1000)) {
            $process.WaitForExit()
            $exitCode = $process.ExitCode
        } else {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        return [pscustomobject]@{
            Seconds  = [math]::Round($watch.Elapsed.TotalSeconds, 1)
            ExitCode = $exitCode
            Errors   = ((Get-Content $errors -TotalCount 5 -ErrorAction SilentlyContinue) -join ' ')
        }
    } finally {
        Remove-Item $output, $errors -Force -ErrorAction SilentlyContinue
    }
}

# Prints the result of one start, as a warning unless it exited cleanly.
function Write-Start {
    param([string]$What, $Result)
    if ($Result.ExitCode -eq 0) {
        Write-Host "Started $What once before the browser tests in $($Result.Seconds) s"
    } elseif ($Result.ExitCode -eq -1) {
        Write-Host "::warning title=Chrome slow to start::$What did not finish its first start within $LimitSeconds s, so it was stopped."
    } else {
        Write-Host "::warning title=Chrome did not start::$What exited with code $($Result.ExitCode) after $($Result.Seconds) s. $($Result.Errors)"
    }
}

$driver = Find-ChromeDriver
if ($driver) {
    Write-Start -What "the Chrome driver $driver" -Result (Measure-Start -FilePath $driver -Arguments '--version')
}

$chrome = Find-Chrome
if (-not $chrome) {
    Write-Host "::warning title=Chrome not started::No Chrome was found to start before the browser tests."
    return
}
$userDataDir = Join-Path ([System.IO.Path]::GetTempPath()) "chrome-first-start-$PID"
try {
    Write-Start -What "Chrome $chrome" -Result (Measure-Start -FilePath $chrome -Arguments @(
        '--headless', '--disable-gpu', '--no-first-run', '--use-mock-keychain',
        "--user-data-dir=$userDataDir", '--dump-dom', 'about:blank'))
} finally {
    Remove-Item -Recurse -Force $userDataDir -ErrorAction SilentlyContinue
}

# The browser to name to the driver, being the file CHROME_BIN names, then the
# first browser on the path. With neither, none is named and the driver finds
# Chrome itself.
function Find-ChromeBinary {
    if ($env:CHROME_BIN -and (Test-Path $env:CHROME_BIN -PathType Leaf)) {
        return $env:CHROME_BIN
    }
    return Find-OnPath 'google-chrome', 'google-chrome-stable', 'chromium-browser', 'chromium', 'chrome.exe'
}

# A free port on the loopback address for the driver to listen on.
function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return $listener.LocalEndpoint.Port
    } finally {
        $listener.Stop()
    }
}

# Sends one WebDriver command and gives the seconds it took and its value.
function Invoke-DriverCommand {
    param([string]$Method, [string]$Uri, $Body)
    $arguments = @{ Method = $Method; Uri = $Uri; TimeoutSec = $LimitSeconds }
    if ($null -ne $Body) {
        $arguments.Body = ($Body | ConvertTo-Json -Depth 6 -Compress)
        $arguments.ContentType = 'application/json'
    }
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-RestMethod @arguments
    return [pscustomobject]@{
        Seconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)
        Value   = $response.value
    }
}

# Starts the driver, waits until it says it is ready, opens a session with
# the arguments the browser tests give Chrome, loads a blank page, ends the
# session and stops the driver, printing how long each part took. A part that
# fails is a warning, and the driver and the browser's profile are always
# cleaned up.
function Measure-DriverSession {
    param([string]$Driver, [string]$Binary)
    $base = "http://127.0.0.1:$(Get-FreePort)"
    $userDataDir = Join-Path ([System.IO.Path]::GetTempPath()) "chrome-driver-session-$PID"
    $output = [System.IO.Path]::GetTempFileName()
    $errors = [System.IO.Path]::GetTempFileName()
    $process = $null
    $session = $null
    try {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $process = Start-Process -FilePath $Driver -ArgumentList "--port=$(([uri]$base).Port)" `
            -PassThru -NoNewWindow -RedirectStandardOutput $output -RedirectStandardError $errors
        $ready = $false
        while (-not $ready -and -not $process.HasExited -and
            $watch.Elapsed.TotalSeconds -lt $LimitSeconds) {
            try {
                $ready = [bool](Invoke-RestMethod -Uri "$base/status" -TimeoutSec 5).value.ready
            } catch {
                Start-Sleep -Milliseconds 250
            }
        }
        $readySeconds = [math]::Round($watch.Elapsed.TotalSeconds, 1)
        if (-not $ready) {
            Write-Host "::warning title=Chrome driver not ready::The Chrome driver $Driver was not ready after $readySeconds s. $((Get-Content $errors -TotalCount 5 -ErrorAction SilentlyContinue) -join ' ')"
            return
        }
        $chromeOptions = @{
            args = @('--headless', '--no-sandbox', '--disable-dev-shm-usage',
                '--disable-gpu', "--user-data-dir=$userDataDir")
        }
        if ($Binary) {
            $chromeOptions.binary = $Binary
        }
        $created = Invoke-DriverCommand -Method Post -Uri "$base/session" -Body @{
            capabilities = @{
                alwaysMatch = @{
                    browserName           = 'chrome'
                    acceptInsecureCerts   = $true
                    'goog:chromeOptions'  = $chromeOptions
                }
            }
        }
        $session = $created.Value.sessionId
        $page = Invoke-DriverCommand -Method Post -Uri "$base/session/$session/url" -Body @{ url = 'about:blank' }
        $ended = Invoke-DriverCommand -Method Delete -Uri "$base/session/$session"
        $session = $null
        Write-Host ("Ran one Chrome driver session before the browser tests. " +
            "The driver was ready in $readySeconds s, the session started in $($created.Seconds) s, " +
            "a blank page loaded in $($page.Seconds) s and the session ended in $($ended.Seconds) s")
    } catch {
        Write-Host "::warning title=Chrome driver session failed::$($_.Exception.Message)"
    } finally {
        if ($session) {
            try {
                Invoke-RestMethod -Method Delete -Uri "$base/session/$session" -TimeoutSec 30 | Out-Null
            } catch {
                Write-Host "The session $session could not be ended: $($_.Exception.Message)"
            }
        }
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        Remove-Item $output, $errors -Force -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force $userDataDir -ErrorAction SilentlyContinue
    }
}

if ($driver) {
    Measure-DriverSession -Driver $driver -Binary (Find-ChromeBinary)
}
