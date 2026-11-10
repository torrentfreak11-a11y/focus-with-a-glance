param([string]$Out = "$PSScriptRoot\app.ico")
Add-Type -AssemblyName System.Drawing
$sizes = 256, 48, 32, 16
$pngs = @()
foreach ($s in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap $s, $s
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([System.Drawing.Color]::Transparent)
  $k = $s / 32.0
  $rect = New-Object System.Drawing.RectangleF (1*$k), (1*$k), (30*$k), (30*$k)
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddEllipse($rect)
  $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 52, 64, 92)), ([System.Drawing.Color]::FromArgb(255, 14, 16, 26)), 90
  $g.FillPath($br, $path)
  $rim = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(220, 190, 228, 255)), ([Math]::Max(1, 1.5*$k))
  $g.DrawEllipse($rim, (2*$k), (2*$k), (28*$k), (28*$k))
  $hand = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.2, 2.6*$k))
  $hand.StartCap = 'Round'; $hand.EndCap = 'Round'
  $g.DrawLine($hand, (16*$k), (16*$k), (16*$k), (8*$k))
  $g.DrawLine($hand, (16*$k), (16*$k), (22*$k), (19*$k))
  $g.Dispose()
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs += ,($ms.ToArray())
  $bmp.Dispose()
}
$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
  $w.Write([Byte]$b); $w.Write([Byte]$b); $w.Write([Byte]0); $w.Write([Byte]0)
  $w.Write([UInt16]1); $w.Write([UInt16]32)
  $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
  $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
