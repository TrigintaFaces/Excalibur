# API Reference Documentation

This directory contains the API reference documentation infrastructure for Excalibur.Dispatch.

## Overview

API documentation is generated from XML documentation comments in the source code using [DocFX](https://dotnet.github.io/docfx/).

## Structure

```
docs/api/
├── docfx.json           # DocFX configuration
├── filterConfig.yml     # API filtering rules
├── toc.yml              # Table of contents
├── index.md             # API reference landing page
├── generate-api-docs.ps1 # Generation script (Windows)
├── README.md            # This file
├── versioning/          # Manually written versioning API docs
├── api-dispatch/        # Generated Dispatch API (after build)
├── api-excalibur/       # Generated Excalibur API (after build)
└── _site/               # Generated static site (after build)
```

## Prerequisites

1. **.NET 9 SDK** - Required for building the solution
2. **DocFX** - Documentation generator

### Install DocFX

```bash
dotnet tool install -g docfx
```

## Generating Documentation

### Windows (PowerShell)

```powershell
# Generate documentation
.\generate-api-docs.ps1

# Generate and serve locally
.\generate-api-docs.ps1 -Serve

# Clean and regenerate
.\generate-api-docs.ps1 -Clean
```

### Cross-Platform (Command Line)

```bash
# Navigate to docs/api
cd docs/api

# Build solution first (generates XML docs)
dotnet build ../../ -c Release

# Generate API metadata
docfx metadata docfx.json

# Build static site
docfx build docfx.json

# Serve locally (optional)
docfx docfx.json --serve
```

## Output

After generation, the `_site/` directory contains the complete static documentation site:

- `_site/api-dispatch/` - Dispatch framework API reference
- `_site/api-excalibur/` - Excalibur framework API reference
- `_site/index.html` - Landing page

## Configuration

### docfx.json

The main configuration file controls:
- **metadata** - Which projects to document
- **build** - Output format and templates
- **globalMetadata** - Site-wide settings

### filterConfig.yml

Controls API visibility:
- Include all public `Dispatch.*` and `Excalibur.*` types
- Exclude `*.Internal.*`, `*.Generated.*`, `*.Tests.*` namespaces
- Include DI extension methods in `Microsoft.Extensions.DependencyInjection`

## Customization

### Adding Conceptual Documentation

Add markdown files to be included alongside API reference:

```yaml
# In docfx.json build.content
{
  "files": ["concepts/*.md"]
}
```

### Overwriting API Documentation

Create markdown files in `apidoc/` to extend generated documentation:

```markdown
---
uid: Excalibur.Dispatch.IDispatcher
summary: *content
---

Additional conceptual documentation for IDispatcher...
```

### Custom Templates

Modify the template by adding custom files:

```yaml
# In docfx.json
"template": ["default", "modern", "templates/custom"]
```

## CI/CD Integration

### GitHub Actions Example

```yaml
- name: Generate API Docs
  run: |
    dotnet tool install -g docfx
    cd docs/api
    docfx docfx.json

- name: Deploy to GitHub Pages
  uses: peaceiris/actions-gh-pages@v3
  with:
    github_token: ${{ secrets.GITHUB_TOKEN }}
    publish_dir: ./docs/api/_site
```

### Azure Pipelines Example

```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: custom
    custom: tool
    arguments: install -g docfx

- script: |
    cd docs/api
    docfx docfx.json
  displayName: 'Generate API Documentation'
```

## Troubleshooting

### Common Issues

1. **"docfx not found"**
   - Run: `dotnet tool install -g docfx`
   - Ensure `~/.dotnet/tools` is in PATH

2. **Build errors during metadata generation**
   - Ensure solution builds: `dotnet build -c Release`
   - Check for missing project references

3. **Missing XML documentation**
   - Verify `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in .csproj

4. **Empty output**
   - Check filterConfig.yml isn't excluding too much
   - Verify public types have XML comments

## See Also

- [DocFX Documentation](https://dotnet.github.io/docfx/)
- [XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/)
- [Developer Guides](../guides/README.md)
