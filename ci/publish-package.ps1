param (
    [Parameter(Mandatory)][string]$RepoName,
    [Parameter(Mandatory)][string]$OrgName,
    [Parameter(Mandatory)][string]$ApiKey,
    [string]$Version,
    [bool]$DryRun = $false
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

. "$PSScriptRoot/packages.ps1"

# Each family is tagged and released on its own, so the shared tag step,
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
    Push-Location package
    try {
        dotnet nuget push "*.nupkg" --source https://api.nuget.org/v3/index.json --api-key $ApiKey --skip-duplicate
    } finally {
        Pop-Location
    }
}

$versions = Get-FamilyVersions -RepoName $RepoName
foreach ($family in $Families) {
    $v = $versions[$family.Name]
    if (-not $v.Changed) { continue }
    ./steps/update-tag.ps1 -RepoName $RepoName -OrgName $OrgName -Tag "$($family.Name)/$($v.Next)" -DryRun $DryRun
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
