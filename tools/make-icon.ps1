<#
.SYNOPSIS
  앱 아이콘(src/JsonPretty.App/Assets/app.ico)을 그려서 만든다.
  둥근 사각형(파랑→남색 그라데이션) 안에 흰색 { } 와, 큰 크기에서는 들여쓴 줄 3개를 넣는다.
  크기마다 따로 그려서(축소하지 않고) 작은 크기에서도 선명하게 한다.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$outIco = Join-Path $root 'src\JsonPretty.App\Assets\app.ico'
$outPng = Join-Path $root 'src\JsonPretty.App\Assets\app-256.png'
New-Item -ItemType Directory -Force (Split-Path $outIco) | Out-Null

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경: 둥근 사각형 + 대각선 그라데이션
    $m = [Math]::Max(0.5, $s * 0.03)
    $rect = New-Object System.Drawing.RectangleF $m, $m, ($s - 2 * $m), ($s - 2 * $m)
    $path = New-RoundedRect $rect.X $rect.Y $rect.Width $rect.Height ($s * 0.22)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
        ([System.Drawing.Color]::FromArgb(255, 59, 130, 246)),   # #3B82F6
        ([System.Drawing.Color]::FromArgb(255, 79, 70, 229)),    # #4F46E5
        45.0
    $g.FillPath($grad, $path)

    $white = [System.Drawing.Color]::White
    $small = $s -lt 32

    # 중괄호 { } : 작은 크기는 더 크고 굵게
    $fontSize = if ($small) { $s * 0.70 } else { $s * 0.46 }
    $font = New-Object System.Drawing.Font 'Consolas', $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush $white
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = 'Center'
    $fmt.LineAlignment = 'Center'

    if ($small) {
        # 작은 크기: 괄호 두 개만, 각각 왼쪽·오른쪽 절반에 (한 문자열로 그리면 줄바꿈될 수 있음)
        $g.DrawString('{', $font, $brush, (New-Object System.Drawing.RectangleF ($s * 0.06), (-$s * 0.03), ($s * 0.44), $s), $fmt)
        $g.DrawString('}', $font, $brush, (New-Object System.Drawing.RectangleF ($s * 0.50), (-$s * 0.03), ($s * 0.44), $s), $fmt)
    }
    else {
        $left = New-Object System.Drawing.RectangleF ($s * 0.05), (-$s * 0.03), ($s * 0.26), $s
        $right = New-Object System.Drawing.RectangleF ($s * 0.69), (-$s * 0.03), ($s * 0.26), $s
        $g.DrawString('{', $font, $brush, $left, $fmt)
        $g.DrawString('}', $font, $brush, $right, $fmt)

        # 괄호 사이: 들여쓴 줄 3개 (정렬된 JSON 느낌)
        $lineBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
        $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 125, 211, 252)) # 하늘색 키
        $h = [Math]::Max(2.0, $s * 0.065)
        $x0 = $s * 0.32
        $rows = @(
            @{ y = 0.33; indent = 0.00; key = 0.10; len = 0.32 },
            @{ y = 0.47; indent = 0.06; key = 0.10; len = 0.24 },
            @{ y = 0.61; indent = 0.00; key = 0.10; len = 0.28 }
        )
        foreach ($r in $rows) {
            $x = $x0 + $s * $r.indent
            $y = $s * $r.y
            $kp = New-RoundedRect $x $y ($s * $r.key) $h ($h / 2)
            $g.FillPath($accent, $kp)
            $vp = New-RoundedRect ($x + $s * $r.key + $s * 0.025) $y ($s * $r.len - $s * $r.key) $h ($h / 2)
            $g.FillPath($lineBrush, $vp)
        }
    }

    $g.Dispose()
    return $bmp
}

# 32비트 BGRA 비트맵 → ICO 안에 넣는 DIB(BITMAPINFOHEADER + 아래→위 픽셀 + AND 마스크)
function ConvertTo-IconDib([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle 0, 0, $s, $s
    $data = $bmp.LockBits($rect, 'ReadOnly', ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $stride = $data.Stride
    $pixels = New-Object byte[] ($stride * $s)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $w.Write([UInt32]40); $w.Write([Int32]$s); $w.Write([Int32]($s * 2))   # 높이는 XOR+AND 두 배
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)          # planes, bpp, BI_RGB
    $w.Write([UInt32]($s * $s * 4)); $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($pixels, $y * $stride, $s * 4) }   # 아래 줄부터
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    $w.Write((New-Object byte[] ($maskRow * $s)))   # 알파를 쓰므로 AND 마스크는 0
    $w.Flush()
    # 쉼표: PowerShell이 배열을 바이트 하나하나로 풀어서 반환하지 않게 한다
    return , $ms.ToArray()
}

# ICO 파일: 256px은 PNG, 그보다 작은 크기는 DIB로 넣는다 (가장 호환성이 좋은 조합)
$images = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
    }
    else {
        [byte[]]$bytes = ConvertTo-IconDib $bmp
    }
    $bmp.Dispose()
    , @{ Size = $s; Data = $bytes }
}

$fs = [System.IO.File]::Create($outIco)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$img.Data.Length); $w.Write([UInt32]$offset)
    $offset += $img.Data.Length
}
foreach ($img in $images) { $w.Write([byte[]]$img.Data) }
$w.Close()

"아이콘 생성: $outIco ($([Math]::Round((Get-Item $outIco).Length / 1KB)) KB, 크기 $($sizes -join ', '))"
