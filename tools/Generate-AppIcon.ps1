<#
.SYNOPSIS
Regenerates src/QuickTools/QuickTools.ico, the exe's icon, from AppIcon.Draw: the same code that
draws the tray icon. Run it after changing AppIcon.cs, then commit the updated .ico.

.EXAMPLE
pwsh tools/Generate-AppIcon.ps1
#>
[CmdletBinding()]
param(
    # Every size Windows commonly asks for: small icons at 100-250% scaling, Explorer views, and 256 for
    # the largest Explorer icons and Alt+Tab.
    [int[]]$Sizes = @(16, 20, 24, 32, 40, 48, 64, 256)
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src/QuickTools/QuickTools.csproj'
$output = Join-Path $root 'src/QuickTools/QuickTools.ico'

# Build into a fresh folder so stale binaries are never used. ApplicationIcon is cleared so the build
# also works when QuickTools.ico is missing.
$buildDir = Join-Path ([IO.Path]::GetTempPath()) "QuickTools-icon-$([guid]::NewGuid().ToString('N'))"
dotnet build $project -c Release -o $buildDir -p:ApplicationIcon= --nologo -v quiet | Out-Host
if ($LASTEXITCODE) { throw "Build failed (exit code $LASTEXITCODE)." }

Add-Type -AssemblyName System.Drawing
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $buildDir 'QuickTools.dll'))
$draw = $assembly.GetType('QuickTools.AppIcon', $true).GetMethod('Draw')

# Each frame is stored as PNG, which Windows Vista and later read at every size.
$frames = foreach ($size in $Sizes | Sort-Object -Unique) {
    # [int] unwraps the PSObject that Sort-Object produces; reflection needs a plain Int32.
    $bitmap = $draw.Invoke($null, [object[]]@([int]$size))
    try {
        $png = [IO.MemoryStream]::new()
        $bitmap.Save($png, [Drawing.Imaging.ImageFormat]::Png)
        [pscustomobject]@{ Size = $size; Data = $png.ToArray() }
    }
    finally {
        $bitmap.Dispose()
    }
}

# ICO layout: ICONDIR (6 bytes), one ICONDIRENTRY (16 bytes) per frame, then the frame data.
$stream = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0)              # reserved
$writer.Write([uint16]1)              # type: icon
$writer.Write([uint16]$frames.Count)

$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }   # 0 means 256
    $writer.Write([byte]$dimension)   # width
    $writer.Write([byte]$dimension)   # height
    $writer.Write([byte]0)            # palette colors
    $writer.Write([byte]0)            # reserved
    $writer.Write([uint16]1)          # color planes
    $writer.Write([uint16]32)         # bits per pixel
    $writer.Write([uint32]$frame.Data.Length)
    $writer.Write([uint32]$offset)
    $offset += $frame.Data.Length
}
foreach ($frame in $frames) { $writer.Write($frame.Data) }
$writer.Flush()

[IO.File]::WriteAllBytes($output, $stream.ToArray())
Remove-Item $buildDir -Recurse -Force -ErrorAction SilentlyContinue
"Wrote $output ($($frames.Size -join ', ') px, $($stream.Length) bytes)"
