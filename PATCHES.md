# vizar-rtl patches on AvaloniaEdit 12.0.0

Branch `vizar-rtl`, based on tag `12.0.0` (86fdebe). Used by vizar-ide as the submodule `external/AvaloniaEdit`
(vizar-ide ADR 0053). Every change is marked `[vizar-rtl]` in the source; `git diff 12.0.0 vizar-rtl` is the full patch.

## Model
`TextEditorOptions.RightToLeft` (default `false`, so upstream behaviour is unchanged) turns on RTL layout:
- `TextEditor` sets its `FlowDirection` to `RightToLeft` (back to `LeftToRight` when turned off). Avalonia mirrors the
  control, so the gutter (margins) moves to the right and x = 0 is the right edge.
- Lines are formatted with paragraph `FlowDirection.RightToLeft` (BiDi base direction RTL), `TextAlignment.Left`.
- Every TextLine x is flipped per line with `VisualLine.FlipX(line, x) = line.WidthIncludingTrailingWhitespace - x`
  (its own inverse) when drawing, hit-testing and building geometry. Together with the control mirror this gives
  right-aligned lines with unmirrored glyphs. Virtual space and scrolling keep working in "distance from the start edge".

## Diffs
| File | Change |
|---|---|
| `TextEditorOptions.cs` | new `RightToLeft` option |
| `TextEditor.cs` | `OnOptionChanged`: sync `FlowDirection` with `RightToLeft` |
| `Rendering/VisualLineTextParagraphProperties.cs` | `FlowDirection` settable (was hard-coded LTR) |
| `Rendering/TextView.cs` | `CreateParagraphProperties` passes the direction; inline-object arrange x flipped |
| `Rendering/VisualLine.cs` | `IsRightToLeft`, `FlipX`; flipped in `GetTextLineVisualXPosition`, `GetVisualColumn(TextLine,…)`, `GetVisualColumnFloor`; `VisualLineDrawingVisual.Render` draws under a flip transform |
| `Rendering/BackgroundGeometryBuilder.cs` | `GetTextBounds` rects flipped (selection, squiggles, markers) |
| `Editing/Caret.cs` | overstrike caret rect flipped |
| `Editing/LineNumberMargin.cs` | digits un-mirrored in place |
| `Directory.Build.props` | `NuGetAuditSuppress` for GHSA-23fw-v26w-5fgq (build-only SourceLink dep; breaks the build under TreatWarningsAsErrors) |

## Known limits
- Inline UI elements (`InlineObjectElement` controls) are positioned correctly but their own content renders mirrored.
- `TextFormatterFactory` (helper formatted text) stays LTR.

## Rebase / upstream
Rebase onto a new tag with `git rebase --onto <tag> 12.0.0 vizar-rtl`; re-check the files above. Upstreaming would
replace the option with `FlowDirection` handling inside `TextView`.
