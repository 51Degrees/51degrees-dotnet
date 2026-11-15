# Tests for ci/packages.json and ci/packages.ps1, run with Pester 5 or later.
#
# The first tests read this repository's project files and check that each
# NuGet package has its own entry, dependencies and version property. The
# last tests run common-ci's release plan on a throwaway git repository, when
# COMMON_CI names a checkout of common-ci that has steps/release-plan.ps1.

BeforeAll {
    $script:Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).ProviderPath
    . (Join-Path $PSScriptRoot 'packages.ps1')

    # The project files a package is built from.
    function Get-PackageProjects($Package) {
        if ($Package.Nuspec) {
            return @($Package.Projects)
        }
        return @($Package.Project)
    }

    function Get-ProjectXml([string]$Path) {
        $xml = [xml]::new()
        $xml.Load((Join-Path $Root $Path))
        return $xml
    }

    # The items of a kind in a project, whatever their namespace.
    function Get-Items([xml]$Xml, [string]$Name) {
        return @($Xml.SelectNodes("//*[local-name()='$Name']"))
    }

    # The package each project file belongs to, by its path.
    $script:PackageOfProject = @{}
    foreach ($package in $PackageList) {
        foreach ($project in Get-PackageProjects $package) {
            $PackageOfProject[$project] = $package.Name
        }
    }
}

Describe 'packages.json' {
    It 'is not replaced by a variable of a script that reads it' {
        # PowerShell variable names ignore case, so a script's own
        # $packages would replace a $Packages that packages.ps1 defines.
        $own = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $PSScriptRoot 'packages.ps1'), [ref]$null, [ref]$null)
        $defined = @($own.EndBlock.Statements |
            Where-Object { $_ -is [Management.Automation.Language.AssignmentStatementAst] } |
            ForEach-Object { $_.Left.VariablePath.UserPath })
        $defined | Should -Contain 'Families'
        $scripts = @(Get-ChildItem $PSScriptRoot -Filter '*.ps1' |
            Where-Object { $_.Name -notlike '*.Tests.ps1' -and $_.Name -ne 'packages.ps1' } |
            Where-Object { (Get-Content -Raw $_.FullName) -match 'packages\.ps1' })
        $scripts.Count | Should -BeGreaterThan 0
        foreach ($script in $scripts) {
            $ast = [Management.Automation.Language.Parser]::ParseFile(
                $script.FullName, [ref]$null, [ref]$null)
            $assigned = @($ast.FindAll({
                $args[0] -is [Management.Automation.Language.AssignmentStatementAst]
            }, $true) | ForEach-Object Left |
                Where-Object { $_ -is [Management.Automation.Language.VariableExpressionAst] } |
                ForEach-Object { $_.VariablePath.UserPath })
            foreach ($name in $assigned) {
                $defined | Should -Not -Contain $name -Because "$($script.Name) assigns `$$name"
            }
        }
    }

    It 'lists every project the families pack once, by its package id' {
        $packed = @(Get-PackableProjects -Solutions ($Families.Solutions))
        $listed = @($PackageList | Where-Object { -not $_.Nuspec } |
            ForEach-Object Project)
        ($listed | Sort-Object) | Should -Be ($packed | Sort-Object)
        $names = @($PackageList.Name)
        $names.Count | Should -Be @($names | Sort-Object -Unique).Count
        foreach ($package in $PackageList | Where-Object { -not $_.Nuspec }) {
            $xml = Get-ProjectXml $package.Project
            $id = (Get-Items $xml 'PackageId' | Select-Object -First 1).InnerText
            if (-not $id) {
                $id = [IO.Path]::GetFileNameWithoutExtension($package.Project)
            }
            $package.Name | Should -Be $id
            $package.Path | Should -Be (Split-Path $package.Project -Parent).Replace('\', '/')
        }
    }

    It 'gives each package the packages its projects reference' {
        foreach ($package in $PackageList) {
            $referenced = foreach ($project in Get-PackageProjects $package) {
                $xml = Get-ProjectXml $project
                $folder = Split-Path $project -Parent
                foreach ($item in Get-Items $xml 'ProjectReference') {
                    $path = [IO.Path]::GetFullPath((Join-Path $Root $folder $item.Include.Replace('\', '/')))
                    $relative = [IO.Path]::GetRelativePath($Root, $path).Replace('\', '/')
                    $PackageOfProject[$relative] ?? "no package: $relative"
                }
                foreach ($item in Get-Items $xml 'FiftyOneReference') {
                    ($PackageList | Where-Object Name -eq $item.Include).Name ?? "no package: $($item.Include)"
                }
            }
            $expected = @($referenced | Where-Object { $_ -ne $package.Name } |
                Sort-Object -Unique)
            @($package.DependsOn | Sort-Object) | Should -Be $expected -Because $package.Name
        }
    }

    It 'gives the root Directory.Build.props one version per package and nothing else' {
        $props = Get-ProjectXml 'Directory.Build.props'
        $defined = @($props.SelectNodes("//*[local-name()='PropertyGroup']/*") |
            Where-Object { $_.LocalName -match '^FiftyOne.*Version$' } |
            ForEach-Object LocalName)
        ($defined | Sort-Object) | Should -Be (@($PackageList.Property) | Sort-Object)
    }

    It 'sets each project a package is built from to that package''s version' {
        $set = @{}
        foreach ($item in Get-Items (Get-ProjectXml 'Directory.Build.props') 'Version') {
            $match = [regex]::Match($item.Condition, "'\$\(MSBuildProjectName\)' == '([^']+)'")
            if ($match.Success) {
                $set[$match.Groups[1].Value] = $item.InnerText
            }
        }
        $projects = 0
        foreach ($package in $PackageList) {
            foreach ($project in Get-PackageProjects $package) {
                $name = [IO.Path]::GetFileNameWithoutExtension($project)
                $set[$name] | Should -Be "`$($($package.Property))" -Because $project
                $projects++
            }
        }
        $set.Count | Should -Be $projects
    }

    It 'has every FiftyOneReference name the version of the package it references' {
        $projects = @(git -C $Root ls-files '*.csproj')
        $projects.Count | Should -BeGreaterThan 0
        foreach ($project in $projects) {
            foreach ($item in Get-Items (Get-ProjectXml $project) 'FiftyOneReference') {
                $package = $PackageList | Where-Object Name -eq $item.Include
                $package | Should -Not -BeNullOrEmpty -Because "$project names $($item.Include)"
                $item.Version | Should -Be "`$($($package.Property))" -Because "$project names $($item.Include)"
            }
        }
    }

    It 'has each nuspec name the version of each FiftyOne package it depends on' {
        foreach ($package in $PackageList | Where-Object Nuspec) {
            $xml = Get-ProjectXml $package.Nuspec
            $dependencies = @(Get-Items $xml 'dependency' |
                Where-Object { $_.id -like 'FiftyOne.*' })
            $dependencies.Count | Should -BeGreaterThan 0
            foreach ($dependency in $dependencies) {
                $named = $PackageList | Where-Object Name -eq $dependency.id
                $named | Should -Not -BeNullOrEmpty -Because $dependency.id
                $dependency.version | Should -Be "`$$($named.Property)`$"
                $package.DependsOn | Should -Contain $dependency.id
            }
        }
    }
}

Describe 'A release planned per package' {
    BeforeDiscovery {
        $step = $env:COMMON_CI ? (Join-Path $env:COMMON_CI 'steps' 'release-plan.ps1') : $null
        $script:HasStep = $step -and (Test-Path $step)
    }

    BeforeAll {
        $step = $env:COMMON_CI ? (Join-Path $env:COMMON_CI 'steps' 'release-plan.ps1') : $null
        if ($step -and (Test-Path $step)) {
            . $step
        }
        $script:Identity = @(
            '-c', 'user.name=Release plan test',
            '-c', 'user.email=test@example.com')

        function Add-File([string]$Repo, [string]$Path, [string]$Text) {
            $full = Join-Path $Repo $Path
            $null = New-Item -ItemType Directory -Force (Split-Path $full -Parent)
            Set-Content -Path $full -Value $Text
            git -C $Repo add -A
            git -C $Repo @Identity commit --quiet -m "Change $Path"
        }

        function New-Package([string]$Name, [string]$Path, [string[]]$DependsOn) {
            [pscustomobject]@{
                Name = $Name
                Path = $Path
                DependsOn = $DependsOn
                Publish = $true
                Family = 'fam'
                Project = "$Path/$Name.csproj"
            }
        }

        # A family released once at 4.5.10, before each package had its own
        # version. Org.A.Core's folder name starts with Org.A's.
        function New-FamilyRepo {
            $repo = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid())
            $null = New-Item -ItemType Directory $repo
            git -C $repo init --quiet
            Add-File $repo 'fam/A/Org.A.csproj' '<Project />'
            Add-File $repo 'fam/A.Core/Org.A.Core.csproj' '<Project />'
            Add-File $repo 'fam/B/Org.B.csproj' '<Project />'
            git -C $repo tag 'fam/4.5.10'
            return $repo
        }
    }

    BeforeEach {
        $script:Repo = New-FamilyRepo
        $script:TestPackages = @(
            (New-Package 'Org.A' 'fam/A' @())
            (New-Package 'Org.A.Core' 'fam/A.Core' @())
            (New-Package 'Org.B' 'fam/B' @('Org.A'))
        )
    }

    AfterEach {
        Remove-Item -Recurse -Force $Repo
    }

    It 'continues each package from its family''s last tag' -Skip:(-not $HasStep) {
        Add-FamilyStartTags -RepoPath $Repo -Packages $TestPackages
        foreach ($name in 'Org.A', 'Org.A.Core', 'Org.B') {
            @(Get-ReleasedVersions $Repo $name) | Should -Be @([version]'4.5.10')
            git -C $Repo rev-parse "$name/4.5.10^{commit}" |
                Should -Be (git -C $Repo rev-parse 'fam/4.5.10^{commit}')
        }
        @(Get-ReleasePlan $Repo $TestPackages 6>$null) | Should -BeNullOrEmpty
    }

    It 'releases a changed package and its dependants, and nothing else' -Skip:(-not $HasStep) {
        Add-File $Repo 'fam/A/Code.cs' 'class A {}'
        Add-FamilyStartTags -RepoPath $Repo -Packages $TestPackages
        $plan = @(Get-ReleasePlan $Repo $TestPackages 6>$null)
        $plan.Name | Should -Be @('Org.A', 'Org.B')
        $plan.Version | Should -Be @('4.5.11', '4.5.11')
    }

    It 'does not release a package whose folder name starts another''s' -Skip:(-not $HasStep) {
        Add-File $Repo 'fam/A.Core/Code.cs' 'class C {}'
        Add-FamilyStartTags -RepoPath $Repo -Packages $TestPackages
        $plan = @(Get-ReleasePlan $Repo $TestPackages 6>$null)
        $plan.Name | Should -Be @('Org.A.Core')
    }

    It 'starts a package added since the family''s tag from its FirstVersion' -Skip:(-not $HasStep) {
        Add-File $Repo 'fam/New/Org.New.csproj' '<Project />'
        $new = New-Package 'Org.New' 'fam/New' @()
        $new | Add-Member FirstVersion '1.0.0'
        $packages = $TestPackages + $new
        Add-FamilyStartTags -RepoPath $Repo -Packages $packages
        @(Get-ReleasedVersions $Repo 'Org.New') | Should -BeNullOrEmpty
        $plan = @(Get-ReleasePlan $Repo $packages 6>$null)
        $plan.Name | Should -Be @('Org.New')
        $plan.Version | Should -Be @('1.0.0')
    }

    It 'keeps a package''s own tags once it has them' -Skip:(-not $HasStep) {
        git -C $Repo tag 'Org.A/4.5.11'
        Add-FamilyStartTags -RepoPath $Repo -Packages $TestPackages
        @(Get-ReleasedVersions $Repo 'Org.A') | Should -Be @([version]'4.5.11')
    }
}
