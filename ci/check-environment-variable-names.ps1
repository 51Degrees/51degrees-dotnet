# Every 51Degrees environment variable name in this repository starts with an
# underscore, because a POSIX shell cannot set a name that starts with a digit,
# and each variable has one name with nothing read in its place. This reads
# every file git tracks, which leaves out build output and the submodules, and
# fails on any name that starts with the digit and on any old name that is
# still read.
#
# Dot-sourcing the script defines Find-EnvironmentNameProblem without reading
# the repository, which is how its tests use it.

[CmdletBinding()]
param(
    # The root of the checkout to read. The default is the repository this
    # script lives in.
    [string]$RepoRoot = (Join-Path $PSScriptRoot '..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The start of a name a shell cannot set, split so this file does not match
# itself. Only the 51DEGREES_ start is matched here, because 51D_ is also the
# prefix of the evidence, cookie and query names the pipeline reads, which are
# on the wire and not in the environment. The two 51D_ variables that did exist
# are named among the old names below.
$digitInitial = '5' + '1DEGREES_'

# A name is not matched when a name character comes before it, so a name with
# a leading underscore is left alone. It is matched in any case, because
# Windows reads an environment variable name in any case, so a lower case
# name that starts with the digit is still one a shell cannot set.
$script:DigitInitialPattern = [regex]::new(
    "(?<![A-Za-z0-9_])$digitInitial[A-Za-z0-9_]*",
    [Text.RegularExpressions.RegexOptions]::IgnoreCase)

# Two kinds of old name.
#
# The first are names that are never anything but an environment variable here,
# so they are matched wherever they appear. Each is built from two pieces so
# that this file does not name one itself. They keep their case, unlike the
# digit-initial pattern, because several differ from a class or property name
# by case alone, and a class is not a variable. They are gone from the
# repository, so only an exact return of one matters.
#
# The second are RESOURCE_KEY and LICENSE_KEY, which also name things that are
# not environment variables, so they are matched only where the line reads or
# sets a variable, or names one in a constant whose own name says so.
$plainOld = '(?-i:FOD_CLOUD' + '_API_URL|FIFTYONE_CLOUD' + '_ENDPOINT|' +
    'FIFTYONE_TEST' + '_FILTER|51D_CLOUD' + '_ENDPOINT|51D_RESOURCE' + '_KEY|' +
    'SUPER_RESOURCE' + '_KEY|RESOURCE_KEY_CLOUD' + '_V5_BESPOKE|' +
    'DEVICEDETECTION' + 'LICENSEKEY_DOTNET|IPINTELLIGENCE' + 'LICENSEKEY_DOTNET|' +
    'DEVICEDETECTION' + 'DATAFILE|IPINTELLIGENCE' + 'DATAFILE|' +
    'GEOIP' + 'EVIDENCEFILE|EVIDENCE' + 'FILE)'
$sharedOld = '(?:RESOURCE_KEY|LICENSE_KEY)'
$script:FallbackPattern = [regex]::new(
    "(?<![A-Za-z0-9_])$plainOld(?![A-Za-z0-9_])|" +
    '(?:GetEnvironmentVariable\(\s*"|SetEnvironmentVariable\(\s*"|' +
    'GetKeyFromEnv\(\s*"|\$env:|\$\{env:|Env:\\?|' +
    "[A-Za-z_][A-Za-z0-9_]*(?:ENV_VAR|ENV_VARIABLE|ENVIRONMENT_VARIABLE)" +
    "[A-Za-z0-9_]*\s*=\s*`")$sharedOld(?![A-Za-z0-9_])|" +
    '\bLEGACY_[A-Z_]*ENV_VARS?\b',
    [Text.RegularExpressions.RegexOptions]::IgnoreCase)

# The problems on one line, each as its kind, 'digit' or 'fallback', and the
# text matched.
function Find-EnvironmentNameProblem([string]$Line) {
    foreach ($match in $script:DigitInitialPattern.Matches($Line)) {
        [pscustomobject]@{ Kind = 'digit'; Value = $match.Value }
    }
    foreach ($match in $script:FallbackPattern.Matches($Line)) {
        [pscustomobject]@{ Kind = 'fallback'; Value = $match.Value }
    }
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$root = (Resolve-Path $RepoRoot).Path
# Paths are listed as they are, so a path holding a non-ASCII character is not
# quoted and is still found.
$tracked = & git -C $root -c core.quotePath=false ls-files
if ($LASTEXITCODE -ne 0) {
    throw "git ls-files failed in $root"
}

# The tests of this script name every form it fails on, on purpose.
$ownTests = 'ci/check-environment-variable-names.Tests.ps1'

$found = @()
$fallbacks = @()
$read = 0
$skipped = @()
foreach ($relative in $tracked) {
    if ($relative -eq $ownTests) {
        continue
    }
    $path = Join-Path $root $relative
    # A submodule is tracked as a directory, and its files belong to another
    # repository.
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $skipped += $relative
        continue
    }
    $read++
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadLines($path)) {
        $lineNumber++
        foreach ($problem in Find-EnvironmentNameProblem $line) {
            $text = "${relative}:${lineNumber}: $($problem.Value)"
            if ($problem.Kind -eq 'digit') {
                $found += $text
            } else {
                $fallbacks += $text
            }
        }
    }
}

Write-Output ("$read paths read, $($skipped.Count) skipped as not files: " +
    "$($skipped -join ', '), and $ownTests left out")

if ($found.Count -gt 0 -or $fallbacks.Count -gt 0) {
    Write-Output $found
    Write-Output $fallbacks
    throw ("$($found.Count) names start with a digit, which a POSIX shell " +
        'cannot set, so each must start with an underscore, and ' +
        "$($fallbacks.Count) old names are read, set or kept, where each " +
        'variable has one name.')
}

Write-Output 'No tracked file names a variable starting with a digit or an old name.'
