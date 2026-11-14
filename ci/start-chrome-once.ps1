# Starts Chrome and its driver once, waits for them to finish and prints how
# long each took. The first start of Chrome on a fresh runner takes many times
# longer than the starts after it, so the browser tests are run after this
# and that cost falls outside the limits of their tests. A start that
# takes longer than the limit is stopped, and one that exits with an error is
# reported with its error output, both as warnings, and the tests then run as
# they would have without it.
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
