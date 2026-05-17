# dabbler-pdf Boundary Overview

> **Snapshot of:** `../dabbler-platform` @ commit `aa77020e1ca0` on `2026-05-16`
> **Captured on:** `2026-05-17`
> **Regenerate when:** the platform's component library changes
> meaningfully, or when starting Access-to-Blazor migration planning.
> **Regeneration trigger:** re-run `dabbler-platform-discovery`.

**PDF dependency baseline:** `../dabbler-pdf` @ commit `3df4134cb61b` on `2026-04-22` (NuGet pin `DabblerPdf` `1.1.2` per `Directory.Packages.props`).

## What `dabbler-pdf` is

`DabblerPdf` is a lightweight .NET library for generating PDF documents, ported from the Java project [OpenPDF](https://github.com/LibrePDF/OpenPDF). As a port of a copyleft-licensed project, it carries forward a dual LGPL-2.1-only OR MPL-2.0 license, which has downstream implications for consumers like `dabbler-platform`. It provides a code-first API for creating PDF content including text, shapes, and images. For a full feature list, see [`../dabbler-pdf/README.md`](../../../dabbler-pdf/README.md).

## Public surface consumed by dabbler-platform

`dabbler-platform` consumes a narrow slice of the `DabblerPdf` API for text measurement and PDF rendering. The interaction is confined to the `Dabbler.Reports.Core` and `Dabbler.Reports.Export` libraries.

| Namespace             | Type           | Members called by `dabbler-platform`                                                                 | `dabbler-platform` call site                                                                  |
|-----------------------|----------------|-------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------|
| `DabblerPdf.Document` | `Document`     | `new Document(Rectangle)`, `Open()`, `Close()`, `NewPage()`, `AddTitle(string)`, `AddAuthor(string)`  | `src/Libs/Dabbler.Reports.Export/PdfExporter.cs`                                              |
| `DabblerPdf.Document` | `Rectangle`    | `new Rectangle(double width, double height)`                                                          | `src/Libs/Dabbler.Reports.Export/PdfExporter.cs`                                              |
| `DabblerPdf.Document` | `BaseColor`    | `BaseColor.White`, `new BaseColor(int, int, int)` (via internal renderer)                             | `src/Libs/Dabbler.Reports.Export/PdfPageRenderer.cs`, `PdfStyleResolver.cs`                   |
| `DabblerPdf.Pdf`      | `PdfWriter`    | `PdfWriter.GetInstance(Document, Stream)`, `DirectContent` (returns `PdfContentByte`)                 | `src/Libs/Dabbler.Reports.Export/PdfExporter.cs`                                              |
| `DabblerPdf.Pdf`      | `PdfContentByte` | Drawing API (passed into the internal renderer)                                                     | `src/Libs/Dabbler.Reports.Export/PdfPageRenderer.cs`                                          |
| `DabblerPdf.Text`     | `BaseFont`     | `BaseFont.CreateFont(string, string, bool)`, `GetWidthPoint(string, double)`, `GetFontDescriptor(int, double)`, font-name constants (`Helvetica`, `TimesRoman`, `Courier`, etc.) | `src/Libs/Dabbler.Reports.Core/Layout/OpenPdfTextMeasurer.cs` |
| `DabblerPdf.Utilities` | `CoordinateTranslator` | `InchesToPoints(double)`, `FromTopLeftInches(double, double, double, double)`                | `src/Libs/Dabbler.Reports.Export/PdfPageRenderer.cs`, `PdfExporter.cs`                        |
| `DabblerPdf.Image`    | `Image`        | `Image.GetInstance(...)` (used inside the internal renderer)                                          | `src/Libs/Dabbler.Reports.Export/PdfPageRenderer.cs`                                          |

`PdfPageRenderer` is an `internal sealed class` within `Dabbler.Reports.Export`; consumers do not call it directly, but it is the seam through which most `PdfContentByte` and `Image` usage flows.

## How dabbler-platform calls it

The platform's interaction with `DabblerPdf` occurs in two phases: text measurement during report composition and rendering during PDF export. The implementation seam is isolated to two classes.

First, `Dabbler.Reports.Core.Layout.OpenPdfTextMeasurer` acts as the text-measurement service for the report layout engine. It uses `DabblerPdf.Text.BaseFont` to calculate character widths and line heights (`GetWidthPoint`, `GetFontDescriptor(BaseFont.ASCENT|DESCENT, fontSize)`) for one of the 14 standard PDF fonts, enabling the layout engine to correctly calculate `CanGrow`/`CanShrink` behavior for report elements. Font family is normalized to one of `Helvetica`/`Times`/`Courier` and combined with bold/italic flags to pick a specific Base 14 font name. Source: `../dabbler-platform/src/Libs/Dabbler.Reports.Core/Layout/OpenPdfTextMeasurer.cs`.

Second, `Dabbler.Reports.Export.PdfExporter` orchestrates the rendering of a composed report into a PDF byte stream. It constructs a `Rectangle` page size, instantiates a `Document` and a `PdfWriter`, calls `Open()`, conditionally writes title/author metadata via `AddTitle`/`AddAuthor`, and iterates through the composed pages — calling `NewPage()` for every page after the first and passing `writer.DirectContent` (a `PdfContentByte` instance) into an internal `PdfPageRenderer` for drawing. `Close()` finalizes the byte stream. Source: `../dabbler-platform/src/Libs/Dabbler.Reports.Export/PdfExporter.cs`. The internal `PdfPageRenderer` (same project) is the consumer of the `PdfContentByte` drawing API; its exact drawing surface area is an implementation detail of `Dabbler.Reports.Export` rather than part of the boundary contract.

## Setup the consuming repo needs

- **NuGet reference.** The package is referenced via central package management. The consumer's `.csproj` needs `<PackageReference Include="DabblerPdf" />` and the root `Directory.Packages.props` must contain the version pin (`<PackageVersion Include="DabblerPdf" Version="1.1.2" />`).
- **Version currently pinned.** `1.1.2` (per `../dabbler-platform/Directory.Packages.props`).
- **.NET TFM.** .NET 10.
- **License obligations.** `dabbler-pdf` is dual-licensed under LGPL-2.1-only OR MPL-2.0. Consumers must comply with the terms of one of these licenses. The NuGet package for `Dabbler.Reports.Export` includes a `THIRD-PARTY-NOTICES.txt` file conveying the upstream license text; per `Dabbler.Reports.Export/README.md`, this file is included in the NuGet package so downstream consumers receive the canonical notice through the package itself. Distribution policies that disallow non-MIT transitive PDF dependencies should review the license before adopting.
- **DI registration.** None required. `PdfExporter` is constructed directly via `new(reportDefinition)` per export operation.

## Out of scope

This document deliberately does not cover:

- `dabbler-pdf` internals (e.g., `PdfArray`, `PdfBody`, `PdfCrossReferenceTable`, `PdfCatalog`, `PdfPage`, `PdfTemplate`, `PdfStream`).
- `PdfPig`, a test-only transitive dependency used for `dabbler-pdf`'s own rendering-visibility tests; not shipped to consumers.
- Any consumer beyond `Dabbler.Reports.Export` and `Dabbler.Reports.Core` (the only two `dabbler-platform` libraries that reference `DabblerPdf`).
- The Migration Map (Feature 3) covers Access-to-platform mapping at the construct level; this document does not.
