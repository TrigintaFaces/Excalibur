# Excalibur.Dispatch Brand Guidelines

This document defines the visual identity for the Excalibur documentation site and all related materials. The site covers the **Excalibur** framework — one framework with focused package families for messaging (`Excalibur.Dispatch.*`), domain modeling (`Excalibur.Domain`), event sourcing (`Excalibur.EventSourcing.*`), and more.

---

## Brand Relationship

```
Excalibur.Dispatch
├── Dispatch     - Fast .NET Messaging (cyan palette)
└── Excalibur    - Legendary .NET Framework (blue palette)
```

Both projects share a cohesive brand family with distinct but complementary identities.

---

## Color Palette

### Dispatch Colors (Primary for Messaging)

| Name | Hex | RGB | Usage |
|------|-----|-----|-------|
| **Dispatch Primary** | `#00d4ff` | rgb(0, 212, 255) | Primary brand color, links, CTAs |
| **Dispatch Light** | `#5de4ff` | rgb(93, 228, 255) | Highlights, dark mode primary |
| **Dispatch Dark** | `#00a8cc` | rgb(0, 168, 204) | Hover states |
| **Dispatch Darkest** | `#0091b3` | rgb(0, 145, 179) | Active states |

### Excalibur Colors (Secondary for Framework)

| Name | Hex | RGB | Usage |
|------|-----|-----|-------|
| **Excalibur Primary** | `#3b82f6` | rgb(59, 130, 246) | Secondary brand color |
| **Excalibur Light** | `#60a5fa` | rgb(96, 165, 250) | Dark mode secondary |
| **Excalibur Lighter** | `#93c5fd` | rgb(147, 197, 253) | Subtle highlights |
| **Excalibur Dark** | `#2563eb` | rgb(37, 99, 235) | Hover states |
| **Excalibur Darker** | `#1d4ed8` | rgb(29, 78, 216) | Active states |

### Background Colors

| Name | Hex | RGB | Usage |
|------|-----|-----|-------|
| **Dark Background** | `#1a1a2e` | rgb(26, 26, 46) | Hero, footer (dark bg) |
| **Darker Background** | `#0f172a` | rgb(15, 23, 42) | Dark mode page bg |
| **Surface Dark** | `#1e293b` | rgb(30, 41, 59) | Dark mode cards |
| **Surface Light** | `#f8fafc` | rgb(248, 250, 252) | Light mode sections |

### Semantic Colors

| Name | Hex | Usage |
|------|-----|-------|
| **Success** | `#22c55e` | Success messages, "after" code examples |
| **Warning** | `#f59e0b` | Warning callouts |
| **Error** | `#ef4444` | Error messages, "before" code examples |
| **Info** | `#3b82f6` | Info callouts (uses Excalibur primary) |

---

## CSS Custom Properties

```css
:root {
  /* Dispatch brand colors (cyan family) */
  --dispatch-primary: #00d4ff;
  --dispatch-light: #5de4ff;
  --dispatch-dark: #00a8cc;

  /* Excalibur brand colors (blue family) */
  --excalibur-primary: #3b82f6;
  --excalibur-light: #60a5fa;
  --excalibur-lighter: #93c5fd;
  --excalibur-dark: #2563eb;
  --excalibur-darker: #1d4ed8;

  /* Shared backgrounds */
  --brand-bg-dark: #1a1a2e;
  --brand-bg-darker: #0f172a;

  /* Docusaurus/Infima mappings - Dispatch cyan as primary */
  --ifm-color-primary: #00d4ff;
  --ifm-color-primary-dark: #00bfe6;
  --ifm-color-primary-darker: #00a8cc;
  --ifm-color-primary-darkest: #0091b3;
  --ifm-color-primary-light: #33dcff;
  --ifm-color-primary-lighter: #66e5ff;
  --ifm-color-primary-lightest: #99edff;

  /* Secondary - Excalibur blue */
  --ifm-color-secondary: #3b82f6;
}

[data-theme='dark'] {
  --ifm-color-primary: #5de4ff;
  --ifm-color-primary-dark: #00d4ff;
  --ifm-color-primary-darker: #00bfe6;
  --ifm-color-primary-darkest: #00a8cc;
  --ifm-color-primary-light: #7aebff;
  --ifm-color-primary-lighter: #99f0ff;
  --ifm-color-primary-lightest: #b8f5ff;

  --ifm-background-color: #0f172a;
  --ifm-background-surface-color: #1e293b;
}
```

---

## Typography

### Font Families

| Type | Font | Fallback Stack | Usage |
|------|------|----------------|-------|
| **Primary** | Inter | system-ui, -apple-system, Segoe UI, Roboto, sans-serif | All body text, headings |
| **Monospace** | JetBrains Mono | SFMono-Regular, Menlo, Monaco, Consolas, monospace | Code blocks, inline code |

### Font Scale

| Element | Size | Weight | Line Height |
|---------|------|--------|-------------|
| h1 | 3.5rem (56px) | 700 | 1.2 |
| h2 | 2rem (32px) | 600 | 1.3 |
| h3 | 1.5rem (24px) | 600 | 1.4 |
| h4 | 1.25rem (20px) | 600 | 1.4 |
| Body | 1rem (16px) | 400 | 1.6 |
| Small | 0.875rem (14px) | 400 | 1.5 |
| Code | 0.875rem (14px) | 400 | 1.7 |

### CSS Import

```css
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
@import url('https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500&display=swap');
```

---

## Logo Assets

### Source Files Location

```
images/
├── Dispatch/          # Dispatch-specific assets
│   ├── logo.svg       # Main logo (dark bg)
│   ├── logo-light.svg # Main logo (light bg)
│   ├── logo-horizontal.svg
│   ├── icon.svg
│   ├── icon-transparent.svg
│   ├── favicon.svg
│   ├── nuget-icon-128.svg
│   ├── social-card.svg
│   └── github-banner.svg
│
└── Excalibur/         # Excalibur-specific assets
    ├── logo.svg
    ├── logo-light.svg
    ├── logo-horizontal.svg
    ├── icon.svg
    ├── icon-transparent.svg
    ├── favicon.svg
    ├── nuget-icon-128.svg
    ├── social-card.svg
    └── github-banner.svg
```

### Documentation Site Assets (static/img/)

Copy and convert as needed:

| Asset | Source | Destination |
|-------|--------|-------------|
| Navbar Logo | `images/Dispatch/logo-horizontal.svg` | `static/img/logo.svg` |
| Navbar Logo (dark) | `images/Dispatch/logo-horizontal.svg` | `static/img/logo-dark.svg` |
| Favicon | `images/Dispatch/favicon.svg` | `static/img/favicon.ico` |
| Social Card | `images/Dispatch/social-card.svg` | `static/img/social-card.png` |

### Logo Usage Guidelines

#### Clear Space

Maintain minimum clear space equal to the height of the "D" in "DISPATCH" on all sides of the logo.

#### Minimum Sizes

| Logo Type | Minimum Width |
|-----------|---------------|
| Full Logo | 200px |
| Horizontal Logo | 160px |
| Icon Only | 32px |
| Favicon | 16px |

#### Don'ts

- Don't rotate the logo
- Don't change logo colors outside the brand palette
- Don't stretch or distort the logo
- Don't add effects (shadows, outlines, etc.)
- Don't place on busy backgrounds without sufficient contrast

---

## Dark/Light Mode

### Theme Detection

Use Docusaurus's built-in theme detection:

```css
/* Light mode (default) */
.component {
  background: var(--ifm-background-color);
  color: var(--ifm-font-color-base);
}

/* Dark mode */
[data-theme='dark'] .component {
  background: var(--ifm-background-surface-color);
}
```

### Contrast Requirements

All text must meet WCAG AA contrast requirements:

| Context | Minimum Ratio |
|---------|---------------|
| Normal text | 4.5:1 |
| Large text (18px+) | 3:1 |
| UI components | 3:1 |

### Color Adjustments by Theme

| Element | Light Mode | Dark Mode |
|---------|------------|-----------|
| Primary | `#00d4ff` | `#5de4ff` |
| Background | `#ffffff` | `#0f172a` |
| Surface | `#f8fafc` | `#1e293b` |
| Text Primary | `#1e293b` | `#f8fafc` |
| Text Secondary | `#475569` | `#94a3b8` |

---

## Component Styling

### Feature Cards

```css
.feature-card {
  background: var(--ifm-card-background-color);
  border-radius: 12px;
  padding: 2rem;
  box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1);
  transition: transform 0.2s, box-shadow 0.2s;
}

.feature-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 10px 15px -3px rgb(0 0 0 / 0.1);
}
```

### CTA Buttons

```css
.cta-button--primary {
  background: var(--dispatch-primary);
  color: var(--brand-bg-dark);
  padding: 0.875rem 2rem;
  border-radius: 8px;
  font-weight: 600;
}

.cta-button--secondary {
  background: transparent;
  color: #ffffff;
  border: 2px solid rgba(255, 255, 255, 0.3);
}
```

### Badges

```css
.badge {
  display: inline-flex;
  padding: 0.25rem 0.75rem;
  background-color: rgba(0, 212, 255, 0.1);
  border-radius: 9999px;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--dispatch-primary);
}
```

---

## Design Concepts

### Dispatch Identity

**Concept**: Three paper airplanes in sequence representing message dispatch/delivery.

**Visual Elements**:
- Three planes with varying opacity (0.35, 0.65, 1.0) create depth
- Left-pointing direction (message flow)
- Paper airplane shape: two triangular wings + center line

**Key Message**: Fast, type-safe .NET messaging

### Excalibur Identity

**Concept**: Crystalline geometric sword blade representing legendary power and modern technology.

**Visual Elements**:
- Geometric faceted blade with layered transparency
- Angular tech-modern aesthetic
- Clean lines and sharp angles
- Energy core in pommel

**Key Message**: Legendary .NET framework for domain-driven design

---

## Accessibility

### Color Accessibility

- Never rely on color alone to convey information
- Use icons or text labels alongside color indicators
- Test with color blindness simulators

### Focus States

```css
:focus-visible {
  outline: 2px solid var(--dispatch-primary);
  outline-offset: 2px;
}
```

### Motion Preferences

```css
@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
  }
}
```

---

## File References

| Resource | Location |
|----------|----------|
| CSS Variables | `src/css/custom.css` |
| Dispatch Brand Assets | `images/Dispatch/README.md` |
| Excalibur Brand Assets | `images/Excalibur/README.md` |
| Docusaurus Config | `docusaurus.config.ts` |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-03 | Initial brand guidelines for Sprint 68 |

---

*Excalibur.Dispatch Brand Guidelines - Sprint 68*
