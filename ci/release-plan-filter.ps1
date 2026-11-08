# Called by common-ci's steps/release-plan.ps1 for each family with changed
# files, once its globs have left out documentation, tests and examples.
# Returns the files whose changes release the family.
#
# A project file or nuspec does not count when the only changes to it are to
# the package metadata listed below, such as its description or links, or to
# the white space between elements. Any other change to it counts, a comment
# included, as does every other file. A file that is new, deleted or not
# valid XML counts, so that a doubt releases the family.
param (
    [Parameter(Mandatory)][string]$RepoPath,
    [Parameter(Mandatory)]$Package,
    [Parameter(Mandatory)][string]$From,
    [string[]]$Files = @()
)
$ErrorActionPreference = 'Stop'

# The MSBuild properties of a project file that only describe the package.
$ProjectMetadata = @(
    'Description'
    'Authors'
    'Copyright'
    'PackageProjectUrl'
    'RepositoryUrl'
    'PackageTags'
    'PackageReleaseNotes'
    'PackageIcon'
    'PackageLicenseExpression'
    'PackageReadmeFile'
)

# The same metadata as the elements of a nuspec's metadata.
$NuspecMetadata = @(
    'description'
    'authors'
    'copyright'
    'projectUrl'
    'repository'
    'tags'
    'releaseNotes'
    'icon'
    'license'
    'readme'
)

# git runs as a process of its own, which starts in the process's folder
# rather than PowerShell's current one, so a relative path is resolved here.
$RepoPath = (Resolve-Path $RepoPath).ProviderPath

# The file's bytes at the revision, or null when it is not there. Bytes, so
# that the XML reader reads the file's byte order mark and encoding itself.
function Get-Blob([string]$Revision, [string]$Path) {
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    foreach ($argument in '-C', $RepoPath, 'show', "${Revision}:$Path") {
        $start.ArgumentList.Add($argument)
    }
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.UseShellExecute = $false
    $git = [Diagnostics.Process]::Start($start)
    $errors = $git.StandardError.ReadToEndAsync()
    $content = [IO.MemoryStream]::new()
    $git.StandardOutput.BaseStream.CopyTo($content)
    $git.WaitForExit()
    $null = $errors.Result
    if ($git.ExitCode -ne 0) {
        return $null
    }
    return , $content.ToArray()
}

# The XML with the elements the paths select taken out, then any property
# group they left empty, and without the white space between elements. Null
# when the content is not XML.
function Get-BuildXml([byte[]]$Content, [string[]]$Paths) {
    $xml = [Xml.XmlDocument]::new()
    $xml.PreserveWhitespace = $false
    try {
        $xml.Load([IO.MemoryStream]::new($Content))
    } catch [Xml.XmlException] {
        return $null
    }
    foreach ($path in $Paths) {
        foreach ($node in @($xml.SelectNodes($path))) {
            $null = $node.ParentNode.RemoveChild($node)
        }
    }
    $empty = "/*[local-name()='Project']/*[local-name()='PropertyGroup'][not(*)]"
    foreach ($node in @($xml.SelectNodes($empty))) {
        $null = $node.ParentNode.RemoveChild($node)
    }
    return $xml.OuterXml
}

# Whether every change to the file since the family's last tag is to its
# package metadata.
function Test-MetadataOnly([string]$Path) {
    if ($Path -match '\.csproj$') {
        $paths = $ProjectMetadata | ForEach-Object {
            "/*[local-name()='Project']/*[local-name()='PropertyGroup']" +
                "/*[local-name()='$_']"
        }
    } elseif ($Path -match '\.nuspec$') {
        $paths = $NuspecMetadata | ForEach-Object {
            "/*[local-name()='package']/*[local-name()='metadata']" +
                "/*[local-name()='$_']"
        }
    } else {
        return $false
    }
    $before = Get-Blob $From $Path
    $after = Get-Blob 'HEAD' $Path
    if ($null -eq $before -or $null -eq $after) {
        return $false
    }
    $before = Get-BuildXml $before $paths
    $after = Get-BuildXml $after $paths
    return $null -ne $before -and $before -ceq $after
}

foreach ($file in $Files) {
    if (Test-MetadataOnly $file) {
        Write-Host "  $($Package.Name): $file changes only package metadata, so it does not count"
    } else {
        $file
    }
}
