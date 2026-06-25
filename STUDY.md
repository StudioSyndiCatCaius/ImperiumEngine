# STUDY.md — Reference Engine Analysis

> Analysis of the engines referenced in `CLAUDE.md`, weighted toward **Godot**
> (the chosen base for Imperium's UI system) and **Unreal**. Use this as the
> design rationale when building Imperium subsystems — especially the UI.

## Reference paths

| Engine | Path | Used for |
|---|---|---|
| Unreal Engine 5.8 | `D:/Games/EpicGames/UnrealEngine/UE_5.8/Engine/Source/` | UI (Slate/UMG), blueprints, friendly structure |
| Godot | `D:/PROJECTS/ImperiumEngine/RefRepos/godot/` | **node/scene architecture, UI system (primary base)** |
| Wicked | `D:/PROJECTS/ImperiumEngine/RefRepos/Wicked/` | modern 3D rendering / RHI abstraction |
| Stride | `D:/PROJECTS/ImperiumEngine/RefRepos/stride/` | C# engine architecture |

---

## Godot — the UI base (primary reference)

Godot's UI is a **node tree** with a clean inheritance chain that maps almost
1:1 onto Imperium's existing `O2D_*` classes:

```
Node → CanvasItem → Control → (BaseButton, Container, Label, Range, ...)
```

### `Node` — `scene/main/node.h`
Tree membership + lifecycle. Key callbacks: `_enter_tree`, `_ready`,
`_process(dt)`, `_physics_process(dt)`, `_exit_tree`, driven by a `NOTIFICATION_*`
integer system (one `_notification(int)` switch rather than many vtable slots).
Has a `ProcessMode` (inherit / pausable / always / disabled).

### `CanvasItem` — `scene/main/canvas_item.h`
Anything drawable in 2D: visibility, modulate (tint), z-index, the immediate-mode
`draw_*` primitives, and the local→global `Transform2D`.

### `Control` — `scene/gui/control.h` (~900 lines, the crown jewel)
Adds everything UI:
- **Layout — anchor + offset model.** Each side (L/T/R/B) has an `anchor` (0–1
  fraction of parent) and an `offset` (pixels). `LayoutPreset` enums
  (`PRESET_FULL_RECT`, `PRESET_CENTER`, …) are convenience setters over those.
  **The single most important concept to port.**
- **Sizing** — `get_minimum_size()` / `get_combined_minimum_size()`,
  `custom_minimum_size`, and a `SizeFlags` bitfield (`FILL`, `EXPAND`,
  `SHRINK_CENTER`…) + `stretch_ratio` that containers read.
- **Input** — `gui_input(event)`, `has_point()` hit-testing, `MouseFilter`
  (stop / pass / ignore) for event propagation.
- **Focus** — `FocusMode`, directional neighbors, `grab_focus`/`release_focus`.
- **Theming** — every control resolves styling by name through a `Theme`
  resource, with per-instance overrides (see below).

### `Container` — `scene/gui/container.h` (tiny but key)
Overrides child add/remove/sort notifications; on `NOTIFICATION_SORT_CHILDREN`
calls `fit_child_in_rect(child, rect)` to position each child. `BoxContainer`,
`GridContainer`, `MarginContainer`, `TabContainer`, `SplitContainer`, etc. are
all just different sort implementations. **This is the model for Imperium's
containers.**

### `BaseButton` — `scene/gui/base_button.h`
The interaction state machine to copy: a `Status` struct
(pressed / hovering / press_attempt / disabled), a `DrawMode` enum
(NORMAL / HOVER / PRESSED / DISABLED / HOVER_PRESSED) that derived buttons use to
pick a stylebox, toggle mode, and button groups. Imperium's `O2D_Button`
currently only has `OnPressed`/`OnReleased` — this shows the full model.

### Theme system — `scene/resources/theme.h`, `style_box.h`, `scene/theme/`
- A `Theme` is a resource mapping `(theme_type, item_name) → value` across six
  `DataType`s: **Color, Constant (int), Font, FontSize, Icon (Texture), StyleBox**.
- `theme_type` is usually the control's class name ("Button"), so one theme
  styles all controls; `theme_type_variation` allows named variants.
- `StyleBox` is the drawable-background abstraction — `StyleBoxFlat` (rounded
  rect + border + shadow, data-driven), `StyleBoxTexture` (9-patch),
  `StyleBoxLine`, `StyleBoxEmpty`. A control draws its background by fetching a
  stylebox by name and calling `stylebox->draw(canvas, rect)`.
- Resolution walks up the tree (instance override → owner theme → parent themes
  → default theme), heavily cached.

This directly informs Imperium's `ImpAsset_2DTheme` (currently an empty stub):
it should be a `(type, name) → {color/const/font/stylebox/...}` map, and Imperium
needs a `StyleBox`-equivalent asset.

---

## Unreal (Slate / UMG) — contrast

Slate (`Runtime/SlateCore`, `Public/Widgets/SWidget.h`) is a **retained
immediate-mode** system, structurally different from Godot:
- Layout is **Measure/Arrange** (two-pass): `ComputeDesiredSize(scale)` bottom-up,
  then parents arrange children. (Stride and WPF use this same model.)
- Rendering is `OnPaint(...)` returning a layer id, drawing into an
  `FSlateWindowElementList` — fully retained draw-element batching, not per-frame
  `draw_line` calls.
- Input handlers return an `FReply` that explicitly captures/routes events
  (`OnMouseButtonDown`, etc.), vs Godot's `accept_event()`.
- Declarative `SLATE_BEGIN_ARGS` builder syntax; **UMG** wraps Slate widgets as
  `UWidget` UObjects for the designer.

**Takeaway:** Godot's anchor model is simpler and is the chosen base. But Slate's
**Measure/Arrange two-pass** and **batched draw-elements** are worth borrowing for
performance — and Unreal's `On*` / `FReply` naming matches Imperium's existing
`On*` convention better than Godot's `_notification` ints.

---

## Stride — C# architecture reference

`Stride.UI` is the most directly **portable** since Imperium is C#.
`UIElement.cs` uses the **Measure/Arrange** model (`Measure(availableSize)` /
`Arrange(finalSize)` with `IsMeasureValid` / `IsArrangeValid` dirty flags) —
WPF-style. Hierarchy: `UIElement → Control → ButtonBase → Button/ToggleButton`,
plus `Panel`s for layout. It uses a `DependencyProperty` system for
styleable/animatable properties — heavier than Imperium's `[ImpVar]`/`[Exposed]`
attributes; stick with the attribute approach, but note Stride's `Thickness`,
`HorizontalAlignment`, `Orientation` value types map cleanly to what Imperium
needs.

---

## Wicked — rendering reference (later)

`WickedEngine/wiGraphicsDevice.h` is a clean modern RHI abstraction: a pure-virtual
`GraphicsDevice` with `CreateBuffer/Texture/Shader/PipelineState`, command-list-based
`Bind*` calls, and `SubmitCommandLists()`, implemented per-backend (`_DX12`,
`_Vulkan`, `_Metal`). This is the model for fleshing out Imperium's
`ImpRender`/`RHI_*` classes — bindless, command-list oriented, PSO-based — but
that's a later concern.

---

## Synthesis for Imperium's UI

| Concept | Source | Maps to Imperium |
|---|---|---|
| Node tree + lifecycle | Godot `Node` | `ImpComponent` (has `OnBegin/Update/Draw`) |
| 2D drawable base | Godot `CanvasItem` | `ImpComponent2D` (has `transform`, `override_theme`) |
| Anchors/offsets layout | Godot `Control` | **new** — `O2D_Control` base (or add to `ImpComponent2D`) |
| Container sort model | Godot `Container` | `O2D_*` containers (ScrollContainer, TabContainer exist) |
| Button state machine | Godot `BaseButton` | expand `O2D_Button` |
| Theme `(type,name)→value` | Godot `Theme` | flesh out `ImpAsset_2DTheme` |
| StyleBox | Godot `StyleBox(Flat)` | **new** asset type |
| Measure/Arrange + draw batching | Unreal / Stride | optional perf upgrade over per-frame immediate draw |
| `On*` naming, event replies | Unreal | already matches Imperium convention |

**Direction:** adopt Godot's anchor/offset + Container + Theme/StyleBox model
wholesale (cleanest, and matches the existing `O2D_*` / `ImpAsset_2DTheme`
scaffolding), but **name things with Imperium's `On*` / `Noun_Verb` convention**
rather than Godot's `_notification` / snake_case, and keep the option open to add
a Measure/Arrange pass à la Stride if immediate-mode layout proves limiting.
