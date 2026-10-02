$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$assets=Join-Path $PSScriptRoot '../src/BVELauncher.App/Assets'
function New-TrainImage([int]$width,[int]$height,[string]$path) {
    $image=[Drawing.Bitmap]::new($width,$height)
    $graphics=[Drawing.Graphics]::FromImage($image)
    $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $size=[Math]::Min($width,$height);$scale=$size/256.0
    $graphics.TranslateTransform(($width-$size)/2,($height-$size)/2);$graphics.ScaleTransform($scale,$scale)
    $body=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(30,73,97));$light=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(243,247,250));$pen=[Drawing.Pen]::new($body.Color,12)
    $graphics.FillRectangle($body,58,36,140,158);$graphics.FillRectangle($light,76,58,104,62)
    $graphics.FillEllipse($light,78,148,20,20);$graphics.FillEllipse($light,158,148,20,20)
    $graphics.DrawLine($pen,90,194,62,226);$graphics.DrawLine($pen,166,194,194,226);$graphics.DrawLine($pen,76,213,180,213)
    $image.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose();$body.Dispose();$light.Dispose();$pen.Dispose();$image.Dispose()
}
foreach($entry in @(@('Square44x44Logo.scale-200.png',88,88),@('Square150x150Logo.scale-200.png',300,300),@('StoreLogo.png',50,50),@('LockScreenLogo.scale-200.png',48,48),@('Wide310x150Logo.scale-200.png',620,300),@('SplashScreen.scale-200.png',1240,600),@('Square44x44Logo.targetsize-24_altform-unplated.png',24,24),@('Square44x44Logo.targetsize-48_altform-lightunplated.png',48,48))){New-TrainImage $entry[1] $entry[2] (Join-Path $assets $entry[0])}
$png=Join-Path $assets 'Icon256.png';New-TrainImage 256 256 $png
$bytes=[IO.File]::ReadAllBytes($png);$stream=[IO.File]::Create((Join-Path $assets 'AppIcon.ico'));$writer=[IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$bytes.Length);$writer.Write([uint32]22);$writer.Write($bytes);$writer.Dispose()
