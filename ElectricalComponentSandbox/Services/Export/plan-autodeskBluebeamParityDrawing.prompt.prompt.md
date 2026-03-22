# Plan: Optimize to Autodesk/Bluebeam-Parity Drawing Features

**TL;DR** — The app has strong architectural bones (dormant SkiaSharp-ready data pipelines, markup models, tile cache, dirty-rect tracker — all existing but never wired up). The plan activates these systems, adds a proper SkiaSharp 2D rendering engine, then layers professional CAD and markup features on top in five focused phases.

---

## Phase 1 — SkiaSharp 2D Rendering Foundation *(everything else depends on this)*

1. Add `SkiaSharp.Views.WPF ~2.88` NuGet to `ElectricalComponentSandbox/ElectricalComponentSandbox.csproj`
2. Create `Rendering/ICanvas2DRenderer.cs` — abstract interface (`DrawLine`, `DrawRect`, `DrawEllipse`, `DrawPath`, `DrawText`, `PushTransform`, `PopTransform`, `Clear`)
3. Create `Rendering/SkiaCanvasHost.cs` — replaces the dormant `ConduitVisualHost` + `PlanCanvas` combo; WPF `SKElement` host managing an `SKCanvas`
4. Create `Rendering/SkiaCanvas2DRenderer.cs` — implements `ICanvas2DRenderer` using SkiaSharp paint/path APIs
5. Create `Rendering/DrawingContext2D.cs` — transform pipeline state (zoom, pan), current layer style, screen↔document transform; delegates to existing `CoordinateTransformService` *(depends on 2–4)*
6. Port existing `Update2DCanvas()` / `DrawConduitsWithVisualLayer()` / `Draw2DComponent()` in `MainWindow.xaml.cs` to call `ICanvas2DRenderer` instead of WPF shapes *(depends on 5)*
7. Create `Rendering/ShadowGeometryTree.cs` — maintains bounding boxes + path segments for hit testing (WPF visual tree won't work under SkiaSharp) *(parallel with 6)*
8. **Activate dormant systems** — wire `DirtyRectTracker` → `SkiaCanvasHost.InvalidateRegion()`, enable `TileCacheService` for PDF underlay tiles (both classes in Services/ already exist, just never called) *(depends on 6)*
9. Wire `Conduit2DRenderer` detail-level output (Coarse/Medium/Fine) → SkiaSharp renderer so geometry simplifies on zoom-out *(parallel with 8)*
10. **First markup rendering** — create `Services/MarkupRenderService.cs` dispatching each `MarkupRecord` type to SkiaSharp draw calls using existing `DetailLevelService` data *(depends on 6)*

**Verification:** All current components still render; conduit bends still draw; PDF underlay renders via tile cache; drag/click hit testing still works.

---

## Phase 2 — CAD-Quality 2D Drafting *(Autodesk Parity)*

*Parallel with Phase 3*

11. **Complete OSNAP** — expand `Services/SnapService.cs` with Nearest, Perpendicular, Tangent, Center, Quadrant modes; add snap-mode toolbar to `MainWindow.xaml` (icon-per-mode like AutoCAD OSNAP bar); draw type-specific snap glyphs on SkiaSharp canvas (square=endpoint, triangle=midpoint, circle=center, X=intersection)
12. **Ortho & Polar tracking** — F8 = ortho (lock to H/V), F10 = polar tracking with configurable increment angles (15°, 30°, 45°, 90°); render tracking dotted guide line on SkiaSharp canvas; write to `MainViewModel` as `IsOrthoActive` / `PolarAngle` properties
13. **2D professional dimensions** — extend `Services/Dimensioning/` with Linear, Aligned, Angular, Radial dimension types rendered directly on SkiaSharp canvas (distinct from existing 3D HUD dimensions); dimension style record (arrow size, text height, units, precision, prefix/suffix)
14. **Layer property expansion** — expand `Models/Layer.cs` with `LineWeight`, `LineType` (enum: Continuous, Dashed, Dotted, Phantom, Hidden, Center), `IsFrozen`, `IsPlotted`; create `ViewModels/LayerManagerViewModel.cs` for full layer manager panel *(parallel with step 17)*
15. **Object property overrides** — add `LineWeight?`, `LineType?`, `ColorOverride?` nullable overrides to `ComponentParameters` / `MarkupRecord` (inherit from layer if null, standard CAD behavior)
16. **Selection toolset** — window selection (left→right = inside only), crossing selection (right→left = touches), Shift+click multi-select/deselect, Ctrl+A select all; implement in `ShadowGeometryTree` hit testing; add selection info to status bar
17. **Grip editing** — selected entity exposes `GripPoint` handles on SkiaSharp canvas; drag grip to move endpoint/vertex; cancels with Esc; connects to existing `UndoRedoService` *(depends on 16)*
18. **2D drawing primitives** — new toolbar section (Line, Arc, Circle, Polyline, Rectangle, Hatch) that create `MarkupRecord` entries (uses existing markup model); Hatch fill with standard electrical patterns
19. **Annotation tools** — Multileader/callout, revision cloud, reference balloon all as new `MarkupRecord` types; rendered via `MarkupRenderService` *(depends on step 10)*
20. **Named views** — save current zoom/pan/active layers as a named view snapshot stored in `.ecproj` format; view selector dropdown in toolbar; zoom-extents and zoom-to-selection commands

**Verification:** OSNAP glyph appears at snap candidates; ortho constrains cursor; linear dimension appears on 2D canvas; layer line weights visible; window/crossing selection highlights correct components; grips appear on selected conduit run.

---

## Phase 3 — Markup & Review Workflows *(Bluebeam Parity)*

*Parallel with Phase 2*

21. **Markup list panel** — new right-panel tab (or dockable panel) showing all `MarkupRecord`s as a scrollable list; columns: Type, Author, Status, Layer, Label; sortable/filterable *(depends on step 10)*
22. **Status/punch workflow** — add `Status` enum to `MarkupRecord` (Open, InProgress, Approved, Void); status column in markup list panel; color-coded glyph in canvas; export filtered markup list to CSV via existing `BomExportService` pattern
23. **Markup stamps** — custom stamp tool: user-defined stamp templates with status text, date, name badge; rendered as styled `MarkupRecord` overlays on SkiaSharp canvas
24. **Callout & hyperlink** — leader callout markup tool with configurable anchor; hyperlink property on any markup/component opening a URL or external file reference
25. **Measurement tools** — Distance (click 2 points, reads calibrated real-world units), Area (polygon markup auto-calculates enclosed area using `MarkupGeometryService`), Perimeter, Count/tally (click items to count, shown in markup panel); all respect the existing `PdfCalibrationService` scale *(depends on step 21)*
26. **PDF underlay upgrades** — multi-page PDF via `PdfiumCore` or `Windows.Data.Pdf`; page selector control in toolbar; overlay/compare mode (blended opacity render of two underlays for drawing comparison); crop/clip to viewport

**Verification:** Click a conduit → markup callout places with leader; markup list shows the entry; set to Approved → glyph turns green; export CSV has all markups; measurement tool reads calibrated distance.

---

## Phase 4 — Layer & Drawing Standards

*Can be done after Phase 2 step 14*

27. **Full layer manager dialog** — modal or dockable panel with all layer properties from step 14; layer filter groups (All, Used, Selection); import/export layer standard as XML
28. **Plot styles** — CTB-style color→print weight table; paper space canvas mode with print-preview layout; standard paper sizes (ANSI A–E, ArchD)

---

## Phase 5 — 3D Viewport Enhancements

*Independent, parallel with Phases 2–4*

29. **Visual styles** — toggle Wireframe (lines only), Conceptual (unlit flat color), Realistic (current shaded), X-Ray (transparent) via HelixToolkit material/lighting swap
30. **Section cut plane** — interactive horizontal or vertical clip plane (position slider); implemented via HelixViewport3D `ClippingPlane` or geometry masking pass
31. **3D component labels** — billboard `BillboardTextVisual3D` anchored to component origin, always faces camera; toggle via View menu; label = component Name or PartNumber

---

## Phase 6 — Revit Round-Trip

32. **IFC4 export** — create `Services/Export/IfcExportService.cs`; map `ConduitComponent` → `IfcCableCarrierSegment`, `PanelComponent` → `IfcElectricDistributionBoard`, `BoxComponent` → `IfcJunctionBox`; bend geometry → `IfcPolyline` representation; use `Xbim.Essentials` NuGet or a lightweight IFC string writer
33. **Revit import** — complete `ConduitEngineIntegration.cs` / `RevitIntrospectionService` to read Revit `.rfa` family XML placements → create `ElectricalComponent` instances; preserve Revit GUID for round-trip identity
34. **Schedule export** — `Services/Export/ScheduleExcelExporter.cs` using `ClosedXML`; columns match Revit standard parameters (Mark, Description, Family Type, Length, Manufacturer, PartNumber); export per-layer or per-component-type

---

## Phase 7 — Architecture & Coverage *(ongoing, last)*

35. **Refactor code-behind** — extract rendering logic from `MainWindow.xaml.cs` into dedicated classes (target: <1000 lines code-behind from ~4000 today); create `Rendering/CanvasInteractionController.cs` for mouse/keyboard event handling
36. **DI container** — wire `Microsoft.Extensions.DependencyInjection` in `App.xaml.cs`; register all 12+ services; inject into `MainViewModel`
37. **Test coverage** — SkiaSharp bitmap comparison tests for renderer output; markup rendering tests; snap mode unit tests for new OSNAP modes; UI automation for select/grip/dimension

---

## Key Files Affected

| File | Change |
|------|--------|
| `ElectricalComponentSandbox/ElectricalComponentSandbox.csproj` | NuGet additions |
| `ElectricalComponentSandbox/MainWindow.xaml.cs` | Rendering refactor hub |
| `ElectricalComponentSandbox/MainWindow.xaml` | Toolbar/panel additions |
| `ElectricalComponentSandbox/Conduit/UI/Conduit2DRenderer.cs` | Wire up detail levels |
| `ElectricalComponentSandbox/Markup/` | Add render dispatch |
| `ElectricalComponentSandbox/Services/SnapService.cs` | Add OSNAP modes |
| `ElectricalComponentSandbox/Services/Dimensioning/` | Add 2D dimension types |
| `ElectricalComponentSandbox/Models/Layer.cs` | Expand properties |
| `ElectricalComponentSandbox/ViewModels/MainViewModel.cs` | Ortho/polar state, markup tool state |

## New Files

```
ElectricalComponentSandbox/Rendering/ICanvas2DRenderer.cs
ElectricalComponentSandbox/Rendering/SkiaCanvas2DRenderer.cs
ElectricalComponentSandbox/Rendering/SkiaCanvasHost.cs
ElectricalComponentSandbox/Rendering/ShadowGeometryTree.cs
ElectricalComponentSandbox/Rendering/DrawingContext2D.cs
ElectricalComponentSandbox/Rendering/CanvasInteractionController.cs
ElectricalComponentSandbox/Services/MarkupRenderService.cs
ElectricalComponentSandbox/Services/Export/IfcExportService.cs
ElectricalComponentSandbox/Services/Export/ScheduleExcelExporter.cs
ElectricalComponentSandbox/ViewModels/MarkupToolViewModel.cs
ElectricalComponentSandbox/ViewModels/LayerManagerViewModel.cs
```

## NuGet Additions

| Package | Version | Purpose |
|---------|---------|---------|
| `SkiaSharp.Views.WPF` | ~2.88 | GPU-accelerated 2D canvas |
| `Xbim.Essentials` | latest | IFC4 read/write (or custom minimal writer) |
| `ClosedXML` | latest | Excel schedule export |
| `PdfiumCore` | latest | Multi-page PDF underlay (alternative to Windows.Data.Pdf) |

---

## Decisions

- SkiaSharp replaces WPF Canvas for 2D; HelixToolkit stays for 3D — no platform migration
- Markup rendering activates existing models (no data model redesign needed)
- IFC4 used for Revit interop (open standard, no Revit API license dependency)
- No multi-user collaboration — single-user only
- Mobile/iOS view stubs left alone (out of scope)

## Open Questions / Further Considerations

1. **PDF library** — `Windows.Data.Pdf` (built-in, limited) vs. `PdfiumCore` (open source, full fidelity). Recommend PdfiumCore for multi-page support and high-DPI tile rendering.
2. **IFC library** — `Xbim.Essentials` is ~7 MB but full-featured; a minimal custom IFC P21 text writer would be ~200 lines and zero dependency. Recommend custom writer for now, Xbim if round-trip fidelity needs to improve.
3. **Phase ordering** — Phase 1 is the hard blocker; Phases 2 and 3 can proceed in parallel once Phase 1 is stable. Phase 6 (Revit) is independent and can be assigned separately.
4. **Keyboard shortcuts** — Re-use AutoCAD conventions where possible (F3=OSNAP toggle, F8=Ortho, F10=Polar, Ctrl+1=Properties, Ctrl+8=Quick Calc) to minimize learning curve for existing AutoCAD users.
5. **SkiaSharp hit testing** — The `ShadowGeometryTree` will need clear policies on when it's rebuilt vs. incrementally updated (e.g., on component add/move vs. full redraw). A spatial index (R-tree or grid) will be needed once component counts grow beyond ~500.
