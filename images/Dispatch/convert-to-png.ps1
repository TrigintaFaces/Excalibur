# Convert SVG assets to PNG files
# Requires Inkscape to be installed: https://inkscape.org/
#
# Usage: .\convert-to-png.ps1

$ErrorActionPreference = "Stop"

# Check if Inkscape is installed
$inkscape = Get-Command inkscape -ErrorAction SilentlyContinue
if (-not $inkscape) {
    Write-Error "Inkscape is not installed. Please install from https://inkscape.org/"
    exit 1
}

Write-Host "Converting SVG files to PNG..." -ForegroundColor Cyan

# Create png directory
$pngDir = ".\png"
if (-not (Test-Path $pngDir)) {
    New-Item -ItemType Directory -Path $pngDir | Out-Null
    Write-Host "Created png directory" -ForegroundColor Green
}

# Main logos
Write-Host "`nConverting main logos..." -ForegroundColor Yellow
inkscape logo.svg -w 512 -o "$pngDir\logo-512.png"
inkscape logo-light.svg -w 512 -o "$pngDir\logo-light-512.png"
inkscape logo-horizontal.svg -w 800 -o "$pngDir\logo-horizontal.png"

# Icons at multiple sizes
Write-Host "`nConverting icons..." -ForegroundColor Yellow
inkscape icon-transparent.svg -w 512 -o "$pngDir\icon-512.png"
inkscape icon-transparent.svg -w 256 -o "$pngDir\icon-256.png"
inkscape icon-transparent.svg -w 128 -o "$pngDir\icon-128.png"
inkscape icon-transparent.svg -w 64 -o "$pngDir\icon-64.png"
inkscape icon-transparent.svg -w 32 -o "$pngDir\icon-32.png"
inkscape icon-transparent.svg -w 16 -o "$pngDir\icon-16.png"

# NuGet icons
Write-Host "`nConverting NuGet icons..." -ForegroundColor Yellow
inkscape nuget-icon-128.svg -w 128 -o "$pngDir\nuget-icon-128.png"
inkscape nuget-icon-64.svg -w 64 -o "$pngDir\nuget-icon-64.png"

# Social media and marketing
Write-Host "`nConverting social media assets..." -ForegroundColor Yellow
inkscape social-card.svg -w 1200 -o "$pngDir\social-card.png"
inkscape github-banner.svg -w 1280 -o "$pngDir\github-banner.png"
inkscape readme-banner.svg -w 1600 -o "$pngDir\readme-banner.png"

# Favicon (simple PNG export, ICO needs ImageMagick)
Write-Host "`nConverting favicon..." -ForegroundColor Yellow
inkscape favicon.svg -w 32 -o "$pngDir\favicon-32.png"
inkscape favicon.svg -w 16 -o "$pngDir\favicon-16.png"

Write-Host "`n✓ Conversion complete!" -ForegroundColor Green
Write-Host "PNG files saved to: $pngDir" -ForegroundColor Cyan

# Check for ImageMagick to create favicon.ico
$magick = Get-Command magick -ErrorAction SilentlyContinue
if ($magick) {
    Write-Host "`nCreating favicon.ico..." -ForegroundColor Yellow
    magick convert "$pngDir\favicon-16.png" "$pngDir\favicon-32.png" "$pngDir\favicon.ico"
    Write-Host "✓ favicon.ico created" -ForegroundColor Green
} else {
    Write-Host "`nImageMagick not found. Skipping favicon.ico creation." -ForegroundColor DarkYellow
    Write-Host "To create favicon.ico, install ImageMagick: https://imagemagick.org/" -ForegroundColor DarkYellow
    Write-Host "Then run: magick convert png\favicon-16.png png\favicon-32.png png\favicon.ico" -ForegroundColor DarkYellow
}

Write-Host "`nDone! All assets are ready for use." -ForegroundColor Green
