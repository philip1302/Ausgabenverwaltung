<#
    .SYNOPSIS
    Veroeffentlicht die Ausgabenverwaltung fuer eine oder mehrere
    Zielplattformen.

    .DESCRIPTION
    Ruft je Ziel "dotnet publish" mit dem passenden Profil aus
    Properties\PublishProfiles auf (Release, selbststaendig lauffaehig,
    Einzeldatei, ohne Trimming - siehe die .pubxml-Dateien fuer die
    Begruendung jeder einzelnen Einstellung).

    Fuer die beiden macOS-Ziele (osx-arm64, osx-x64) baut das Skript im
    Anschluss ein vollstaendiges App-Bundle
    (Ausgabenverwaltung.app\Contents\{MacOS,Resources}, Info.plist) und
    packt es als .tar.gz. TAR statt ZIP, weil TAR das Ausfuehrungsrecht
    der Datei in Contents\MacOS\ als Teil des Formats mitfuehrt - ZIP
    kennt unter Windows keine zuverlaessige Entsprechung. Da Windows
    (NTFS) selbst kein Unix-Ausfuehrungsrecht kennt und
    System.Formats.Tar in Windows PowerShell 5.1 (.NET Framework) nicht
    verfuegbar ist, schreibt dieses Skript das ustar-Format von Hand
    (siehe New-UstarHeader/New-TarGzArchive) - das Ausfuehrungsrecht
    steht damit unabhaengig von den tatsaechlichen Windows-Dateiattributen
    korrekt im Archiv, nie ueber ein chmod auf einer Windows-Kopie.

    .PARAMETER Targets
    Liste der Ziel-RIDs: win-x64, linux-x64, osx-arm64, osx-x64.

    .PARAMETER All
    Baut alle vier Ziele. Hat Vorrang vor -Targets.

    .EXAMPLE
    .\publish.ps1
    Nur win-x64 - die Plattform, auf der dieses Skript typischerweise
    laeuft, und ohne Angabe deshalb die sinnvollste Vorgabe.

    .EXAMPLE
    .\publish.ps1 -Targets osx-arm64, osx-x64

    .EXAMPLE
    .\publish.ps1 -All
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')]
    [string[]] $Targets,

    [switch] $All
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

# ============================================================
# Feste Werte
# ============================================================

$AllTargets = @('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64')
$MacTargets = @('osx-arm64', 'osx-x64')

if ($All) {
    $Targets = $AllTargets
}
elseif (-not $Targets -or $Targets.Count -eq 0) {
    $Targets = @('win-x64')
}
else {
    # Reihenfolge und Duplikate aus einer freien -Targets-Liste
    # vereinheitlichen, damit "erzeugte Dateien" am Ende in derselben
    # Reihenfolge erscheinen wie $AllTargets.
    $Targets = $AllTargets | Where-Object { $Targets -contains $_ }
}

$RepoRoot = $PSScriptRoot
$ProjectFile = Join-Path $RepoRoot 'Ausgabenverwaltung.csproj'
$PublishRoot = Join-Path $RepoRoot 'bin\Release\net10.0\publish'
$IconIcnsPath = Join-Path $RepoRoot 'Assets\AppIcon.icns'
$ExecutableName = 'Ausgabenverwaltung'
$DisplayName = 'Ausgabenverwaltung'
$BundleIdentifier = 'de.benni.ausgabenverwaltung'

# .NET 8 hat die minimal unterstuetzte macOS-Version fuer Windows/x64
# und Arm64 auf Monterey (12) angehoben; spaetere .NET-Versionen haben
# das bislang nicht weiter gesenkt. Bei Bedarf pruefen und anpassen:
# https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md
$MinimumSystemVersion = '12.0'

# Version aus dem Projekt lesen statt sie hier zu duplizieren -
# dieselbe Zahl wie im Protokoll und im aufklappbaren Bereich jedes
# Fehlerdialogs (siehe Kommentar bei <Version> in der csproj).
[xml] $csprojXml = Get-Content -LiteralPath $ProjectFile
$AppVersion = $csprojXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $AppVersion) {
    throw "Konnte <Version> nicht aus $ProjectFile lesen."
}

# ============================================================
# tar+gzip von Hand: siehe Erklaerung im Skriptkopf oben.
# ============================================================

# Schreibt ASCII-Text an eine feste Stelle im (bereits nullinitiali-
# sierten) 512-Byte-Kopfpuffer. Der Rest des Feldes bleibt NUL - das
# ist bei allen ustar-Textfeldern (name, magic, uname, gname, prefix)
# der korrekte Leerwert.
function Set-TarField {
    param([byte[]] $Header, [int] $Offset, [string] $Text)

    $bytes = [System.Text.Encoding]::ASCII.GetBytes($Text)
    [Array]::Copy($bytes, 0, $Header, $Offset, $bytes.Length)
}

# Numerische ustar-Felder stehen als linksbuendig nullgefuellte
# Oktalziffern, terminiert durch ein NUL - deshalb Width - 1 Ziffern,
# das abschliessende NUL ergibt sich aus der Nullinitialisierung des
# Puffers von selbst.
function Set-TarOctalField {
    param([byte[]] $Header, [int] $Offset, [int] $Width, [long] $Value)

    $digits = [Convert]::ToString($Value, 8).PadLeft($Width - 1, '0')
    Set-TarField -Header $Header -Offset $Offset -Text $digits
}

# Ein einzelner 512-Byte ustar-Kopf. Feldlayout nach POSIX.1-1988:
#   0    100  name
#   100    8  mode      (oktal)
#   108    8  uid       (oktal)
#   116    8  gid       (oktal)
#   124   12  size      (oktal)
#   136   12  mtime     (oktal)
#   148    8  chksum    (oktal, waehrend der Berechnung mit Leerzeichen belegt)
#   156    1  typeflag  ('0' Datei, '5' Verzeichnis)
#   157  100  linkname  (hier ungenutzt)
#   257    6  magic     "ustar\0"
#   263    2  version   "00" (NICHT NUL-terminiert)
#   265   32  uname / 297 32 gname (hier ungenutzt)
#   329    8  devmajor / 337 8 devminor (hier ungenutzt)
#   345  155  prefix    (hier ungenutzt - unsere Namen bleiben < 100 Zeichen)
function New-UstarHeader {
    param(
        [string] $EntryName,
        [long] $Size,
        [char] $TypeFlag,
        [int] $Mode,
        [long] $ModTimeUnix
    )

    $nameBytes = [System.Text.Encoding]::UTF8.GetBytes($EntryName)
    if ($nameBytes.Length -gt 100) {
        throw "Tar-Eintragsname laenger als 100 Zeichen (ustar-Grenze ohne 'prefix'-Feld): $EntryName"
    }

    $header = New-Object byte[] 512

    [Array]::Copy($nameBytes, 0, $header, 0, $nameBytes.Length)
    Set-TarOctalField -Header $header -Offset 100 -Width 8 -Value $Mode
    Set-TarOctalField -Header $header -Offset 108 -Width 8 -Value 0
    Set-TarOctalField -Header $header -Offset 116 -Width 8 -Value 0
    Set-TarOctalField -Header $header -Offset 124 -Width 12 -Value $Size
    Set-TarOctalField -Header $header -Offset 136 -Width 12 -Value $ModTimeUnix

    for ($i = 148; $i -lt 156; $i++) { $header[$i] = 0x20 }
    $header[156] = [byte] $TypeFlag
    Set-TarField -Header $header -Offset 257 -Text 'ustar'
    $header[263] = [byte][char] '0'
    $header[264] = [byte][char] '0'

    # Pruefsumme: Summe ALLER 512 Bytes, waehrend das chksum-Feld selbst
    # mit Leerzeichen belegt ist (oben bereits geschehen) - danach als
    # 6 Oktalziffern + NUL + Leerzeichen zurueckgeschrieben.
    $checksum = 0
    foreach ($b in $header) { $checksum += $b }
    Set-TarField -Header $header -Offset 148 -Text ([Convert]::ToString($checksum, 8).PadLeft(6, '0'))
    $header[154] = 0
    $header[155] = 0x20

    return , $header
}

# [System.IO.Path]::GetRelativePath gibt es erst seit .NET Core 2.0 -
# Windows PowerShell 5.1 laeuft auf dem klassischen .NET Framework und
# kennt die Methode nicht. Da FullPath hier immer unterhalb von
# BasePath liegt (Get-ChildItem -Recurse ab genau diesem Wurzelordner),
# reicht ein einfaches Abschneiden des gemeinsamen Anfangs.
function Get-RelativeSlashPath {
    param([string] $BasePath, [string] $FullPath)

    $normalizedBase = $BasePath.TrimEnd('\', '/')
    $relative = $FullPath.Substring($normalizedBase.Length).TrimStart('\', '/')
    return ($relative -replace '\\', '/')
}

# Baut $SourcePath (samt seines eigenen Ordnernamens als Wurzel des
# Archivs, z. B. "Ausgabenverwaltung.app/...") als .tar.gz. Eintraege
# aus $ExecutableRelativePaths (vollstaendiger Tar-Eintragsname,
# einschliesslich des Wurzelordners) bekommen Modus 755 statt 644 -
# das ist der einzige Grund, warum dieses Skript ueberhaupt selbst
# ein tar-Format schreibt statt eines der eingebauten
# Komprimierungs-Cmdlets zu nutzen: keines davon laesst sich das
# Ausfuehrungsrecht je Eintrag vorschreiben.
function New-TarGzArchive {
    param(
        [Parameter(Mandatory)] [string] $SourcePath,
        [Parameter(Mandatory)] [string] $DestinationPath,
        [string[]] $ExecutableRelativePaths = @()
    )

    $modeDir = [Convert]::ToInt32('755', 8)
    $modeFile = [Convert]::ToInt32('644', 8)
    $modeExecutable = [Convert]::ToInt32('755', 8)

    $topLevelName = Split-Path -Leaf $SourcePath
    $modTime = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

    $tarStream = New-Object System.IO.MemoryStream

    function Write-TarEntry {
        param([string] $EntryName, [byte[]] $Content, [char] $TypeFlag, [int] $Mode)

        $size = if ($Content) { $Content.Length } else { 0 }
        $header = New-UstarHeader -EntryName $EntryName -Size $size -TypeFlag $TypeFlag -Mode $Mode -ModTimeUnix $modTime
        $tarStream.Write($header, 0, $header.Length)

        if ($size -gt 0) {
            $tarStream.Write($Content, 0, $Content.Length)
            $padding = (512 - ($size % 512)) % 512
            if ($padding -gt 0) {
                $tarStream.Write((New-Object byte[] $padding), 0, $padding)
            }
        }
    }

    Write-TarEntry -EntryName "$topLevelName/" -Content $null -TypeFlag '5' -Mode $modeDir

    $items = Get-ChildItem -LiteralPath $SourcePath -Recurse
    # Elternordner muessen vor ihren Kindern im Archiv stehen; nach
    # Pfadtiefe sortiert ist das unabhaengig davon sichergestellt, in
    # welcher Reihenfolge Get-ChildItem selbst liefert.
    $directories = $items | Where-Object { $_.PSIsContainer } |
        Sort-Object { ($_.FullName -split '[\\/]').Count }
    $files = $items | Where-Object { -not $_.PSIsContainer }

    foreach ($dir in $directories) {
        $relative = Get-RelativeSlashPath -BasePath $SourcePath -FullPath $dir.FullName
        Write-TarEntry -EntryName "$topLevelName/$relative/" -Content $null -TypeFlag '5' -Mode $modeDir
    }

    foreach ($file in $files) {
        $relative = Get-RelativeSlashPath -BasePath $SourcePath -FullPath $file.FullName
        $entryName = "$topLevelName/$relative"
        $content = [System.IO.File]::ReadAllBytes($file.FullName)
        $mode = if ($ExecutableRelativePaths -contains $entryName) { $modeExecutable } else { $modeFile }
        Write-TarEntry -EntryName $entryName -Content $content -TypeFlag '0' -Mode $mode
    }

    # Archivende: zwei Nullbloecke (POSIX-Pflicht), danach auf ein
    # Vielfaches von 10240 Byte (klassischer Blocking-Faktor 20)
    # aufgefuellt - kein Muss fuer moderne Lesegeraete, aber das
    # gewohnte Verhalten von GNU tar.
    $endMarker = New-Object byte[] 1024
    $tarStream.Write($endMarker, 0, $endMarker.Length)
    $remainder = $tarStream.Length % 10240
    if ($remainder -ne 0) {
        $tailPadding = New-Object byte[] (10240 - $remainder)
        $tarStream.Write($tailPadding, 0, $tailPadding.Length)
    }

    $tarStream.Position = 0
    $destinationDir = Split-Path -Parent $DestinationPath
    if (-not (Test-Path -LiteralPath $destinationDir)) {
        New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
    }

    $fileStream = [System.IO.File]::Create($DestinationPath)
    try {
        $gzipStream = New-Object System.IO.Compression.GZipStream($fileStream, [System.IO.Compression.CompressionLevel]::Optimal)
        try {
            $tarStream.CopyTo($gzipStream)
        }
        finally {
            $gzipStream.Dispose()
        }
    }
    finally {
        $fileStream.Dispose()
    }
    $tarStream.Dispose()
}

# ============================================================
# Info.plist
# ============================================================

function Write-InfoPlist {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $ExecutableName,
        [Parameter(Mandatory)] [string] $BundleIdentifier,
        [Parameter(Mandatory)] [string] $DisplayName,
        [Parameter(Mandatory)] [string] $Version,
        [Parameter(Mandatory)] [string] $MinimumSystemVersion,
        [switch] $IncludeIcon
    )

    # CFBundleIconFile faellt weg, wenn kein Symbol vorhanden ist - das
    # Bundle bleibt dann trotzdem gueltig, macOS zeigt nur das
    # generische App-Symbol (siehe PUBLISH.md, Abschnitt Symbol).
    $iconEntry = if ($IncludeIcon) {
        "`n    <key>CFBundleIconFile</key>`n    <string>AppIcon</string>"
    }
    else {
        ''
    }

    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$DisplayName</string>
    <key>CFBundleDisplayName</key>
    <string>$DisplayName</string>
    <key>CFBundleIdentifier</key>
    <string>$BundleIdentifier</string>
    <key>CFBundleExecutable</key>
    <string>$ExecutableName</string>$iconEntry
    <key>CFBundleVersion</key>
    <string>$Version</string>
    <key>CFBundleShortVersionString</key>
    <string>$Version</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>$MinimumSystemVersion</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
"@

    # Ohne BOM: Out-File/Set-Content haengen unter Windows PowerShell
    # standardmaessig ein UTF-8-BOM an. plutil/macOS vertragen das zwar,
    # noetig ist es fuer eine reine ASCII/UTF-8-plist aber nicht.
    [System.IO.File]::WriteAllText($Path, $plist, (New-Object System.Text.UTF8Encoding($false)))
}

# ============================================================
# App-Bundle
# ============================================================

# Baut aus der von "dotnet publish" erzeugten Einzeldatei
# (lose in $PublishDir) das vollstaendige App-Bundle. Die rohe Datei
# WIRD dabei Teil des Bundles - danach liegt in $PublishDir keine lose
# Kopie mehr, nur noch Ausgabenverwaltung.app (siehe Ausgabestruktur).
function New-MacAppBundle {
    param(
        [Parameter(Mandatory)] [string] $PublishDir,
        [Parameter(Mandatory)] [string] $ExecutableName,
        [Parameter(Mandatory)] [string] $BundleIdentifier,
        [Parameter(Mandatory)] [string] $DisplayName,
        [Parameter(Mandatory)] [string] $Version,
        [Parameter(Mandatory)] [string] $MinimumSystemVersion,
        [string] $IconIcnsPath
    )

    $bundlePath = Join-Path $PublishDir "$ExecutableName.app"
    if (Test-Path -LiteralPath $bundlePath) {
        Remove-Item -LiteralPath $bundlePath -Recurse -Force
    }

    $macOsDir = Join-Path $bundlePath 'Contents\MacOS'
    $resourcesDir = Join-Path $bundlePath 'Contents\Resources'
    New-Item -ItemType Directory -Force -Path $macOsDir | Out-Null
    New-Item -ItemType Directory -Force -Path $resourcesDir | Out-Null

    $rawExecutable = Join-Path $PublishDir $ExecutableName
    if (-not (Test-Path -LiteralPath $rawExecutable)) {
        throw "Erwartete Publish-Ausgabe nicht gefunden: $rawExecutable (RID-Profil ohne PublishSingleFile geaendert?)"
    }
    Move-Item -LiteralPath $rawExecutable -Destination (Join-Path $macOsDir $ExecutableName) -Force

    $hasIcon = [bool]($IconIcnsPath -and (Test-Path -LiteralPath $IconIcnsPath))
    if ($hasIcon) {
        Copy-Item -LiteralPath $IconIcnsPath -Destination (Join-Path $resourcesDir 'AppIcon.icns') -Force
    }
    else {
        Write-Warning "Kein Symbol gefunden ($IconIcnsPath) - das Bundle entsteht ohne CFBundleIconFile. Siehe PUBLISH.md, Abschnitt Symbol, fuer den manuellen Weg."
    }

    Write-InfoPlist -Path (Join-Path $bundlePath 'Contents\Info.plist') `
        -ExecutableName $ExecutableName -BundleIdentifier $BundleIdentifier -DisplayName $DisplayName `
        -Version $Version -MinimumSystemVersion $MinimumSystemVersion -IncludeIcon:$hasIcon

    return $bundlePath
}

# ============================================================
# Dateigroessen fuer die Abschlussuebersicht
# ============================================================

function Get-DirectorySizeBytes {
    param([string] $Path)

    $sum = (Get-ChildItem -LiteralPath $Path -Recurse -File | Measure-Object -Property Length -Sum).Sum
    if (-not $sum) { return 0 }
    return $sum
}

function Format-FileSize {
    param([long] $Bytes)

    return "{0:N1} MB" -f ($Bytes / 1MB)
}

# ============================================================
# Hauptteil
# ============================================================

Write-Host "Ziele: $($Targets -join ', ')" -ForegroundColor Cyan

$producedArtifacts = [System.Collections.Generic.List[pscustomobject]]::new()

foreach ($target in $Targets) {
    Write-Host ""
    Write-Host "== $target ==" -ForegroundColor Cyan

    & dotnet publish $ProjectFile -c Release "-p:PublishProfile=$target"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish fuer $target fehlgeschlagen (Exit $LASTEXITCODE)."
    }

    $publishDir = Join-Path $PublishRoot $target

    if ($MacTargets -contains $target) {
        $bundlePath = New-MacAppBundle -PublishDir $publishDir -ExecutableName $ExecutableName `
            -BundleIdentifier $BundleIdentifier -DisplayName $DisplayName -Version $AppVersion `
            -MinimumSystemVersion $MinimumSystemVersion -IconIcnsPath $IconIcnsPath

        $executableEntry = "$ExecutableName.app/Contents/MacOS/$ExecutableName"
        $tarGzPath = Join-Path $publishDir "$ExecutableName-$target.tar.gz"
        New-TarGzArchive -SourcePath $bundlePath -DestinationPath $tarGzPath -ExecutableRelativePaths @($executableEntry)

        $producedArtifacts.Add([pscustomobject]@{
            Target = $target
            Path   = $bundlePath
            Bytes  = Get-DirectorySizeBytes -Path $bundlePath
            Label  = 'App-Bundle'
        })
        $producedArtifacts.Add([pscustomobject]@{
            Target = $target
            Path   = $tarGzPath
            Bytes  = (Get-Item -LiteralPath $tarGzPath).Length
            Label  = 'tar.gz'
        })
    }
    else {
        $executableFileName = if ($target -eq 'win-x64') { "$ExecutableName.exe" } else { $ExecutableName }
        $executablePath = Join-Path $publishDir $executableFileName

        $producedArtifacts.Add([pscustomobject]@{
            Target = $target
            Path   = $executablePath
            Bytes  = (Get-Item -LiteralPath $executablePath).Length
            Label  = 'Einzeldatei'
        })
    }
}

Write-Host ""
Write-Host "Erzeugte Dateien:" -ForegroundColor Cyan
foreach ($artifact in $producedArtifacts) {
    Write-Host ("  [{0,-9}] {1,-11} {2,10}  {3}" -f $artifact.Target, $artifact.Label, (Format-FileSize $artifact.Bytes), $artifact.Path)
}
