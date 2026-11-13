# Tests for ci/release-plan-filter.ps1, run with Pester 5 or later.
#
# Each test commits a change to a project file or nuspec in a throwaway git
# repository tagged as a released family, and asks the filter which files
# count. The last tests run common-ci's release plan with the filter, when
# COMMON_CI names a checkout of common-ci that has steps/release-plan.ps1.

BeforeAll {
    $script:Filter = Join-Path $PSScriptRoot 'release-plan-filter.ps1'
    $script:Family = [pscustomobject]@{ Name = 'pkg'; Path = 'pkg' }
    $script:Tag = 'pkg/1.0.0'

    # The commits in the throwaway repositories carry their own identity.
    $script:Identity = @{
        GIT_AUTHOR_NAME = 'Release plan filter test'
        GIT_AUTHOR_EMAIL = 'test@example.com'
        GIT_COMMITTER_NAME = 'Release plan filter test'
        GIT_COMMITTER_EMAIL = 'test@example.com'
    }
    $script:Saved = @{}
    foreach ($name in $Identity.Keys) {
        $Saved[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $Identity[$name])
    }

    # A project with every kind of content the filter tells apart.
    $script:Project = @'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Version>1.0.0</Version>
    <Description>The first description.</Description>
    <Authors>51Degrees Engineering</Authors>
    <Copyright>51Degrees Mobile Experts Limited</Copyright>
    <PackageProjectUrl>https://example.com/one</PackageProjectUrl>
    <RepositoryUrl>https://example.com/repository</RepositoryUrl>
    <PackageTags>one;two</PackageTags>
    <PackageReleaseNotes>The first release.</PackageReleaseNotes>
    <PackageIcon>icon.png</PackageIcon>
    <PackageLicenseExpression>EUPL-1.2</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)'=='Debug'">
    <DebugType>full</DebugType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
    <ProjectReference Include="..\Other\Other.csproj" />
  </ItemGroup>

  <!-- The native library. -->
  <ItemGroup>
    <_PackageFiles Include="..\linux\x64\Native.dll">
      <PackagePath>runtimes\linux-x64\native</PackagePath>
    </_PackageFiles>
  </ItemGroup>

  <Target Name="PreBuild" BeforeTargets="PreBuildEvent">
    <Exec Command="pwsh PreBuild.ps1" />
  </Target>

</Project>
'@

    $script:Nuspec = @'
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>Pkg</id>
    <version>$version$</version>
    <description>The first description.</description>
    <authors>51Degrees Engineering</authors>
    <projectUrl>https://example.com/one</projectUrl>
    <repository type="git" url="https://example.com/repository" />
    <dependencies>
      <group targetFramework="net8.0">
        <dependency id="Other" version="1.0.0" />
      </group>
    </dependencies>
  </metadata>
  <files>
    <file src="bin\net8.0\Pkg.dll" target="lib\net8.0" />
  </files>
</package>
'@

    # The edits below match line ends of "`n", whatever line ends a checkout
    # gave this file.
    $script:Project = $Project -replace "`r`n", "`n"
    $script:Nuspec = $Nuspec -replace "`r`n", "`n"

    # A repository holding the project, the nuspec and a source file, tagged
    # as the family's last release. The files have a byte order mark, as
    # the project files in this repository do.
    function New-Repo {
        $path = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
        $null = New-Item -ItemType Directory -Force (Join-Path $path 'pkg')
        Set-Content (Join-Path $path 'pkg' 'Pkg.csproj') $Project -Encoding utf8BOM -NoNewline
        Set-Content (Join-Path $path 'pkg' 'Pkg.nuspec') $Nuspec -Encoding utf8BOM -NoNewline
        Set-Content (Join-Path $path 'pkg' 'Code.cs') 'class Code { }'
        git init --quiet $path
        git -C $path add -A
        git -C $path commit --quiet -m 'Released'
        git -C $path tag $Tag
        return $path
    }

    # Replaces text in a file of the repository, keeping its byte order
    # mark, and commits it.
    function Edit-File([string]$Repo, [string]$File, [string]$Old,
            [string]$New) {
        $full = Join-Path $Repo $File
        $text = Get-Content $full -Raw
        if ($text.Contains($Old) -eq $false) {
            throw "'$Old' is not in $File"
        }
        Set-Content $full $text.Replace($Old, $New) -Encoding utf8BOM -NoNewline
        git -C $Repo commit --quiet -am "Change $File"
    }

    function Invoke-Filter([string]$Repo, [string[]]$Files) {
        return @(& $Filter -RepoPath $Repo -Package $Family -From $Tag `
            -Files $Files 6>$null)
    }

    $script:Repos = @()
}

AfterAll {
    foreach ($name in $Saved.Keys) {
        [Environment]::SetEnvironmentVariable($name, $Saved[$name])
    }
    foreach ($repo in $Repos) {
        Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue
    }
}

Describe 'A project file' {
    BeforeEach {
        $script:Repo = New-Repo
        $script:Repos += $Repo
    }

    It 'starts with a byte order mark, as the project files here do' {
        $bytes = [IO.File]::ReadAllBytes((Join-Path $Repo 'pkg' 'Pkg.csproj'))
        $bytes[0..2] | Should -Be @(0xEF, 0xBB, 0xBF)
    }

    It 'does not count when only <Property> changed' -ForEach @(
        @{ Property = 'Description'; Old = 'The first description.'; New = 'The second description.' }
        @{ Property = 'Authors'; Old = '<Authors>51Degrees Engineering<'; New = '<Authors>51Degrees<' }
        @{ Property = 'Copyright'; Old = '<Copyright>51Degrees Mobile Experts Limited<'; New = '<Copyright>51Degrees<' }
        @{ Property = 'PackageProjectUrl'; Old = 'https://example.com/one'; New = 'https://example.com/two' }
        @{ Property = 'RepositoryUrl'; Old = 'https://example.com/repository'; New = 'https://example.com/moved' }
        @{ Property = 'PackageTags'; Old = 'one;two'; New = 'one;two;three' }
        @{ Property = 'PackageReleaseNotes'; Old = 'The first release.'; New = 'The second release.' }
        @{ Property = 'PackageIcon'; Old = 'icon.png'; New = 'logo.png' }
        @{ Property = 'PackageLicenseExpression'; Old = 'EUPL-1.2'; New = 'MIT' }
        @{ Property = 'PackageReadmeFile'; Old = '<PackageReadmeFile>README.md<'; New = '<PackageReadmeFile>docs/README.md<' }
    ) {
        Edit-File $Repo 'pkg/Pkg.csproj' $Old $New
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -BeNullOrEmpty
    }

    It 'does not count when a metadata property is added or removed' {
        Edit-File $Repo 'pkg/Pkg.csproj' "    <PackageTags>one;two</PackageTags>`n" ''
        Edit-File $Repo 'pkg/Pkg.csproj' '<PackageIcon>' "<PackageReleaseNotes>More.</PackageReleaseNotes>`n    <PackageIcon>"
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -BeNullOrEmpty
    }

    It 'does not count metadata moved to a property group of its own' {
        Edit-File $Repo 'pkg/Pkg.csproj' "    <PackageTags>one;two</PackageTags>`n" ''
        Edit-File $Repo 'pkg/Pkg.csproj' '  <ItemGroup>' "  <PropertyGroup>`n    <PackageTags>one;two</PackageTags>`n  </PropertyGroup>`n`n  <ItemGroup>"
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -BeNullOrEmpty
    }

    It 'does not count a change only to the white space between elements' {
        Edit-File $Repo 'pkg/Pkg.csproj' "`n    <Version>" "`n`n        <Version>"
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -BeNullOrEmpty
    }

    It 'counts a change to the target frameworks, as adding net10.0 does' {
        Edit-File $Repo 'pkg/Pkg.csproj' '<TargetFramework>netstandard2.0</TargetFramework>' '<TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>'
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -Be @('pkg/Pkg.csproj')
    }

    It 'counts a change to <What>' -ForEach @(
        @{ What = 'a package reference'; Old = 'Version="13.0.4"'; New = 'Version="13.0.5"' }
        @{ What = 'a project reference'; Old = '..\Other\Other.csproj'; New = '..\Another\Another.csproj' }
        @{ What = 'the version'; Old = '<Version>1.0.0<'; New = '<Version>1.0.1<' }
        @{ What = 'a native library item'; Old = 'runtimes\linux-x64\native'; New = 'runtimes\linux-arm64\native' }
        @{ What = 'a build target'; Old = 'pwsh PreBuild.ps1'; New = 'pwsh PreBuild.ps1 -Arch x64' }
        @{ What = 'a property that is not metadata'; Old = '<DebugType>full<'; New = '<DebugType>portable<' }
        @{ What = 'a comment'; Old = 'The native library.'; New = 'The native libraries.' }
    ) {
        Edit-File $Repo 'pkg/Pkg.csproj' $Old $New
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -Be @('pkg/Pkg.csproj')
    }

    It 'counts a change to metadata made together with a build change' {
        Edit-File $Repo 'pkg/Pkg.csproj' 'https://example.com/one' 'https://example.com/two'
        Edit-File $Repo 'pkg/Pkg.csproj' '<TargetFramework>netstandard2.0</TargetFramework>' '<TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>'
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -Be @('pkg/Pkg.csproj')
    }

    It 'counts a metadata property moved into a target, where it is built' {
        Edit-File $Repo 'pkg/Pkg.csproj' "    <PackageTags>one;two</PackageTags>`n" ''
        Edit-File $Repo 'pkg/Pkg.csproj' '    <Exec ' "    <PropertyGroup><PackageTags>one;two</PackageTags></PropertyGroup>`n    <Exec "
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -Be @('pkg/Pkg.csproj')
    }

    It 'counts a new project file' {
        Set-Content (Join-Path $Repo 'pkg' 'New.csproj') $Project
        git -C $Repo add -A
        git -C $Repo commit --quiet -m 'New project'
        Invoke-Filter $Repo 'pkg/New.csproj' | Should -Be @('pkg/New.csproj')
    }

    It 'counts a deleted project file' {
        git -C $Repo rm --quiet 'pkg/Pkg.csproj'
        git -C $Repo commit --quiet -m 'Deleted project'
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -Be @('pkg/Pkg.csproj')
    }

    It 'counts a project file that is no longer valid XML' {
        Edit-File $Repo 'pkg/Pkg.csproj' '</Project>' '</Projec>'
        Invoke-Filter $Repo 'pkg/Pkg.csproj' | Should -Be @('pkg/Pkg.csproj')
    }

    It 'counts every file that is not a project file or nuspec' {
        Edit-File $Repo 'pkg/Code.cs' 'class Code { }' 'class Code { int x; }'
        Invoke-Filter $Repo 'pkg/Code.cs' | Should -Be @('pkg/Code.cs')
    }

    It 'keeps the files that count and drops the others, in order' {
        Edit-File $Repo 'pkg/Pkg.csproj' 'https://example.com/one' 'https://example.com/two'
        Edit-File $Repo 'pkg/Code.cs' 'class Code { }' 'class Code { int x; }'
        Invoke-Filter $Repo @('pkg/Code.cs', 'pkg/Pkg.csproj') | Should -Be @('pkg/Code.cs')
    }

    It 'reads the repository from a path relative to the current folder' {
        Edit-File $Repo 'pkg/Pkg.csproj' 'https://example.com/one' 'https://example.com/two'
        Edit-File $Repo 'pkg/Code.cs' 'class Code { }' 'class Code { int x; }'
        Push-Location (Split-Path $Repo -Parent)
        try {
            Invoke-Filter (Split-Path $Repo -Leaf) @('pkg/Pkg.csproj', 'pkg/Code.cs') |
                Should -Be @('pkg/Code.cs')
        } finally {
            Pop-Location
        }
    }
}

Describe 'A nuspec' {
    BeforeEach {
        $script:Repo = New-Repo
        $script:Repos += $Repo
    }

    It 'does not count when only <What> changed' -ForEach @(
        @{ What = 'the project link'; Old = 'https://example.com/one'; New = 'https://example.com/two' }
        @{ What = 'the description'; Old = 'The first description.'; New = 'The second description.' }
        @{ What = 'the repository'; Old = 'https://example.com/repository'; New = 'https://example.com/moved' }
    ) {
        Edit-File $Repo 'pkg/Pkg.nuspec' $Old $New
        Invoke-Filter $Repo 'pkg/Pkg.nuspec' | Should -BeNullOrEmpty
    }

    It 'counts a change to <What>' -ForEach @(
        @{ What = 'a dependency'; Old = 'version="1.0.0"'; New = 'version="1.0.1"' }
        @{ What = 'a dependency group'; Old = 'targetFramework="net8.0"'; New = 'targetFramework="net10.0"' }
        @{ What = 'the files'; Old = 'target="lib\net8.0"'; New = 'target="lib\net10.0"' }
        @{ What = 'the id'; Old = '<id>Pkg<'; New = '<id>Pkg.Other<' }
    ) {
        Edit-File $Repo 'pkg/Pkg.nuspec' $Old $New
        Invoke-Filter $Repo 'pkg/Pkg.nuspec' | Should -Be @('pkg/Pkg.nuspec')
    }
}

Describe 'With common-ci''s release plan' {
    BeforeDiscovery {
        # Only a release plan that calls a filter script can test this one.
        $step = $env:COMMON_CI ? (Join-Path $env:COMMON_CI 'steps' 'release-plan.ps1') : $null
        $script:HasStep = $step -and (Test-Path $step) -and
            (Select-String -Path $step -Pattern 'release-plan-filter.ps1' -Quiet)
    }

    BeforeAll {
        $step = $env:COMMON_CI ? (Join-Path $env:COMMON_CI 'steps' 'release-plan.ps1') : $null
        if ($step -and (Test-Path $step)) {
            . $step
        }
    }

    BeforeEach {
        $script:Repo = New-Repo
        $script:Repos += $Repo
        $null = New-Item -ItemType Directory -Force (Join-Path $Repo 'ci')
        Copy-Item $Filter (Join-Path $Repo 'ci' 'release-plan-filter.ps1')
        git -C $Repo add -A
        git -C $Repo commit --quiet -m 'The filter'
        $script:Packages = @([pscustomobject]@{
            Name = 'pkg'; Path = 'pkg'; DependsOn = @(); Publish = $true
        })
    }

    It 'releases nothing for a change only to a project''s links' -Skip:(-not $HasStep) {
        Edit-File $Repo 'pkg/Pkg.csproj' 'https://example.com/one' 'https://example.com/two'
        @(Get-ReleasePlan $Repo $Packages 6>$null) | Should -BeNullOrEmpty
    }

    It 'releases the family for a change to its target frameworks' -Skip:(-not $HasStep) {
        Edit-File $Repo 'pkg/Pkg.csproj' '<TargetFramework>netstandard2.0</TargetFramework>' '<TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>'
        $plan = @(Get-ReleasePlan $Repo $Packages 6>$null)
        $plan.Name | Should -Be @('pkg')
        $plan.Version | Should -Be @('1.0.1')
        $plan.Reason | Should -Be @('changed since 1.0.0 (1 file)')
    }
}
