[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$')]
    [string]$RootNamespace,

    [Parameter(Mandatory = $true)]
    [string]$ProductName,

    [Parameter(Mandatory = $true)]
    [string]$ProductNameArabic,

    [Parameter(Mandatory = $true)]
    [string]$ShortProductName,

    [Parameter(Mandatory = $true)]
    [string]$ShortProductNameArabic,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z][a-z0-9_]*$')]
    [string]$DatabaseName
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$marker = Join-Path $root '.appcore-initialized'

if (Test-Path $marker) {
    throw 'This repository has already been initialized.'
}

$rootIdentifier = $RootNamespace.Replace('.', '')
$slug = ($RootNamespace -replace '\.', '-').ToLowerInvariant()
$skipDirectories = @('.git', 'bin', 'obj', 'node_modules', '.vite', 'dist', 'coverage')
$binaryExtensions = @(
    '.png', '.jpg', '.jpeg', '.gif', '.ico', '.webp', '.pdf', '.zip', '.dll', '.exe'
)

function Replace-AppCoreIdentifierTokens {
    param([Parameter(Mandatory = $true)][string]$Value)

    # Embedded type/file identifiers cannot contain namespace separators.
    $result = [regex]::Replace(
        $Value,
        '(?<=[A-Za-z0-9_])AppCore|AppCore(?=[A-Za-z0-9_])',
        $rootIdentifier
    )

    # Standalone namespace, assembly, project, and solution tokens use the
    # requested dotted root namespace.
    return $result.Replace('AppCore', $RootNamespace)
}

$files = Get-ChildItem $root -Recurse -File -Force | Where-Object {
    $relativePath = $_.FullName.Substring($root.Length).TrimStart(
        [IO.Path]::DirectorySeparatorChar
    )

    -not ($skipDirectories | Where-Object {
        $relativePath -eq $_ -or
        $relativePath.StartsWith("$_$([IO.Path]::DirectorySeparatorChar)")
    })
}

foreach ($file in $files) {
    if ($file.Extension.ToLowerInvariant() -in $binaryExtensions) {
        continue
    }

    try {
        $content = Get-Content $file.FullName -Raw -Encoding UTF8
    }
    catch {
        continue
    }

    $content = Replace-AppCoreIdentifierTokens $content
    $content = $content.Replace('app-core', $slug)
    $content = $content.Replace('app_core', $DatabaseName)
    $content = $content.Replace('APP_CORE', $DatabaseName.ToUpperInvariant())
    $content = $content.Replace('نظام التطبيق', $ProductNameArabic)
    $content = $content.Replace('"' + $RootNamespace + '"', '"' + $ProductName + '"')
    $content = $content.Replace("'" + $RootNamespace + "'", "'" + $ProductName + "'")
    $content = $content.Replace('"Core"', '"' + $ShortProductName + '"')
    $content = $content.Replace("'Core'", "'" + $ShortProductName + "'")

    $relativePath = $file.FullName.Substring($root.Length).Replace('\', '/')
    if ($relativePath.EndsWith('/frontend/src/i18n/locales/ar.ts')) {
        $content = $content.Replace(
            "organizationName: '$ProductName'",
            "organizationName: '$ProductNameArabic'"
        )
        $content = $content.Replace(
            "organizationShortName: '$ShortProductName'",
            "organizationShortName: '$ShortProductNameArabic'"
        )
    }

    Set-Content $file.FullName $content -Encoding UTF8 -NoNewline
}

Get-ChildItem $root -Recurse -Force |
    Where-Object {
        $_.Name -like '*AppCore*' -and
        $_.FullName -notlike "*$([IO.Path]::DirectorySeparatorChar).git$([IO.Path]::DirectorySeparatorChar)*"
    } |
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $newName = Replace-AppCoreIdentifierTokens $_.Name
        Rename-Item $_.FullName $newName
    }

@"
ProductName=$ProductName
ProductNameArabic=$ProductNameArabic
ShortProductName=$ShortProductName
ShortProductNameArabic=$ShortProductNameArabic
RootNamespace=$RootNamespace
DatabaseName=$DatabaseName
InitializedAtUtc=$([DateTimeOffset]::UtcNow.ToString('O'))
"@ | Set-Content $marker -Encoding UTF8

Write-Host "Initialized $ProductName. Review changes and run the full quality gates."
