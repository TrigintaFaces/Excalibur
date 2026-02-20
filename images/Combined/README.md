# Combined Excalibur.Dispatch Brand Assets

This folder contains the combined lockup assets that feature both **Excalibur** and **Dispatch** branding together.

## Files

### Horizontal Lockups

| File                       | Size     | Use Case                               |
| -------------------------- | -------- | -------------------------------------- |
| `lockup-horizontal.svg`    | 1200x200 | Main lockup (dark background)          |
| `lockup-horizontal-light.svg` | 1200x200 | Main lockup (light background)      |
| `lockup-monochrome.svg`    | 1200x200 | Single-color lockup (inherits color)   |

### Stacked Lockups

| File                    | Size    | Use Case                                |
| ----------------------- | ------- | --------------------------------------- |
| `lockup-stacked.svg`    | 512x512 | Square format (dark background)         |
| `lockup-stacked-light.svg` | 512x512 | Square format (light background)     |

## Design Specifications

### Horizontal Lockup (1200x200)

- **Left side**: Excalibur crystalline sword icon + "EXCALIBUR" text (blue: `#3b82f6`)
- **Center**: Dot separator
- **Right side**: Dispatch paper airplane trio + "DISPATCH" text (cyan: `#00d4ff`)

### Stacked Lockup (512x512)

- **Top**: Both icons side by side (sword left, planes right)
- **Middle**: "EXCALIBUR" text (blue)
- **Center**: Dot separator
- **Below**: "DISPATCH" text (cyan)
- **Bottom**: Tagline "LEGENDARY .NET FRAMEWORK"

## Color Palette

| Element       | Dark BG       | Light BG      |
| ------------- | ------------- | ------------- |
| Excalibur     | `#3b82f6`     | `#2563eb`     |
| Dispatch      | `#00d4ff`     | `#0891b2`     |
| Separator     | `#6b7280`     | `#9ca3af`     |
| Background    | `#1a1a2e`     | `#ffffff`     |

## Usage Guidelines

### When to Use Combined Lockup

- Repository landing pages (GitHub, NuGet)
- Documentation site headers
- Presentations covering the full Excalibur ecosystem
- Marketing materials for the combined framework

### When to Use Individual Logos

- When referring to only Dispatch (messaging)
- When referring to only Excalibur (domain/persistence)
- NuGet package icons (use package-specific logo)
- Favicon (use Dispatch or Excalibur individually)

### Minimum Clear Space

- Horizontal: 20px on all sides
- Stacked: 30px on all sides

### Minimum Size

- Horizontal: 400px wide minimum
- Stacked: 128px wide minimum

## Converting to PNG

```bash
# Using Inkscape
inkscape lockup-horizontal.svg --export-type=png --export-width=1200 --export-filename=lockup-horizontal.png
inkscape lockup-stacked.svg --export-type=png --export-width=512 --export-filename=lockup-stacked.png

# Using ImageMagick
convert -background none -density 300 lockup-horizontal.svg lockup-horizontal.png
convert -background none -density 300 lockup-stacked.svg lockup-stacked.png
```

## Monochrome Version

The `lockup-monochrome.svg` uses `currentColor` and can be styled via CSS:

```css
.lockup {
  color: #000000; /* Black */
}

.lockup-inverted {
  color: #ffffff; /* White */
}
```

## Design Philosophy

The combined lockup represents the full Excalibur framework:

- **Excalibur** (sword): Domain modeling, event sourcing, persistence
- **Dispatch** (planes): Fast messaging, pipelines, middleware

Together they form a complete .NET application framework.

## License

These brand assets are part of the Excalibur project. Use according to project license.
