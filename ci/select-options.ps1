param(
    # Every entry of options.json.
    [Parameter(Mandatory)][object[]]$Options,
    # The files the pull request changes, less documentation.
    [string[]]$ChangedFiles,
    # Those of the changed files whose change touches only comments.
    [string[]]$CommentOnlyFiles
)
# Chooses the options.json entries a pull request is built on, for common-ci's
# steps/select-options.ps1. The branch itself and a release build every entry.
#
# The standard set is the Linux x64 build, with the Selenium contract, and the
# Windows x64 build, the only one of the .NET Framework projects. Neither runs
# the IP Intelligence tests that load the Enterprise data file, or the web
# example tests. A change in more than its comments to a native engine or the
# cxx submodule adds Linux ARM and Windows x86, a change to IP Intelligence or
# the cxx submodule runs the Enterprise tests, and a change to the web
# integration or a web example runs the web example tests on Linux. A change
# outside the package folders, such as ci, adds every entry. Only five macOS
# jobs run at once across the organisation, and the releases need them, so
# macOS is left to the branch and release builds, as is performance.

$Standard = @('Ubuntu_x64_Release', 'Windows_x64_Release')
$Contract = 'Ubuntu_x64_Release'
$Native = @('Ubuntu_ARM_Release', 'Windows_x86_Release')
$PackageFolders = @('caching/', 'common/', 'device-detection/',
    'ip-intelligence/', 'location/', 'pipeline/', 'examples/')
$NativePaths = @('device-detection/Hash.Engine.OnPremise/',
    'ip-intelligence/Engine.OnPremise/')
$IpIntelligencePaths = @('ip-intelligence/', 'examples/ip-intelligence/')
$WebPaths = @('pipeline/Web/', 'pipeline/Web.Framework/', 'pipeline/Web.Shared/',
    'pipeline/JavaScriptBuilderElement/')

function Test-Under([string]$Path, [string[]]$Prefixes) {
    return @($Prefixes | Where-Object { $Path.StartsWith($_) }).Count -gt 0
}

$code = @($ChangedFiles | Where-Object { $CommentOnlyFiles -notcontains $_ })
$cxx = @($code | Where-Object { $_ -eq 'cxx' }).Count -gt 0
$native = $cxx -or @($code | Where-Object { Test-Under $_ $NativePaths }).Count -gt 0
$ipi = $cxx -or
    @($code | Where-Object { Test-Under $_ $IpIntelligencePaths }).Count -gt 0
$web = @($code | Where-Object {
    (Test-Under $_ $WebPaths) -or
        ($_.StartsWith('examples/device-detection/') -and $_ -clike '*Web*')
}).Count -gt 0
$other = @($code | Where-Object {
    $_ -ne 'cxx' -and !(Test-Under $_ $PackageFolders)
}).Count -gt 0
Write-Host ("Native: $native. IP Intelligence: $ipi. Web: $web. " +
    "Outside the package folders: $other.")

foreach ($entry in $Options) {
    $name = $entry.Name
    if ($entry.Image -like 'macos*') {
        Write-Host "$name is left to the branch and release builds"
        continue
    }
    $why = if ($Standard -contains $name) { 'the standard set' }
        elseif ($other) { 'a change outside the package folders' }
        elseif ($native -and $Native -contains $name) { 'a native change' }
    if (!$why) {
        Write-Host "$name is not needed for this change"
        continue
    }
    $entry.Depth = ($ipi -or $other) ? 'deep' : 'standard'
    $entry.WebExamples = $other -or ($web -and $name -eq $Contract)
    $entry.SeleniumContract = $name -eq $Contract
    $entry.RunPerformance = $false
    Write-Host ("$name runs for $why, at depth $($entry.Depth), with the web " +
        "examples $($entry.WebExamples ? 'tested' : 'left out')")
    $entry
}
