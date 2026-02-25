# Excalibur Brand Assets

This folder contains all brand assets for the Excalibur .NET framework, based on Concept 2 Crystal V2 (crystalline geometric blade).

## Color Palette

- **Primary Blue**: `#3b82f6` (main brand color)
- **Light Blue**: `#60a5fa` (highlights)
- **Sky Blue**: `#93c5fd` (center highlights)
- **Pale Blue**: `#e0f2fe` (core light)
- **Cyan Primary**: `#0ea5e9` (guard, pommel)
- **Cyan Light**: `#06b6d4`, `#67e8f9`, `#22d3ee` (accents)
- **Dark Background**: `#1a1a2e` (primary dark bg)
- **Dark Grip**: `#0f172a`, `#1e293b`, `#334155` (grip tones)

## Files

### Main Logos

| File                  | Size    | Use Case                               |
| --------------------- | ------- | -------------------------------------- |
| `logo.svg`            | 512x512 | Main logo with text (dark background)  |
| `logo-light.svg`      | 512x512 | Main logo with text (light background) |
| `logo-horizontal.svg` | 800x200 | Horizontal layout for headers/footers  |

### Icons

| File                   | Size    | Use Case                           |
| ---------------------- | ------- | ---------------------------------- |
| `icon.svg`             | 512x512 | Icon only (dark background)        |
| `icon-transparent.svg` | 512x512 | Icon only (transparent background) |
| `favicon.svg`          | 32x32   | Favicon (optimized for small size) |

### NuGet Package Icons

| File                 | Size    | Use Case                         |
| -------------------- | ------- | -------------------------------- |
| `nuget-icon-128.svg` | 128x128 | NuGet package icon (recommended) |
| `nuget-icon-64.svg`  | 64x64   | NuGet package icon (legacy)      |

### Social Media & Marketing

| File                | Size     | Use Case                        |
| ------------------- | -------- | ------------------------------- |
| `social-card.svg`   | 1200x630 | Twitter/Facebook/LinkedIn cards |
| `github-banner.svg` | 1280x640 | GitHub repository header        |
| `readme-banner.svg` | 1600x400 | README header banner            |

## Converting SVG to PNG/ICO

All files are in SVG format for maximum quality and scalability. Convert to PNG/ICO as needed:

### Using Inkscape (Recommended)

```bash
# Install Inkscape first: https://inkscape.org/

# Convert to PNG at specific size
inkscape logo.svg --export-type=png --export-width=512 --export-filename=logo.png

# Batch convert all logos
inkscape logo.svg -w 512 -o logo-512.png
inkscape logo.svg -w 256 -o logo-256.png
inkscape logo.svg -w 128 -o logo-128.png
inkscape icon.svg -w 64 -o icon-64.png
inkscape icon.svg -w 32 -o icon-32.png
inkscape icon.svg -w 16 -o icon-16.png
```

### Using ImageMagick

```bash
# Convert SVG to PNG
convert -background none -density 300 icon-transparent.svg -resize 512x512 icon-512.png

# Create favicon.ico (multi-size)
convert icon-transparent.svg -background none -define icon:auto-resize=16,32,48,64 favicon.ico
```

### Using Online Tools

- [Convertio](https://convertio.co/svg-png/) - SVG to PNG converter
- [CloudConvert](https://cloudconvert.com/svg-to-png) - SVG to PNG converter
- [RealFaviconGenerator](https://realfavicongenerator.net/) - Generate all favicon formats

## Usage Guidelines

### NuGet Package Icon

1. Convert `nuget-icon-128.svg` to PNG (128x128)
2. Add to your `.csproj`:

   ```xml
   <PropertyGroup>
     <PackageIcon>icon.png</PackageIcon>
   </PropertyGroup>
   <ItemGroup>
     <None Include="..\..\images\Excalibur\nuget-icon-128.png" Pack="true" PackagePath="\" />
   </ItemGroup>
   ```

### GitHub Repository

1. Convert `github-banner.svg` to PNG (1280x640)
2. Upload to repository
3. Reference in README:

   ```markdown
   ![Excalibur Banner](./images/Excalibur/github-banner.png)
   ```

### Documentation Site

Use `logo.svg` or `logo-horizontal.svg` depending on layout. SVG is preferred for documentation sites as it scales perfectly.

### Social Media

Convert `social-card.svg` to PNG (1200x630) and use as:

- Twitter Card (`twitter:image`)
- Facebook Open Graph (`og:image`)
- LinkedIn preview image

## Recommended PNG Exports

Create these PNG files for common use cases:

```bash
# Main logos
logo-512.png          # From logo.svg
logo-light-512.png    # From logo-light.svg
logo-horizontal.png   # From logo-horizontal.svg (800x200)

# Icons
icon-512.png          # From icon-transparent.svg
icon-256.png          # From icon-transparent.svg
icon-128.png          # From icon-transparent.svg
icon-64.png           # From icon-transparent.svg
icon-32.png           # From icon-transparent.svg
icon-16.png           # From icon-transparent.svg

# NuGet
nuget-icon-128.png    # From nuget-icon-128.svg
nuget-icon-64.png     # From nuget-icon-64.svg

# Social/Marketing
social-card.png       # From social-card.svg (1200x630)
github-banner.png     # From github-banner.svg (1280x640)
readme-banner.png     # From readme-banner.svg (1600x400)

# Favicon
favicon.ico           # Multi-size ICO from icon-transparent.svg (16,32,48,64)
```

## File Structure After PNG Export

```
Excalibur/
├── README.md                   # This file
├── logo.svg                    # Main logo (dark bg)
├── logo-light.svg              # Main logo (light bg)
├── logo-horizontal.svg         # Horizontal layout
├── icon.svg                    # Icon (dark bg)
├── icon-transparent.svg        # Icon (no bg)
├── favicon.svg                 # Favicon source
├── nuget-icon-128.svg          # NuGet icon (128x128)
├── nuget-icon-64.svg           # NuGet icon (64x64)
├── social-card.svg             # Social media card
├── github-banner.svg           # GitHub banner
├── readme-banner.svg           # README banner
├── convert-to-png.ps1          # PowerShell conversion script
├── convert-to-png.sh           # Bash conversion script
└── png/                        # PNG exports (create after conversion)
    ├── logo-512.png
    ├── logo-light-512.png
    ├── icon-512.png
    ├── icon-256.png
    ├── icon-128.png
    ├── icon-64.png
    ├── icon-32.png
    ├── icon-16.png
    ├── nuget-icon-128.png
    ├── social-card.png
    ├── github-banner.png
    ├── readme-banner.png
    └── favicon.ico
```

## Design Notes

**Concept**: Crystalline geometric sword blade representing legendary power and modern technology.

**Visual Elements**:

- Geometric faceted blade with layered transparency
- Angular tech-modern aesthetic
- Clean lines and sharp angles
- Segmented grip for tech detail
- Energy core in pommel
- Without outer blade edges (V2) for cleaner silhouette

**Scale**:

- Full logos: proportional to 512x512 canvas
- Icons: optimized for clarity
- Favicon: simplified for 32x32 recognition

**Typography**:

- Font: Arial, sans-serif (web-safe, clean, modern)
- Text: "EXCALIBUR" (bold, letter-spacing: 3-4px)
- Tagline: "LEGENDARY .NET FRAMEWORK" (lighter weight, letter-spacing: 3-4px)

## Brand Synergy with Dispatch

Excalibur uses a **blue palette** (`#3b82f6` family) that complements Dispatch's **cyan palette** (`#00d4ff` family):

- **Dispatch**: Paper airplanes, cyan tones, message flow
- **Excalibur**: Crystalline sword, blue tones, legendary power

Together they form a cohesive brand family with distinct but complementary identities.

## Design Philosophy

**Excalibur represents**:

- **Legendary Heritage**: Sword imagery connects to Arthurian legend
- **Modern Technology**: Geometric crystalline design speaks to cutting-edge .NET
- **Power & Precision**: Angular facets suggest sharpness and capability
- **Clean Sophistication**: Refined edges create professional appearance

**Perfect for**:

- .NET framework positioning
- Developer tool branding
- Enterprise software identity
- Technical excellence messaging

## License

These brand assets are part of the Excalibur project. Use according to project license.
