# Imperium Engine — Grok Project Memory

Truncated port of Claude project memory (`~/.claude/projects/D--PROJECTS-ImperiumEngine-GitRepo/memory/`).
Keep this file short; deeper history lives in Claude memory files if needed.

## Snapshot

- Free OSS 3D engine in **C# / .NET 10**, UE + Godot inspired.
- **Dev project:** always use `Templates/DevTest/`.
- **Stack:** Raylib, R3D, rlImGui, JoltPhysicsSharp, **Tomlyn 0.17.0 only** (do not upgrade to 2.x).
- **Solution apps:** Engine (runtime), Editor, Launcher, Crasher.

## Layout

| Path | Role |
|------|------|
| `Engine/` | Runtime: components, assets, configs, platforms |
| `Editor/` | ImGui editor: windows, panels, dialogs, proxies, undo |
| `Templates/DevTest/` | Working game project |
| `Engine/Content/` | Engine-shipped assets |
| `Templates/DevTest/Content/` | Game content |
| `Templates/DevTest/Config/` | Game + editor TOML |

### Naming

- Components: `C1_` (1D/logic), `C2_` (2D/UI), `C3_` (3D)
- Assets: `A_*` (e.g. `A_Level`, `A_Entity`, `A_Texture2D`, `A_Sound`)
- Configs: `CFG_*` → `Config/{Name}.toml` (strip `CFG_`)
- Platforms: `Plat_*`
- Editor: `WND_`, `PNL_`, `DLG_`, `Proxy_*`
- Structs: `T*` / `st_*`; enums `E*`

### Extensions

- Default asset: `.impasset`
- Level: `.ImpLvl` · Entity class: `.ImpEnt` · Game: `.ImpGame`
- Package (planned): `.ipk`
- Raw sources (proxies): `.png`, `.glb`, `.ogg`, `.wav` — never mutate source; spawn assets with `file_source`

## Hard rules

1. **One draw entry:** `OnDraw(delta, cam, EDrawFlags)` only. Pass/editor state via flags (`DEBUG_PASS`, `EDITOR_DEBUG`, …). Never add parallel draw virtuals (`OnDrawDebug`, etc.).
2. **`[ImpVar]` gates everything** shown/serialized (inspector + TOML). Struct fields with ctor defaults need `= new()` (else Scale=0).
3. **Asset slots (Godot-style):** `file_link` set = **reference**; empty = **embedded instance**. Serdes in `ImpToml`. Inspector: blue=ref, red=instance.
4. **`file_source`:** raw import path (png/glb/ogg…); many assets may wrap one source.
5. **Tomlyn 0.17.0** pin — 2.x breaks `Toml.ToModel` / `FromModel`.
6. **ImGui modals:** only one top-level modal. Multi-step flows stay **inline** in the same dialog (see `DLG_SaveAsset` overwrite confirm).
7. **`i_EditorConfig`:** planned stub for custom inspector UI — do not delete; check before default reflection draw.
8. **ImpComponent2D = Control** — do not invent a separate O2D_Control class.
9. **Physics only during PIE/play.** `ImpPhysicsWorld` + `ImpPhysic3D`; extend via `BuildCollisionShape` / `ColliderCenterLocal` / `LockUpright`. Subclasses overriding `OnEnd` must call `base.OnEnd()`.
10. **PIE isolation:** play/simulate uses `A_Level.Clone()` (TOML round-trip). While playing: no dirty, no undo. Stop = discard clone. Shift+Esc stops.
11. **Component lifecycle (UE-style):**
    - **`OnInit` / `OnDeinit`** = construction (like UE `OnConstruction`). Run in **editor** (load, spawn, property edit via `Reconstruct`) **and** at runtime before Begin. Build meshes/lights/env here; keep re-entrant.
    - **`OnBegin` / `OnUpdate` / `OnEnd`** = **runtime / PIE only**. Never call from editor preview — prevents input/sim (e.g. camera orbit) firing while editing.
    - Editor uses `Init`/`Deinit`/`Reconstruct` only. PIE/packaged game uses full `Init`→`Begin`→`Update`→`End`.
    - Live editor preview for lights can also refresh in `OnDraw` with `EDITOR_DEBUG` (no `OnUpdate` in editor).

## Editor systems (condensed)

| System | Key facts |
|--------|-----------|
| **Save** | `EditorWindow.DocumentAsset` + `is_dirty`. **S / Ctrl+S** = Save; **Ctrl+Shift+S** = Save As. Stable docking id = `WindowId` (not dynamic `Title`). Async work → `DLG_Process.Run`. |
| **Undo** | Per-window `UndoHistory` (cap 256). Panels push `RelayUndoable` via `on_action`. Ctrl+Z/Y. Inspector = whole-object snapshot; gizmo/hierarchy = closures. Not wired on `WND_AssetEdit` yet. |
| **PIE** | NORMAL = real `ImpPlayer` + game camera; SIMULATE = editor cam + gizmo. Editor level = `Init`/`Deinit` only; play clone = full `Init`+`Begin`/`Update`/`End`. Deinit editor before PIE so lights don't double. |
| **New content** | FileExplorer → `DLG_NewAsset` / `DLG_NewEntity` / fixed New Level. Shared `TypeTree` picker. |
| **TRef\<T\>** | C# type **or** `.ImpEnt` (green in dropdown). `New()` instantiates type or loads entity root. |
| **Settings** | `WND_SettingsProject` = Engine `ImpConfig`s (auto-save). `WND_SettingsEditor` = live `EditorConfig` → `Config/Editor.toml` (session: last level, cam, layout, open windows). |
| **Build** | `WND_Build` + `ImpBuilder.FastWindows` = `dotnet publish` single-file Engine.exe → project dir. Full/other platforms TODO. Needs SDK + Engine source. |
| **File proxies** | `EditorFileProxy` subclasses in `Editor/Proxies/`, reflection registry by extension. PNG: "Create Texture2D". GLB/OGG/WAV: claim ext only (import actions later). |

## Serialization

- Default: cascade `[ImpVar]` into nested TOML; omit default-equal values.
- Custom: `I_Serialize` (`File_WriteTo` / `File_ReadFrom`) checked first.
- Transforms: `[entity.params.transform]` via `TTransform3D`/`TTransform2D` `I_Serialize`.
- Levels/entities: `[[entity]]` arrays; `A_Entity` has `parent_type` + components.
- Keyword paths: `{game}/...` via `ImpFile` / `ImpAsset.ToKeywordPath`.

## Known gaps (do not “fix” casually)

- Level TOML still **flat** top-level components — nested children not fully persisted → PIE clones lose hierarchy.
- `A_Level.Save` may omit some level-level `[ImpVar]`s (e.g. `game_mode`).
- Full build / non-Windows Fast build stubs.
- Scripting deferred; New Script menu disabled.
- CFG_Game / CFG_Input fields not fully serializable yet.
- Sound/mesh import proxies stubbed (`Proxy_Sound`, `Proxy_GLB`).

## Conventions when coding

- Prefer existing patterns: reflection discovery, `[ImpVar]`, `ImpToml`, keyword paths.
- Match surrounding style (comments explain *why*, short type prefixes).
- Dev/test against `Templates/DevTest/`.
- Do not expand scope into unrelated systems.
- Physics/body teardown: always `base.OnEnd()` when overriding.
