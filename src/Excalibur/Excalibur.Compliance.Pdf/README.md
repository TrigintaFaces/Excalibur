# Excalibur.Compliance.Pdf

Opt-in PDF export for SOC 2 reports. `Excalibur.Compliance` exports JSON, CSV, XML, Excel and text on
its own; install this package only when you also need `ExportFormat.Pdf`.

## Licence obligation - read before installing

This package depends on **QuestPDF**, which is **not** MIT-licensed. Its Community edition is free only
for licensees below **USD 1,000,000 total annual gross revenue, measured company-wide**. Above that
threshold a paid QuestPDF licence is required. See <https://www.questpdf.com/license/>.

That obligation is why PDF export lives here instead of in `Excalibur.Compliance`: a consumer who does
not export PDFs never takes the dependency.

## Installation

```bash
dotnet add package Excalibur.Compliance.Pdf
```

## Quick Start

```csharp
// The hosting application selects the QuestPDF licence. This package never writes
// QuestPDF.Settings.License, because it is a process-global static: writing it would overwrite a
// Professional or Enterprise licence you configured, and would assert Community eligibility on your
// behalf. With no licence selected, QuestPDF throws when a PDF is rendered.
QuestPDF.Settings.License = LicenseType.Community;

services.AddSoc2Compliance();
services.AddSoc2PdfExport();
```

Without `AddSoc2PdfExport()`, `ISoc2ReportExporter.GetSupportedFormats()` omits `ExportFormat.Pdf` and a
PDF export request is rejected with a message naming this package. Every other format is unaffected.

## Native AOT

The PDF export path is **not** validated under Native AOT. QuestPDF ships no trim annotations or ILLink
descriptors, and this repository's AOT publish validation does not cover this package.

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
