<#
    .SYNOPSIS
    Erzeugt das Anwendungssymbol fuer Windows (Assets\AppIcon.ico) und
    macOS (Assets\AppIcon.icns) aus derselben, hier gezeichneten
    Quellgrafik: ein Segelboot auf abgerundetem, farbigem Quadrat.

    .DESCRIPTION
    Rein mit .NET/System.Drawing (GDI+), ohne externe Werkzeuge: kein
    ImageMagick, kein "iconutil" (das ohnehin nur unter macOS existiert
    und deshalb auf einem Windows-Build-Rechner nicht zur Verfuegung
    steht). Das Motiv ist bewusst auf wenige, grosse Flaechen reduziert
    (Rumpf, Mast, Segel, Wasserlinie), damit es auch bei 16x16 Pixeln
    noch als Boot erkennbar bleibt.

    Einmalig auszufuehren, wenn sich das Symbol aendern soll. Die
    erzeugten Dateien werden wie jede andere Bild-Ressource ins
    Repository eingecheckt (siehe Assets\avalonia-logo.ico als
    bestehendes Beispiel) - publish.ps1 und die csproj lesen sie nur,
    sie entstehen nicht bei jedem Build neu.

    .EXAMPLE
    pwsh -File tools\New-AppIcon.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$RepoRoot = Split-Path -Parent $PSScriptRoot
$AssetsDir = Join-Path $RepoRoot 'Assets'
$IcoPath = Join-Path $AssetsDir 'AppIcon.ico'
$IcnsPath = Join-Path $AssetsDir 'AppIcon.icns'

# Farben: eine ruhige "Meer"-Blau-Flaeche als Untergrund (kollidiert
# bewusst nicht mit den in App.axaml vergebenen Bedeutungsfarben
# ErstattungFarbe/Rot und EinnahmeFarbe/Gruen), weisser Rumpf und
# weisses Segel, ein warmer Braunton fuer den Mast, ein helleres Blau
# fuer die angedeutete Wasserlinie.
$BackgroundColor = [System.Drawing.Color]::FromArgb(255, 35, 108, 158)
$WaterlineColor = [System.Drawing.Color]::FromArgb(255, 74, 143, 192)
$HullColor = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$SailColor = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$MastColor = [System.Drawing.Color]::FromArgb(255, 107, 79, 53)

function New-RoundedRectPath {
    param(
        [single] $X,
        [single] $Y,
        [single] $Width,
        [single] $Height,
        [single] $Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath

    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    return $path
}

# Zeichnet das Motiv direkt in der Zielgroesse statt immer aus einer
# festen Master-Aufloesung herunterzuskalieren: bei 16x16 wuerde ein
# herunterskaliertes 1024er-Bild den duennen Mast verlieren, waehrend
# ein direkt bei 16px gezeichneter Mast als eigene Linie ohne
# Mindestbreite nicht verschwindet (siehe minMastWidth unten).
# AntiAlias sorgt fuer sauberen Kantenverlauf in jeder Groesse.
function New-IconBitmap {
    param([int] $Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        # --- Untergrund: abgerundetes Quadrat ------------------------
        $cornerRadius = $Size * 0.2237
        $backgroundPath = New-RoundedRectPath -X 0 -Y 0 -Width $Size -Height $Size -Radius $cornerRadius
        $backgroundBrush = New-Object System.Drawing.SolidBrush($BackgroundColor)
        $graphics.FillPath($backgroundBrush, $backgroundPath)
        $backgroundBrush.Dispose()
        $backgroundPath.Dispose()

        # --- Wasserlinie: flache Ellipse unter dem Rumpf --------------
        $waterY = $Size * 0.78
        $waterWidth = $Size * 0.64
        $waterHeight = $Size * 0.09
        $waterRect = [System.Drawing.RectangleF]::new(
            ($Size - $waterWidth) / 2.0, $waterY - $waterHeight / 2.0, $waterWidth, $waterHeight)
        $waterBrush = New-Object System.Drawing.SolidBrush($WaterlineColor)
        $graphics.FillEllipse($waterBrush, $waterRect)
        $waterBrush.Dispose()

        # --- Rumpf: Trapez, oben breit, unten zum Kiel verjuengt ------
        $hullTopY = $Size * 0.62
        $hullBottomY = $Size * 0.78
        $hullTopLeft = $Size * 0.22
        $hullTopRight = $Size * 0.78
        $hullBottomLeft = $Size * 0.36
        $hullBottomRight = $Size * 0.64

        $hullPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $hullPoints = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($hullTopLeft, $hullTopY),
            [System.Drawing.PointF]::new($hullTopRight, $hullTopY),
            [System.Drawing.PointF]::new($hullBottomRight, $hullBottomY),
            [System.Drawing.PointF]::new($hullBottomLeft, $hullBottomY)
        )
        $hullPath.AddPolygon($hullPoints)
        $hullBrush = New-Object System.Drawing.SolidBrush($HullColor)
        $graphics.FillPath($hullBrush, $hullPath)
        $hullBrush.Dispose()
        $hullPath.Dispose()

        # --- Mast: duenne senkrechte Linie ab der Rumpfmitte ----------
        $mastCenterX = $Size * 0.5
        $mastTopY = $Size * 0.16
        $mastBottomY = $hullTopY
        $minMastWidth = 1.2
        $mastWidth = [Math]::Max($minMastWidth, $Size * 0.024)
        $mastPen = New-Object System.Drawing.Pen($MastColor, $mastWidth)
        $mastPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $mastPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawLine($mastPen, $mastCenterX, $mastTopY, $mastCenterX, $mastBottomY)
        $mastPen.Dispose()

        # --- Segel: rechtwinkliges Dreieck am Mast --------------------
        $sailTop = [System.Drawing.PointF]::new($mastCenterX, $Size * 0.18)
        $sailBottom = [System.Drawing.PointF]::new($mastCenterX, $Size * 0.60)
        $sailTip = [System.Drawing.PointF]::new($mastCenterX + $Size * 0.32, $Size * 0.58)

        $sailPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $sailPath.AddPolygon([System.Drawing.PointF[]]@($sailTop, $sailBottom, $sailTip))
        $sailBrush = New-Object System.Drawing.SolidBrush($SailColor)
        $graphics.FillPath($sailBrush, $sailPath)
        $sailBrush.Dispose()
        $sailPath.Dispose()
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function Get-PngBytes {
    param([System.Drawing.Bitmap] $Bitmap)

    $stream = New-Object System.IO.MemoryStream
    $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)

    # Der Komma-Operator verhindert, dass PowerShell das Byte-Array beim
    # Verlassen der Funktion "entrollt" und beim Aufrufer als
    # System.Object[] wieder zusammensetzt (Standardverhalten der
    # Ausgabe-Pipeline bei Array-Rueckgaben). Ohne ihn liefe spaeter
    # jedes .AddRange() auf einer List[byte] gegen eine
    # IEnumerable<Byte>-Typpruefung, die ein Object[] nicht erfuellt.
    return , $stream.ToArray()
}

# ---------------------------------------------------------------
# .ico (Windows): ICONDIR-Header + ICONDIRENTRY je Groesse + rohe
# PNG-Daten. Windows liest PNG-kodierte Icon-Eintraege seit Vista fuer
# jede Groesse - eine separate unkomprimierte BMP/DIB-Kodierung ist
# nicht noetig.
#
# $SizeToPng ist bewusst ein GEWOEHNLICHES Hashtable (@{}) und kein
# [ordered]@{}: OrderedDictionary hat NEBEN dem key-basierten Indexer
# this[object key] auch einen POSITIONS-Indexer this[int index] - bei
# einem Int32-Schluessel wie 16 oder 256 greift .NET dann den
# spezifischeren int-Indexer statt der Schluesselsuche, und
# $dict[16] = ... versucht Position 16 zu setzen statt den Schluessel
# "16" anzulegen. Das gewoehnliche Hashtable kennt nur den
# key-basierten Indexer und ist deshalb hier die richtige Wahl - die
# Reihenfolge der Eintraege spielt fuer ICO/ICNS ohnehin keine Rolle.
# ---------------------------------------------------------------
function New-IcoFile {
    param(
        [hashtable] $SizeToPng,
        [int[]] $Sizes,
        [string] $Path
    )

    $count = $Sizes.Count
    # [byte[]]-Zwangsumwandlung um die ganze Verkettung: der +-Operator
    # zwischen zwei Arrays liefert in PowerShell ein System.Object[],
    # selbst wenn beide Operanden Byte[] sind - siehe Kommentar in
    # Get-PngBytes fuer denselben Effekt bei Funktionsrueckgaben.
    $header = [byte[]] ([byte[]]@(0, 0, 1, 0) + [BitConverter]::GetBytes([uint16] $count))

    $entries = [System.Collections.Generic.List[byte]]::new()
    $imageData = [System.Collections.Generic.List[byte]]::new()
    $offset = 6 + (16 * $count)

    foreach ($size in $Sizes) {
        $png = $SizeToPng[$size]
        # 0 bedeutet in ICONDIRENTRY "256" - der Bytewert selbst kann
        # keine 256 fassen.
        $dim = if ($size -ge 256) { 0 } else { [byte] $size }

        $entries.Add($dim)                                          # bWidth
        $entries.Add($dim)                                          # bHeight
        $entries.Add(0)                                             # bColorCount
        $entries.Add(0)                                             # bReserved
        $entries.AddRange([byte[]] [BitConverter]::GetBytes([uint16] 1))   # wPlanes
        $entries.AddRange([byte[]] [BitConverter]::GetBytes([uint16] 32))  # wBitCount
        $entries.AddRange([byte[]] [BitConverter]::GetBytes([uint32] $png.Length)) # dwBytesInRes
        $entries.AddRange([byte[]] [BitConverter]::GetBytes([uint32] $offset))     # dwImageOffset

        $imageData.AddRange($png)
        $offset += $png.Length
    }

    $bytes = [byte[]] ($header + $entries.ToArray() + $imageData.ToArray())
    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

# ---------------------------------------------------------------
# .icns (macOS): 8-Byte-Kopf "icns" + Gesamtlaenge, danach je Symbol
# ein 4-Byte-Typcode + 4-Byte-Laenge (jeweils Big-Endian) + rohe
# PNG-Daten. Seit Mac OS X 10.7 akzeptiert das Format PNG-kodierte
# Eintraege fuer alle hier verwendeten Typcodes - die alten
# unkomprimierten ARGB-Formate braucht es nicht mehr.
# ---------------------------------------------------------------
function ConvertTo-BigEndianBytes {
    param([uint32] $Value)

    $bytes = [BitConverter]::GetBytes($Value)
    if ([BitConverter]::IsLittleEndian) {
        [Array]::Reverse($bytes)
    }

    # Siehe Kommentar in Get-PngBytes: ohne das fuehrende Komma kaeme
    # beim Aufrufer ein Object[] statt eines Byte[] an.
    return , $bytes
}

function New-IcnsFile {
    param(
        [hashtable] $SizeToTypeCode,
        [hashtable] $SizeToPng,
        [int[]] $Sizes,
        [string] $Path
    )

    $body = [System.Collections.Generic.List[byte]]::new()
    foreach ($size in $Sizes) {
        $typeCode = [System.Text.Encoding]::ASCII.GetBytes([string] $SizeToTypeCode[$size])
        $png = $SizeToPng[$size]
        $chunkLength = 8 + $png.Length

        $body.AddRange($typeCode)
        $body.AddRange((ConvertTo-BigEndianBytes ([uint32] $chunkLength)))
        $body.AddRange($png)
    }

    $totalLength = 8 + $body.Count
    $bytes = [byte[]] (
        [System.Text.Encoding]::ASCII.GetBytes('icns') +
        (ConvertTo-BigEndianBytes ([uint32] $totalLength)) +
        $body.ToArray()
    )

    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

# ---------------------------------------------------------------
# Hauptteil
# ---------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null

$IcoSizes = @(16, 32, 48, 64, 128, 256)
# Gewoehnliches Hashtable statt [ordered]@{} - siehe Kommentar bei
# New-IcoFile: Int32-Schluessel und OrderedDictionary vertragen sich
# beim nachtraeglichen Indexieren nicht.
$IcnsSizeToType = @{
    16   = 'icp4'
    32   = 'icp5'
    64   = 'icp6'
    128  = 'ic07'
    256  = 'ic08'
    512  = 'ic09'
    1024 = 'ic10'
}
$IcnsSizes = @(16, 32, 64, 128, 256, 512, 1024)

$allSizes = ($IcoSizes + $IcnsSizes) | Sort-Object -Unique
$pngBySize = @{}

foreach ($size in $allSizes) {
    $bitmap = New-IconBitmap -Size $size
    try {
        $pngBySize[$size] = Get-PngBytes -Bitmap $bitmap
    }
    finally {
        $bitmap.Dispose()
    }
}

New-IcoFile -SizeToPng $pngBySize -Sizes $IcoSizes -Path $IcoPath
New-IcnsFile -SizeToTypeCode $IcnsSizeToType -SizeToPng $pngBySize -Sizes $IcnsSizes -Path $IcnsPath

Write-Host "Erzeugt: $IcoPath ($([Math]::Round((Get-Item $IcoPath).Length / 1KB, 1)) KB)"
Write-Host "Erzeugt: $IcnsPath ($([Math]::Round((Get-Item $IcnsPath).Length / 1KB, 1)) KB)"
