# vizar-rtl patches on AvaloniaEdit 12.0.0

Branch `vizar-rtl`, based on tag `12.0.0` (86fdebe). Used by vizar-ide as the submodule `external/AvaloniaEdit`
(vizar-ide ADR 0053). Every change is marked `[vizar-rtl]` in the source; `git diff 12.0.0 vizar-rtl` is the full patch.

## Model
`TextEditorOptions.RightToLeft` (default `false`, so upstream behaviour is unchanged) turns on RTL layout:
- `TextEditor` sets its `FlowDirection` to `RightToLeft` (back to `LeftToRight` when turned off). Avalonia mirrors the
  control, so the gutter (margins) moves to the right and x = 0 is the right edge.
- Lines are formatted with paragraph `FlowDirection.RightToLeft` (BiDi base direction RTL), `TextAlignment.Left`.
- Character geometry of RTL lines comes from the new `RtlTextLineMetrics` (runs in visual order, UBA L2 undone to get
  logical offsets, glyph clusters) because Avalonia 12's RTL `GetDistanceFromCharacterHit`/`GetCharacterHitFromDistance`
  are wrong and `GetTextBounds` throws "Covered length must be greater than zero" for many RTL paragraphs. LTR keeps
  Avalonia's implementation.
- Every TextLine x is flipped per line with `VisualLine.FlipX(line, x) = line.WidthIncludingTrailingWhitespace - x`
  (its own inverse) when drawing, hit-testing and building geometry. Together with the control mirror this gives
  right-aligned lines with unmirrored glyphs. Virtual space and scrolling keep working in "distance from the start edge".

## Diffs
| File | Change |
|---|---|
| `TextEditorOptions.cs` | new `RightToLeft` option |
| `TextEditor.cs` | `OnOptionChanged`: sync `FlowDirection` with `RightToLeft` |
| `Rendering/VisualLineTextParagraphProperties.cs` | `FlowDirection` settable (was hard-coded LTR) |
| `Rendering/TextView.cs` | `CreateParagraphProperties` passes the direction; inline-object arrange x from flipped bounds |
| `Rendering/RtlTextLineMetrics.cs` | new: RTL caret distance, hit-test and text bounds per TextLine (cached) |
| `Rendering/VisualLine.cs` | `IsRightToLeft`, `FlipX`, RTL-safe `GetTextBounds`/`GetDistanceFromCharacterHit`/`GetCharacterHitFromDistance`; used in `GetTextLineVisualXPosition`, `GetVisualColumn(TextLine,…)`, `GetVisualColumnFloor`; `VisualLineDrawingVisual.Render` draws under a flip transform |
| `Rendering/BackgroundGeometryBuilder.cs` | `VisualLine.GetTextBounds` rects flipped (selection, squiggles, markers) |
| `Editing/Caret.cs` | overstrike caret rect via `VisualLine.GetTextBounds`, flipped |
| `Rendering/Layer.cs` | layers bind `FlowDirection` to the text view: they have no logical parent, so they stayed LTR and Avalonia mirrored each layer again (caret, selection, squiggles and other layer renderers landed far left of the right-aligned text) |
| `Editing/LineNumberMargin.cs` | digits un-mirrored in place |
| `Directory.Build.props` | `NuGetAuditSuppress` for GHSA-23fw-v26w-5fgq (build-only SourceLink dep; breaks the build under TreatWarningsAsErrors) |

## Known limits
- Inline UI elements (`InlineObjectElement` controls): content renders mirrored, and `TextView.ArrangeOverride` still
  counts run offsets in visual order (upstream), so placement can be off in mixed-direction lines.
- The end-of-line caret of a line ending in LTR text sits at that text's trailing edge.
- Tests live in vizar-ide (`RtlLayoutTests`, `GoldenLineTests`), not in this fork.
- `TextFormatterFactory` (helper formatted text) stays LTR.

## Rebase / upstream
Rebase onto a new tag with `git rebase --onto <tag> 12.0.0 vizar-rtl`; re-check the files above. Upstreaming would
replace the option with `FlowDirection` handling inside `TextView`.
