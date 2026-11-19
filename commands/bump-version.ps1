#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automated version bumping script for Unity Package project

.DESCRIPTION
    Updates version numbers across all project files automatically to prevent human errors.
    Supports preview mode for safe testing.

.PARAMETER NewVersion
    The new version number in semver format (e.g., "0.18.0")

.PARAMETER WhatIf
    Preview changes without applying them

.EXAMPLE
    .\bump-version.ps1 -NewVersion "0.18.0"

.EXAMPLE
    .\bump-version.ps1 -NewVersion "0.18.0" -WhatIf
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$NewVersion,

    [switch]$WhatIf
)

# Set location to repository root (parent of commands folder)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
Push-Location $repoRoot

# Script configuration
$ErrorActionPreference = "Stop"

# Version file locations (relative to script root)
$VersionFiles = @(
    @{
        Path        = "Unity-MeshGen/Assets/root/package.json"
        Pattern     = '"version":\s*"[\d\.]+"'
        Replace     = '"version": "{VERSION}"'
        Description = "Unity package version"
    },
    @{
        Path        = "Installer/Assets/Unity Mesh Generation Installer/Installer.cs"
        Pattern     = 'public const string Version = "[\d\.]+";'
        Replace     = 'public const string Version = "{VERSION}";'
        Description = "Installer C# version constant"
    },
    @{
        Path        = "Unity-MeshGen/Assets/root/README.md"
        Pattern     = "https://github\.com/Saesentsessis/Unity-MeshGen/releases/download/[\d\.]+/Unity-MeshGen-Installer\.unitypackage"
        Replace     = "https://github.com/Saesentsessis/Unity-MeshGen/releases/download/{VERSION}/Unity-MeshGen-Installer.unitypackage"
        Description = "Package README download URL"
    },
    @{
        Path        = "README.md"
        Pattern     = "https://github\.com/Saesentsessis/Unity-MeshGen/releases/download/[\d\.]+/Unity-MeshGen-Installer\.unitypackage"
        Replace     = "https://github.com/Saesentsessis/Unity-MeshGen/releases/download/{VERSION}/Unity-MeshGen-Installer.unitypackage"
        Description = "Repository README download URL"
    },
    # Install snippets. Built with [regex]::Escape because the package id / repository contain regex metacharacters ('.').
    @{
        Path        = "Unity-MeshGen/Assets/root/README.md"
        Pattern     = [regex]::Escape('"com.saesentsessis.unity-meshgen":') + '\s*"[\d\.]+"'
        Replace     = '"com.saesentsessis.unity-meshgen": "{VERSION}"'
        Description = "Package README manifest.json example"
    },
    @{
        Path        = "README.md"
        Pattern     = [regex]::Escape('"com.saesentsessis.unity-meshgen":') + '\s*"[\d\.]+"'
        Replace     = '"com.saesentsessis.unity-meshgen": "{VERSION}"'
        Description = "Repository README manifest.json example"
    },
    @{
        Path        = "Unity-MeshGen/Assets/root/README.md"
        Pattern     = [regex]::Escape("https://github.com/Saesentsessis/Unity-MeshGen.git?path=Unity-MeshGen/Assets/root#") + '[\d\.]+'
        Replace     = "https://github.com/Saesentsessis/Unity-MeshGen.git?path=Unity-MeshGen/Assets/root#{VERSION}"
        Description = "Package README git URL tag"
    },
    @{
        Path        = "README.md"
        Pattern     = [regex]::Escape("https://github.com/Saesentsessis/Unity-MeshGen.git?path=Unity-MeshGen/Assets/root#") + '[\d\.]+'
        Replace     = "https://github.com/Saesentsessis/Unity-MeshGen.git?path=Unity-MeshGen/Assets/root#{VERSION}"
        Description = "Repository README git URL tag"
    }
)

function Write-ColorText {
    param([string]$Text, [string]$Color = "White")
    Write-Host $Text -ForegroundColor $Color
}

function Get-TotalReplacements {
    param([object[]]$Changes)

    # Sum explicitly: 'Measure-Object -Property' cannot read hashtable keys in Windows PowerShell 5.1.
    $total = 0
    foreach ($change in $Changes) {
        $total += $change.Matches
    }
    return $total
}

function Get-ChangedFileCount {
    param([object[]]$Changes)

    return @($Changes | ForEach-Object { $_.Path } | Select-Object -Unique).Count
}

# UTF-8 file I/O that keeps each file's BOM state. Get-Content/Set-Content without -Encoding are not used:
# Windows PowerShell 5.1 would decode BOM-less UTF-8 with the ANSI code page, so non-ASCII text (e.g. a
# package or installer name) would never match the patterns.
function Read-Utf8File {
    param([string]$Path)

    # [System.IO.File] resolves relative paths against the process directory, not Push-Location.
    $resolvedPath = (Resolve-Path -LiteralPath $Path).ProviderPath
    $bytes = [System.IO.File]::ReadAllBytes($resolvedPath)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }

    return @{
        Text   = [System.Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
        HasBom = $hasBom
    }
}

function Write-Utf8File {
    param([string]$Path, [string]$Text, [bool]$HasBom)

    $resolvedPath = (Resolve-Path -LiteralPath $Path).ProviderPath
    [System.IO.File]::WriteAllText($resolvedPath, $Text, (New-Object System.Text.UTF8Encoding($HasBom)))
}

function Test-SemanticVersion {
    param([string]$Version)

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return $false
    }

    # Basic semver pattern: major.minor.patch (with optional prerelease/build)
    $pattern = '^\d+\.\d+\.\d+(-[a-zA-Z0-9\-\.]+)?(\+[a-zA-Z0-9\-\.]+)?$'
    return $Version -match $pattern
}

function Get-CurrentVersion {
    # Extract current version from package.json
    $packageJsonPath = "Unity-MeshGen/Assets/root/package.json"
    if (-not (Test-Path $packageJsonPath)) {
        throw "Could not find package.json at: $packageJsonPath"
    }

    $content = (Read-Utf8File $packageJsonPath).Text
    if ($content -match '"version":\s*"([\d\.]+)"') {
        return $Matches[1]
    }

    throw "Could not extract current version from package.json"
}

function Update-VersionFiles {
    param([string]$OldVersion, [string]$NewVersion, [bool]$PreviewOnly = $false)

    $changes = @()

    # Working copy per file, so several patterns on the same file accumulate instead of overwriting each other.
    $pendingContent = [ordered]@{}
    $pendingHasBom = @{}

    foreach ($file in $VersionFiles) {
        $fullPath = $file.Path

        if (-not (Test-Path -LiteralPath $fullPath)) {
            Write-ColorText "[!] File not found: $($file.Path)" "Yellow"
            continue
        }

        if (-not $pendingContent.Contains($fullPath)) {
            $textFile = Read-Utf8File $fullPath
            $pendingContent[$fullPath] = $textFile.Text
            $pendingHasBom[$fullPath] = $textFile.HasBom
        }

        $originalContent = $pendingContent[$fullPath]

        # Create the replacement string
        $replacement = $file.Replace -replace '\{VERSION\}', $NewVersion

        # Apply the replacement
        $newContent = $originalContent -replace $file.Pattern, $replacement

        # Check if any changes were made
        if ($originalContent -ne $newContent) {
            # Count matches for reporting
            $regexMatches = [regex]::Matches($originalContent, $file.Pattern)

            $pendingContent[$fullPath] = $newContent

            $changes += @{
                Path        = $file.Path
                Description = $file.Description
                Matches     = $regexMatches.Count
            }

            Write-ColorText "[*] $($file.Description): $($regexMatches.Count) occurrence(s)" "Green"

            # Show the actual changes
            foreach ($match in $regexMatches) {
                $newValue = $match.Value -replace $file.Pattern, $replacement
                Write-ColorText "   $($match.Value) -> $newValue" "Gray"
            }
        }
        else {
            Write-ColorText "[!] No matches found in: $($file.Path)" "Yellow"
            Write-ColorText "   Pattern: $($file.Pattern)" "Gray"
        }
    }

    if ($changes.Count -eq 0) {
        Write-ColorText "[x] No version references found to update!" "Red"
        Pop-Location
        exit 1
    }

    if ($PreviewOnly) {
        Write-ColorText "`n[i] Preview Summary:" "Cyan"
        Write-ColorText "Files to be modified: $(Get-ChangedFileCount $changes)" "White"
        Write-ColorText "Total replacements: $(Get-TotalReplacements $changes)" "White"
        return $null
    }

    # Apply changes: write each changed file once, with all of its replacements.
    $changedPaths = @($changes | ForEach-Object { $_.Path } | Select-Object -Unique)
    foreach ($fullPath in $changedPaths) {
        Write-Utf8File -Path $fullPath -Text $pendingContent[$fullPath] -HasBom $pendingHasBom[$fullPath]
    }

    return $changes
}

# Main execution
try {
    Write-ColorText "Package Version Bump Script" "Cyan"
    Write-ColorText "=================================" "Cyan"

    # Validate semantic version format
    if (-not (Test-SemanticVersion $NewVersion)) {
        Write-ColorText "[x] Invalid semantic version format: $NewVersion" "Red"
        Write-ColorText "Expected format: major.minor.patch (e.g., '1.2.3')" "Yellow"
        Pop-Location
        exit 1
    }

    # Get current version
    $currentVersion = Get-CurrentVersion
    Write-ColorText "[i] Current version: $currentVersion" "White"
    Write-ColorText "[i] New version: $NewVersion" "White"

    if ($currentVersion -eq $NewVersion) {
        Write-ColorText "[!] New version is the same as current version" "Yellow"
        Pop-Location
        exit 0
    }

    Write-ColorText "`n[i] Scanning for version references..." "Cyan"

    # Update version files
    # @() keeps a single change as an array (a bare hashtable's .Count would count its keys).
    $changes = @(Update-VersionFiles -OldVersion $currentVersion -NewVersion $NewVersion -PreviewOnly $WhatIf)

    if ($WhatIf) {
        Write-ColorText "`n[ok] Preview completed. Use without -WhatIf to apply changes." "Green"
        Pop-Location
        exit 0
    }

    if ($changes -and $changes.Count -gt 0) {
        Write-ColorText "`n[ok] Version bump completed successfully!" "Green"
        Write-ColorText "   Updated $(Get-ChangedFileCount $changes) files" "White"
        Write-ColorText "   Total replacements: $(Get-TotalReplacements $changes)" "White"
        Write-ColorText "   Version: $currentVersion -> $NewVersion" "White"
        Write-ColorText "`n[i] Remember to commit these changes to git" "Cyan"
    }

    Pop-Location
}
catch {
    Write-ColorText "`n[x] Script failed: $($_.Exception.Message)" "Red"
    Pop-Location
    exit 1
}