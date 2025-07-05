# PowerShell script to create a simple ICO file for ChromeGuard
Add-Type -AssemblyName System.Drawing

# Create a 32x32 bitmap
$bitmap = New-Object System.Drawing.Bitmap(32, 32)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Set high quality rendering
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Create brushes and pens
$blueBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(33, 150, 243))
$whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$darkBluePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(25, 118, 210), 2)

# Draw background circle
$graphics.FillEllipse($blueBrush, 2, 2, 28, 28)

# Draw shield shape (simplified as rectangle with rounded corners)
$shieldRect = New-Object System.Drawing.Rectangle(8, 6, 16, 20)
$graphics.FillRectangle($whiteBrush, $shieldRect)

# Draw Chrome-like symbol inside shield
$centerX = 16
$centerY = 16
$graphics.DrawEllipse($darkBluePen, $centerX - 4, $centerY - 4, 8, 8)
$graphics.FillEllipse($blueBrush, $centerX - 1, $centerY - 1, 2, 2)

# Save as ICO file
$iconPath = "c:\git\ChromeGuard\chromeguard.ico"
try {
    # Convert bitmap to icon
    $icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
    $fileStream = [System.IO.File]::Create($iconPath)
    $icon.Save($fileStream)
    $fileStream.Close()
    Write-Host "Icon created successfully at: $iconPath"
} catch {
    # Fallback: save as PNG and note that ICO conversion is needed
    $bitmap.Save("c:\git\ChromeGuard\chromeguard.png", [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Created PNG file - manual ICO conversion may be needed"
}

# Cleanup
$graphics.Dispose()
$bitmap.Dispose()
$blueBrush.Dispose()
$whiteBrush.Dispose()
$darkBluePen.Dispose()
