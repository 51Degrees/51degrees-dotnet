param (
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$OrgName,
    [string]$Version,
    [bool]$DryRun = $false
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

. "$PSScriptRoot/packages.ps1"

# Each package is tagged and released on its own, so the shared tag step,
# which would tag the run's single version, is skipped.
$global:SkipUpdateTag = $true

$packages = @(Get-ChildItem "$pwd/package/*.nupkg" -ErrorAction SilentlyContinue)
if (-not $packages) {
    Write-Host "No packages to publish"
    return
}

if ($DryRun) {
    Write-Host "Dry run, not pushing:"
    $packages | ForEach-Object { Write-Host "  $($_.Name)" }
} else {
    # The job that publishes has the nuget.org key under this name.
    if ([string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
        throw "NUGET_API_KEY is not set, so publishing cannot succeed."
    }
    Push-Location package
    try {
        dotnet nuget push "*.nupkg" --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY --skip-duplicate
    } finally {
        Pop-Location
    }
}

# Every released package is tagged '<package>/<version>' at this commit, and
# the tags go up in one atomic push, so a publish that stops leaves either all
# of them or none. Then each gets its GitHub release. The tags this checkout
# made for packages that continue from their family's tag are not pushed.
$versions = Get-PackageVersions -RepoName $RepoName
$tags = @($versions.Keys | Where-Object { $versions[$_].Release } |
    ForEach-Object { "$_/$($versions[$_].Next)" })
foreach ($tag in $tags) {
    Write-Output "Tagging '$tag'"
    git -C $RepoName tag $tag
}
if ($DryRun) {
    Write-Output "Dry run - not pushing $($tags.Count) tags or creating releases"
} elseif ($tags) {
    git -C $RepoName push --atomic origin @tags
    foreach ($tag in $tags) {
        gh -R $OrgName/$RepoName release create --verify-tag --generate-notes $tag
    }
}

# The root Directory.Build.props records the published versions, which Package
# mode and the examples restore against. The change goes up as a pull
# request into rc, like the other nightly changes, as only rc merges into main.
$props = "$RepoName/Directory.Build.props"
$content = Get-Content $props -Raw
foreach ($name in $versions.Keys) {
    $v = $versions[$name]
    $content = $content -replace "<$($v.Property)>[^<]*</$($v.Property)>", "<$($v.Property)>$($v.Next)</$($v.Property)>"
}
Set-Content $props $content -NoNewline
./steps/has-changed.ps1 -RepoName $RepoName
if ($LASTEXITCODE -eq 0) {
    $branch = "update-versions/rc"
    git -C $RepoName switch -C $branch
    ./steps/commit-changes.ps1 -RepoName $RepoName -Message "Record the published package versions"
    ./steps/push-changes.ps1 -RepoName $RepoName -DryRun $DryRun -Force $true
    ./steps/pull-request.ps1 -RepoName $RepoName -From $branch -To "rc" -Message "Record the published package versions" -DryRun $DryRun
}
exit 0
