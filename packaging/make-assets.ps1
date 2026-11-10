# Draws the Store tile/logo PNGs (same design as FocusGlance\app.ico) into packaging\Assets.
param([string]$Out = "$PSScriptRoot\Assets")
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Out | Out-Null

function Draw-Icon($g, [float]$x, [float]$y, [float]$s) {
  $k = $s / 32.0
  $rect = New-Object System.Drawing.RectangleF ($x + 1*$k), ($y + 1*$k), (30*$k), (30*$k)
  $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 52, 64, 92)), ([System.Drawing.Color]::FromArgb(255, 14, 16, 26)), 90
  $g.FillEllipse($br, $rect)
  $rim = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(220, 190, 228, 255)), ([Math]::Max(1, 1.5*$k))
  $g.DrawEllipse($rim, ($x + 2*$k), ($y + 2*$k), (28*$k), (28*$k))
  $hand = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.2, 2.6*$k))
  $hand.StartCap = 'Round'; $hand.EndCap = 'Round'
  $g.DrawLine($hand, ($x + 16*$k), ($y + 16*$k), ($x + 16*$k), ($y + 8*$k))
  $g.DrawLine($hand, ($x + 16*$k), ($y + 16*$k), ($x + 22*$k), ($y + 19*$k))
}

# name, width, height, icon size as a fraction of the height
function Save-Asset([string]$name, [int]$w, [int]$h, [double]$frac) {
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
  $s = [float]($h * $frac)
  Draw-Icon $g (($w - $s) / 2) (($h - $s) / 2) $s
  $g.Dispose()
  $bmp.Save((Join-Path $Out $name), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

foreach ($sc in 100, 125, 150, 200, 400) {
  $f = $sc / 100
  Save-Asset "Square44x44Logo.scale-$sc.png"   ([int](44 * $f))  ([int](44 * $f))  1.0
  Save-Asset "Square150x150Logo.scale-$sc.png" ([int](150 * $f)) ([int](150 * $f)) 0.6
  Save-Asset "Wide310x150Logo.scale-$sc.png"   ([int](310 * $f)) ([int](150 * $f)) 0.6
  Save-Asset "StoreLogo.scale-$sc.png"         ([int](50 * $f))  ([int](50 * $f))  1.0
}
# Taskbar / Start list icons without the tile plate
foreach ($t in 16, 24, 32, 48, 256) {
  Save-Asset "Square44x44Logo.targetsize-$t.png" $t $t 1.0
  Save-Asset "Square44x44Logo.targetsize-${t}_altform-unplated.png" $t $t 1.0
}
Write-Host "Assets written to $Out"
