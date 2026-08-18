# Dictionary

Living lookup so agents do not re-read source. Update this when you learn or change something.

## ImpPhys (`Engine/ImpPhys.cs`)

Per-`ImpGame` Jolt world. `Foundation` + `JobSystemThreadPool` are process-wide (`ImpPhys.Init` from `ImpApp.Run`, `Shutdown` on exit).

| Flag combo | Jolt object | Role |
|---|---|---|
| `!physics_enabled` | none | Visual only |
| `physics_enabled && !movement_enabled` | Static `Body` | World geo (`C3_Mesh` default) |
| `physics_enabled && movement_enabled` | `CharacterVirtual` + optional inner body | Pawn (`C3_Character` default) |

- `ImpGame.phys` / `Phys_Get()` / `Phys_Dispose()`. Lazy-create on first `Register`. `REnd` and `Play_Stop` dispose so PIE never shares bodies with host.
- Layer filters (`ObjectLayerPairFilterTable` etc.) **must stay rooted on `ImpPhys`**. They were locals at first; GC disposed the native objects while Jolt still held the pointers, and `CharacterVirtual.ExtendedUpdate` crashed with `ExecutionEngineException`. `PhysicsSystem.Dispose` already deletes those natives — do not `Dispose()` the C# wrappers after that (just `GC.SuppressFinalize`).
- Register on `Imp3D.OnBegin` if `physics_enabled`. Unregister on `OnEnd` / `OnDestroy`.
- Tick: `ImpScene.Update` walks the tree (`Update_Physics` / `Update_Movement`) **then** `game.phys.Step(dt)` so same-frame `Phys_Move` applies.
- Two object layers: Static (0) / Moving (1). Static↔Moving and Moving↔Moving collide.
- `Trace_Line` uses `NarrowPhaseQuery.CastRay` when a world exists. Editor picking still uses AABB `Ray_Comp3D`.
- `C3_Mesh` shapes: `GEO_PLANE` = thin box, everything else (including `GEO_CUBE` and imports) = `BoxShape(0.5 * scale)`. Triangle `MeshShape` from R3D meshes is not wired (R3D `Mesh` has no vertex accessor).
- `C3_Collider`: collision volume only (not a mesh). Cube/Sphere/Cylinder/Capsule primitives, Cone = convex hull. Capsule `Phys_ShapeOffset` lifts the shape so `CharacterVirtual.Position` is at the feet. Editor draws a translucent cyan debug primitive matching `shape`/`extents` (hidden in PIE). `Shape_Local` feeds `Comp3D_LocalBounds` so the volume is pickable. Default `physics_enabled`.
- No dynamic rigid bodies in v1.
- `ImpComp.OnBegin` / `OnEnd` now cascade to children. `Destroy` calls `OnDestroy` before detaching.

### Movement (`Imp3D`)

Runtime (not ImpVar): `velocity`, `is_grounded`.

- `Phys_Move(dir, scale)` — accumulate world wish (UE `AddMovementInput`).
- `Phys_MoveByRot(dir, scale, rot_euler)` — `Transform(dir, rot)` then `Phys_Move`.
- `Phys_Launch(axis, scale, force_h, force_v)` — impulse; force flags replace that plane.
- `Update_Physics` — gravity / zero downward when grounded.
- `Update_Movement` — accel/decel toward wish * `A_MoveMode.speed`, air control + friction, then `rotate_with_movement` (UE Orient Rotation to Movement): face horizontal velocity, up = `-gravity`. `velocity_rotation_rate` is deg/s per euler axis (0 locks that axis). Default yaw 360. `CharacterVirtual` drives position only — facing stays on the Imp3D.
- `C3_Character` consumes `_Move` (WASD, camera/self yaw, X=forward Z=right remapped to local `(Z, Y, -X)`) and `_Jump` (`Phys_Launch` up, `jump_speed` on `A_MoveMode`). Claims player 0 on `OnBegin`.
- Visual child meshes of a character must keep `physics_enabled = false` or they double-collide. `C3_Character.mesh` is a child `C3_Mesh` defaulting to `A_Mesh.SK_MANNEQUIN` (`Import` of `{engine}/Meshes/Character/Mannequin/sk_c_mannequin.glb`).

`A_MoveMode` defaults: speed 5, accel/decel 20, jump 6, `rotate_with_movement` on, `velocity_rotation_rate` (0, 360, 0) deg/s. Gravity is `gravity_dir * 9.81 * gravity_scale` (curve unused). `PRESET_PAWN` / `ECollisionChannel.World` + `Pawn` added; body vs body still uses the two Jolt layers only.


## ImpComp (`Engine/ImpComp.cs`)

Scene-graph node. `Imp2D` / `Imp3D` inherit.

| Member | Notes |
|---|---|
| `name` | ImpVar. Defaults to type name. |
| `is_visible` | ImpVar. Local only — `IsVisibleInTree()` walks ancestors. |
| `parent` / `children` | Tree. `children` is readonly list, mutate via Child_* / Detach. |
| owned / native | Public (or private) `ImpComp` fields that are also children — `C3_Character.mesh` / `skeleton` / `creature`. `IsOwned`, `OwnedFields`, `OwnedFieldOf`, `Owned_Bind`, `OutlinerHost`. |
| `scene` | Cached owning `ImpScene`. **Not** ImpVar. Property: assign cascades to descendants. |
| `game_owner` | Cached owning `ImpGame`. **Not** ImpVar. Same cascade as `scene`. Get(0) = editor/standalone, Get(1) = PIE. |
| `input_owner` | `ImpPlayer` that feeds input into this subtree. |
| `popup_config` | `A_PopupConfig`. Non-null = RMB on this comp (or a descendant without its own config) opens the global ImpPlayer popup. |

**`scene` / `game_owner` contract**
- Set on attach: `Child_Add` / `Child_Insert` copy `parent.scene` and `parent.game_owner` onto the child (cascades).
- Cleared on `Detach` and `Destroy`.
- Scene root is bound by `ImpScene.root` setter (`root.scene = this`, `root.game_owner = scene.game`).
- `ImpGame.scene` setter stamps `ImpScene.game`, which stamps the tree.
- Loose / cloned comps have `scene` / `game_owner` == null until attached under a bound root.
- Clone skips `_scene` and `_game_owner` (and parent/children/input_owner/is_destroying). Re-attach to bind.

**Tree ops**
- `Child_Add` / `Child_Insert` Detach first (or reorder if already a child).
- `Reparent` keeps world transform for 2D/3D, then Child_Add/Insert.
- `Destroy` destroys children first, then unhooks parent + clears scene and game_owner.
- Update/Draw snapshot children via `ArrayPool` (not `ToArray`) because those passes may reparent/destroy.
- Owned children stay in the live `children` list (draw / update / physics) but **do not appear in the outliner**. `Detach` / `Reparent` refuse them. `Clone` overlays onto the copy’s ctor instances instead of adding a second set. JSON writes them under `owned` (field name) and merges back on load so editor reload does not double them. Private owned slots stay hidden in the inspector tree too.

**Inspector Components tree**
- Unreal-style: outliner picks the host (`OutlinerHost` walks past owned / packed-foreign). Inspector top `Components` tree lists the host + **public** owned slots + packed-foreign kids (and extras under those hidden nodes).
- Click a row to inspect that comp’s ImpVars; gizmo follows; outliner stays on the host. Prefab instance children are the same path (hidden from outliner, yellow in the inspector tree). User-added siblings of the host still live in the outliner.

**Do not confuse with** `Imp2D._scene_root` — static per-frame flag for “this subtree is scene canvas content” (layout/pivot/rotation). That is **not** `ImpComp.scene`.

## Engine `Content`

One folder: `Engine/Content` in source. `Engine.csproj` copies it next to the exe as `Content` for shipped builds. `{engine}` via `ImpFile.ContentDir_Engine()` prefers the source tree (`…/Engine/Content` if `Engine.csproj` is nearby) and falls back to `AppContext.BaseDirectory/Content`. Game content is still `{game}` → `<game>/Content`. Do not add `_Content`.

## Imp2D (`Engine/Imp2D.cs`)

Was `ImpComp2D`. Size + viewport alignment live on `layout` (`TLayout2` in `Engine/Structs/ST_UI.cs`), not on the comp.

| Member | Notes |
|---|---|
| `position` | Layout slot in the parent (UE slot pos). Used when the parent is a free Imp2D. Unused under C2_List / C2_ScrollBox — those place via `Bounds_GetForChild`. |
| `transform` | Added offset after layout (UE RenderTransform): nudge, shake, hover, rotation, scale. Does not own the slot. |
| `layout` | `TLayout2`: `size`, `size_min`, `size_max`, `view_alighnment_H/V`. Presets: `TLayout2.FULL`, `H_BAR`, `V_BAR`. Lists still use this for size/fill. |
| `pivot` / `normalize_pivot` | Still on the comp (not in `layout`). |
| `stretch_ratio` | List stretch share. |

**Draw primitives** (`Draw_Rect` / `Draw_Texture` / `Draw_Text`): `TBounds2` is the local rect (`start`→`end`), `TTransform2` is the world offset (same compose as `WorldFromLocal`). Identity offset = draw the bounds as-is. `scale` 0 counts as 1 so `default(TTransform2)` works. Texture is a stretch blit. Text height = dest height (`DrawTextEx` / `DrawTextPro`). Rotation about dest top-left.

**`TBounds2(position, layout, parent)`** — child AABB inside a parent `TBounds2` (not a viewport size). Parent start/end is normalized. Clamps `layout.size` via `size_min`/`size_max` (`size_max` of 0 = min only). Fill axis uses parent size. Start/Center/End/Fill place against parent origin the same way `Imp2D.Dimensions_Get` does (`position` is the Align offset; End insets it). `end = start + size`. Pass `parent.Expand(margins)` to inset first. No pivot/rotation — those stay on `TTransform2`. `TDimensions2` is being replaced by `TBounds2` / `TBounds3`.

**`Bounds_Get`** — screen-absolute *layout* AABB: `new TBounds2(position, layout, parent.Bounds_GetForChild(index))`. No Imp2D parent → screen rect. Cached on `_layout_epoch`. Does **not** include `transform` — draw with `Draw_*(Bounds_Get(), Transform_Get(true), …)`. **`Bounds_GetForChild(index)`** virtual; default is `Bounds_Get()`. Lists/scrolls override to hand a child its slot (padding, stretch) and ignore the child's `position`.

`C2_Button.layout` was renamed to `button_layout` (`EButtonLayout`) so it does not hide `Imp2D.layout`. `layout.size.X =` is illegal (struct-in-struct) — assign the whole `Vector2`.

## C2_EnumOption (`Engine/Comps/2D/C2_EnumOption.cs`)

Radio row of `C2_Button`s for one enum. Set `base_enum`, `selected_enum`, `on_change`. `show_name` / `show_icon` / `orentation`. Rebuilds when those change. Clicking a value sets `selected_enum` and fires `on_change` (no-op if already selected). Sync from outside by assigning `selected_enum` (no callback).

Icons autoload from `{engine}/Icons/type/` then `{engine}/Icons/Types/`. Tried names: `{Enum}/{Value}.png`, `{Enum}_{Value}.png`, `{Enum}.{Value}.png`, `{Value}.png`. Missing file → text-only for that value. Cache is static. Label uses `[Title]` on the enum field when present.

`EdScene` toolbar: `ESceneEditorMode`, `EGizmoMode`, `EGizmoSpace`. `ESceneEditorMode` titles are `3D` / `2D`.

**UI styles** live in the matching `C2_*.cs` as `ImpAsset` subclasses (`UI_Text` in `C2_Text`, `UI_Button` + `EButtonLayout` in `C2_Button`, `UI_List` in `C2_List`, `UI_MenuBar` in `C2_MenuBar`, `UI_TextEdit` / `UI_Slider` / `UI_TabBox` same pattern). Box chrome is `UiStyle_Box` in `C2_Box`. There is no `Engine/Assets/UI` or `A_UI` anymore.

`C2_TextEdit.hog_input` (default true) is whether focusing the field sets `input_hog`. Inspector fields hog; graph pin defaults set it false so the canvas still pans / zooms / clicks. Click-outside walks from `target_cursor` up (the field or a descendant), not from the field up through ancestors — otherwise clicking the graph (an ancestor) would never unfocus.

## ImpScene (`Engine/ImpScene.cs`)

`ImpAsset` subclass. File ext `ImpScene`. A scene **is** a hierarchy of ImpComps under `root`.

| Member | Notes |
|---|---|
| `current` / `global` | `current` follows `ImpGame.current.scene` (Get(0) host, or Get(1) while PIE is ticking). `global` is still process-wide. |
| `game` | Owning `ImpGame`. Stamped by `ImpGame.scene`. Cascades onto `root`. |
| `root` | Property. Assign unbinds old tree (`scene=null`), Detach, binds new (`scene=this`). Null coalesces to a fresh ImpComp. |
| `root_type` | ImpVar `TClass<ImpComp>` — intended root class when creating a scene, not the live instance. |
| `script_builtin` / `script_override` | Hidden inline `A_Script` + optional on-disk override. `Script_Get()` prefers override. New scenes always have a builtin (`parent_type` = ImpScene). |
| `is_running` | Toggles `RuntimeBegin` / `RuntimeEnd` on the root, then `root.Update`. |
| `canvas_size` | 2D scene canvas (default 1920x1080). |

Ctor always creates a bound default root. Replacing root is how the editor boots (`ImpScene.current.root = new Scene_Editor()`).

`root` is **not** ImpVar. File_JSON special-cases ImpScene: `vars.root` is `{ _class, name, vars, children }` via `Comp_ToJson` / `Comp_FromJson`. Comp vars are ImpVar fields (skips `name` — stored at the node — and ImpComp-typed fields). Comp class from `ImpComp.Type_FromName`.

**SceneDrop** — `Instantiate()` of this asset (packed instance, not a flattened copy). Ghost on `view.overlay`. Drop parents the instance root under a host-owned dest (`scene.root` if empty). Will not parent inside an instance capsule. Undo via `ImpUndo.Comp_Moved`. Selection after drop is `PNL_SceneView`.

Test asset: `Projects/Test/Content/Scenes/boxes_3.ImpScene` — root `Boxes3` (`ImpComp3D` group pivot) + `Box_A/B/C` (`C3_Mesh` / `GEO_CUBE`) at x = -2, 0, 2.

## ImpAsset (`Engine/ImpAsset.cs`)

JSON-backed asset. Cache keyed by resolved full path. Builtins are `builtin:Type.Member`.

- Load: peek `_class` → `Activator` → `File_Read` → `BindSource`.
- Save: `{ _class, vars }` via `File_JSON`. Only `[ImpVar]` fields.
- `source_file` is an `ImpFile` (png/glb/hdr/…) that many assets can share.
- `File_IsValid` — path exists on disk (or builtin). `File_CanWrite` — real disk path, not builtin / not untitled.
- `File_Write` writes in place and binds `_loaded`. `File_SaveTo(path)` rekeys the cache then writes (Save As).
- `SaveAllDirty` writes cached dirty assets that already `File_CanWrite`. Untitled ones need `DLG_SaveFile`.

## Editor Save (`Scene_Editor` / `EdWindow` / `DLG_SaveFile`)

File menu + toolbar + hotkeys. `C2_MenuBar` fires the File entries:

| Command | Hotkey | Who |
|---|---|---|
| New Scene | Ctrl+N | `DLG_NewScene` — pick root `ImpComp` + name |
| New Asset | | `DLG_NewAsset` — pick `ImpAsset` class + name |
| Save | Ctrl+S | Frontmost `EdWindow.OnTrySave` |
| Save As | Ctrl+Shift+S | Frontmost `EdWindow.OnTrySaveAs` |
| Save All | Ctrl+Alt+S | All open scene/asset tabs, then remaining dirty cache. Untitled → `DLG_SaveFile` in sequence |

`WND_Scene` / `WND_Asset` save the active tab. No path → Save As. `DLG_SaveFile` (`Editor/Dialog`) is the Save As picker: `EdFileTree` of game `Content` (click a folder), name + extension, then write. Suggested folder is that window's file browser `CurrentDir` if it sits under Content, else Content itself. Overwrite of a different file goes through `Dialog_Confirm`. Tab titles pick up `GetName()` plus `*` while dirty.

`WND_Asset` has its own `PNL_FileBrowser` under the asset tabs (same expandable wrap as the scene window). Double-click still goes through `ImpAsset.Editor_OnOpenAsset`.

`Dialog_FileSave` was removed — do not add engine-side save UI; the editor owns the picker.

## Editor session (`Editor.TOML` / `EdState`)

Per-project UI state. Path: `{A_Game.game.GetRootDir()}/Config/Editor.TOML`. Tokenized `{game}/…` / `{engine}/…` paths via `File_JSON.Path_Tokenize`.

Loaded at the end of `Scene_Editor` ctor (`EdState.Load`). Written every 2s while running and again on `ImpApp` shutdown (`Editor/Program.cs` `on_shutdown`).

| Section | What |
|---|---|
| `[window]` | Main tab name, inspector/outliner tab, scene + asset file-browser expanded/stretch (`file_browser_expanded`, `file_browser_asset_expanded`, `browser_stretch` + `scene_tabs_stretch`, `browser_asset_stretch` + `asset_tabs_stretch`), splitter sizes (`panel_width`, sidebar) |
| `[tabs]` | Active scene tab index, active asset tab index |
| `[[open_scenes]]` | Saved scenes only (`File_CanWrite`). Camera 3D/2D, edit/gizmo/snap, selected comp name-paths |
| `[[open_assets]]` | Open asset file paths |
| `[file_browser]` | Scene window `PNL_FileBrowser`: current dir, Game/Engine tab, search, show flags, thumbnail size, favorites, expanded folders |
| `[file_browser_asset]` | Asset window `PNL_FileBrowser`, same keys. Independent of the scene browser. |

Untitled tabs are not persisted (no path). Missing `Editor.TOML` keeps the default preview scene. Saved scenes replace that preview.

`A_Game.GetRootDir()` uses `gamepath` first and ignores `builtin:` filepath. `Builtins_All` must not stamp `GAME_TEST.filepath` — that made `ContentDir_Game()` fall back to `{cwd}/Content` (the engine content copy). Result: Game tab listed Fonts/Icons, and `{game}/Scenes/…` restore missed project scenes.

`File_TOML` (`Engine/Files/File_TOML.cs`) is a small tables / array-of-tables reader-writer used by `EdState` and `ImpConfig`. Not a full TOML 1.0 impl.

## ImpConfig (`Engine/ImpConfig.cs`) / `WND_ConfigGame`

Game settings. A member is a config var when it is **public**, **static**, `[ImpVar]`, and `[Config]`. Each declaring class is a category; values live in `{A_Game.game.GetRootDir()}/Config/{Type.Name}.TOML` (e.g. `ImpGame.save_game_type` → `ImpGame.TOML`).

`ImpApp.Run` calls `ImpConfig.LoadAll()` after `on_pre_init` (snapshots field initialisers, then overlays the files). Editor assembly types are skipped. `[Config(name)]` / `[ImpVar(name)]` override the TOML key.

`WND_ConfigGame` (main tab **Config Game**): left `C2_Tree` of categories, splitter, `C2_Inspector` on the selected `Type` (`include_static`, `declared_only`, `filter_property = ImpConfig.Member_IsConfig`). Edits save that class file immediately; File → Save writes every category.

Supported TOML values: bool / string / numbers / enum (name) / `Vector2-4` / `Color` (float arrays) / `TRef<T>` (tokenized path) / `TClass<T>` (class name).

`ImpGame` save slots (`save_game_type`, `save_game_prefex`, `save_global_type`, `save_global_name`) are `[Category("Save")]` config vars.

`C2_Seperator` writes `stretch_ratio` as a 0-1 **share** of the two neighbors (not pixel size). `EdState.Stretch_IsWeight` rejects values `> 8` so old pixel dumps (`browser_stretch = 189`) do not restore a 189:1 file-browser vs scene split. Both sides of the file-browser split are persisted (`browser_stretch` + `scene_tabs_stretch`, same for assets). Drag is disabled (and the sep ignores the cursor) when a neighbor `C2_Expandable` is collapsed.

`C2_Tree.Tree_ExpandedKeys` / `Tree_SetExpandedKeys` persist folder-tree open rows. `PNL_SceneView.Camera3_Apply` / `Camera2_Apply` restore orbit distance with the 3D camera.

**SceneDrop hooks** (driven by `PNL_SceneView`, not ImpPlayer 3D hit-test). `view` is the active `C2_Viewport3D` / `C2_Viewport2D`.

| Hook | When |
|---|---|
| `SceneDrop_Enter/Exit/Update(view, …)` | Cursor enters / leaves / hovers the scene view while this asset is the grab payload. |
| `SceneDrop_CompEnter/Exit(comp, …)` | Hovered **scene-content** comp changes (picked via viewport `Trace_*`, not the 2D widget). |
| `SceneDrop_DropOnComp(comp, …)` | Release. Returns the spawned comp. `comp` is the picked scene node, or `scene.root` if empty. |

Default impls are empty. `ImpScene` instances the hierarchy. `A_Mesh` spawns a `C3_Mesh` on a 3D viewport. File-browser payload is a **path string** → `ImpAsset.Load`.

`A_Mesh.GEO_CUBE` / `GEO_PLANE` set `filepath` to `builtin:A_Mesh.GEO_*` so they survive JSON.

## File_JSON (`Engine/Files/File_JSON.cs`)

Reads/writes ImpVar fields only (public+private instance). Public-field fallback for non-asset objects. Does not serialize `ImpComp.scene` / `parent` as fields.

Hierarchy: `Comp_ToJson` / `Comp_FromJson` — used when the asset is an `ImpScene` (`vars.root`). User children are a JSON array. Native field comps go in `owned` and are applied onto the ctor instances (never `Child_Add`’d again).

## ImpComp.Type_FromName

Resolves a concrete `ImpComp` subclass by short type name (`C3_Mesh`, `ImpComp`, …) across loaded assemblies. Used by scene load.

## PNL_SceneTree (`Editor/Panel/PNL_SceneTree.cs`)

Editor outliner panel. Search bar + `C2_Tree`. Binds `scene` (or `root_comp` if set). Refreshes on hierarchy sig. Skips `IsOwned` and `IsPackedForeign` (those live in the inspector Components tree). `WND_Scene` owns click/drop (inspector + gizmo + reparent). External payload drop (`on_item_drop_external`) of a `Type` adds that comp as a child (`AddComp` / `AddChild`).

## PNL_CommonComps (`Editor/Panel/PNL_CommonComps.cs`)

"Comps" tab next to the Outliner (`WND_Scene.tab_outliners`). Inner 3D / 2D tabs are `EdCommonCompsCategory` lists (`root_type` = `Imp3D` / `Imp2D`). Each category collects concrete, non-hidden descendants with `[ImpClass(Common = true)]` (`C2_Tree.Class_IsCommon` — not inherited). Tiles (`EdCommonCompTile`) grab with payload `Type`. Drop on `PNL_SceneView` spawns a ghost at the cursor (switches 2D/3D mode to match) then parents under the pick / scene root. Drop on the outliner parents under that row. Double-click adds under the current selection (or root).

## ImpClass

`[ImpClass(Hidden = true)]` hides the type **and** subclasses from class pickers. `[ImpClass(Common = true)]` lists the type on the Comps palette (per-type, not inherited). Already on the usual spawnables: `C3_Mesh` / `Light` / `Camera` / `Audio` / `PlayerStart` / `Transit`, `C2_Box` / `Button` / `List` / `Image` / `Text`.

## PNL_SceneView (`Editor/Panel/PNL_SceneView.cs`)

Scene viewport tab (`C2_Box`, `cursor_filter = Hit`). Owns `scene`, `undo`, `edit_mode`, `gizmo_data`, `C2_Viewport3D` + `C2_Viewport2D` (both `Pass` so clicks land on the panel), 2D/3D gizmos, camera nav, marquee/selection, asset drop, and the mode/gizmo/space/snap toolbar. `WND_Scene` still hosts the tab box, selection/inspector bind, and dup/delete hotkeys.

Inner `tabs_view` pages: **Scene** (`view_root` — toolbar + 3D/2D viewports), **Game** (`PNL_GameView`), **Script** (`PNL_ScriptGraph`). Camera / gizmo / marquee only run on Scene. Play binds PIE into Game and selects that tab.

## PNL_GameView (`Editor/Panel/PNL_GameView.cs`)

Play-in-Editor tab (`EdPanel`, tab name `Game`) inside `PNL_SceneView.tabs_view`. Owns `C2_GameView`. `Play(game, cam3)` binds the session and focuses the widget; `Stop()` unbinds. The play scene ticks from `C2_GameView.OnUpdate` even while the Game tab is hidden (`ImpComp.Update` does not skip `is_visible`). Draw only happens on the selected tab.

## PNL_ScriptGraph (`Editor/Panel/PNL_ScriptGraph.cs`)

Pulse graph editor (`EdPanel`, tab name `Script`). Binds the open `ImpScene.Script_Get()` (builtin unless `script_override` is set). Layout: override list (left) + `C2_GraphEdit` + defaults inspector (`A_Script` parent_type / vars).

- Left list: `[PulseOverride]` methods on `parent_type`. Green = already in the graph. Click adds or focuses the event node.
- RMB empty canvas: Add Override / Call Function (`[PulseCall]`) / Variables / Flow. Context is **Self** (`parent_type`). The menu is `ScriptNodes.Menu(ctx, self_ctx, script)` — the panel only turns entries into `TPopupMenuOption`s, grouped by `category` (separator on change).
- Drop a wire on empty canvas: same menu. If the pin is an object type, context is **that type** (not Self), `self_ctx = false` (no Overrides / Flow) and the new node auto-wires.
- `BuildWidget` no longer knows any node's shape: it calls `ScriptNodes.Create(pn, ctx, script)` and maps `GetNode_Title/Color/Chrome` + `Slots_Build` rows onto the widget through `ApplySlot`. One `Spawn(TScriptNodeMenu, …)` replaces `SpawnFunc` / `SpawnVar`.
- Graph widget stays generic — Pulse node data is `TPulseNode` on `C2_GraphNode.user_data`. No compile / VM yet.

## C2_GraphEdit / C2_GraphNode (`Engine/Comps/2D/C2_Graph.cs`)

Generic Godot-like graph canvas. **No scripting types in here** — Pulse / anim / material graphs all reuse this.

`C2_GraphEdit` owns `scroll_offset` + `zoom`, `connections` (`TGraphLink`: from node/slot → to node/slot), grid, snap. Children that are `C2_GraphNode` are placed each frame: `transform.position = (graph_position - scroll) * zoom`, `layout.size = graph_size * zoom`.

`C2_GraphNode`: `title`, `graph_position` / `graph_size` (graph space), `title_color`, `slots` (`TGraphSlot`: left/right enable, type int, color, name). `Slot_Set(index, …)` grows the list. Same `type` required to connect. One wire per input (new connect replaces). Outputs fan out.

Unconnected **value** inputs (`TGraphSlot.edit_left`) host an inspector-style widget (`C2_TextEdit` / slider / checkbox / dropdown / vector / color) as a child of the node. Layout is `transform.position` (same as other Imp2D chrome) via `SlotEdits_Layout`, called from the graph's place-nodes pass so it lines up with the pin. Clicking the widget selects the node but does not drag it. The widget hides while that input is wired. Pin text fields set `C2_TextEdit.hog_input = false` so they type without swallowing graph input; Delete / Ctrl+A stay on the field while it is focused. `TGraphSlot.value` is the live default; `on_slot_value` notifies the host. `C2_GraphEdit.IsInputConnected` / `C2_GraphNode.SlotEdits_Rebuild` / `SlotEdit_Hit`.

Input (Godot-ish):
- Pan: MMB, Space+LMB, or RMB on empty canvas
- Zoom: wheel toward cursor
- Drag node (selected move together). Ctrl inverts snap
- Box select (Shift additive)
- Drag output → input to connect. Drag a wired input to pull the wire off and rewire (`right_disconnects`)
- RMB on a wire deletes it. Delete/Backspace: selected nodes (and their wires) or a selected wire
- Ctrl+A select all

`Connect` / `Disconnect` / `CanConnect` are the API. `on_connection` / `on_disconnection` fire after the list changes. `on_context_empty(screen)` — RMB on empty canvas (replaces RMB-pan when set). `on_connect_drop(screen, node, slot, from_out)` — released a wire on empty. `on_node_removed` before Destroy. `TGraphSlot.data_left/right` optional `Type` for host graphs. `C2_GraphNode.user_data` is host payload.

Node chrome uses `{engine}/Textures/Graph/` (UE GraphEditor brushes, MIT). `C2_GraphEdit.style` is `UI_Graph`. `C2_GraphNode.chrome` = Regular (body + title spill/gloss + shadow) or Var (compact get/set). Pins/wires still drawn in code. Missing textures fall back to a tinted rect.

RMB empty still pans if `on_context_empty` is null. MMB / Space+LMB always pan.

## A_Script / Pulse (`Engine/Assets/A_Script.cs`, `Engine/ImpGraph.cs`)

Pulse is Imperium's visual script (UE Blueprints-like). File ext `ImpScript`.

`A_Script`: `parent_type` (`TClass<Object>` — `Get()` resolves by type name), `nodes` (`List<TPulseNode>`), `connections` (`List<TGraphConnection>` Guid+pin), `vars` (`List<TScriptVar>`). Hidden ImpVars stay out of the inspector.

`TPulseNode.node_class` names the `SN_*` class that describes the node (empty on nodes saved before it existed — `ScriptNodes.Create` then falls back to `kind`). `TPulseNode.pin_values` (`List<TPulsePinValue>`: name / type_name / invariant text) stores unconnected input defaults. `Pin_Get` / `Pin_Set`. `Pulse.CanEditDefault` is string / bool / numbers / Vector2-4 / Color / enum (not exec, not object Target). Script graph marks those left pins `edit_left` and rebuilds widgets after `BuildWidget`.

**Where scripts live**
- `ImpScene.script_builtin` (always present) + optional on-disk `script_override`. `Script_Get()` prefers override.
- **A scene script is authored against `ImpScene.RootType_Get()` (the `root_type` comp class), never `ImpScene`.** ImpAssets are not scriptable; the script treats the scene as a custom subclass of its root comp, so the root's ImpVars / `[PulseCall]`s / `[PulseOverride]`s are what's in scope (and `Self` / an unconnected `Target` means the root comp). `Script_Get()` re-syncs `parent_type` to `root_type` every call, which both follows a root-class change and migrates scenes saved when the builtin was parented to `ImpScene`. Editing `parent_type` on a scene builtin in the Defaults inspector will snap back — it is derived. The graph's source label shows the context: `Builtin (ImpComp)`.
- `ImpComp` has the same pair; parent_type defaults to the comp's class. Scene Script tab edits the **scene** script, not the selected comp.

**Attributes** (`Engine/Attributes.cs`)
- `[PulseCall]` — callable from the graph (void = exec node, non-void = pure).
- `[PulseOverride]` — overridable event (left list + RMB). On `ImpComp`: OnBegin / OnEnd / OnUpdate / Input_* / OnPopupSelect. On `ImpScene`: OnBegin / OnEnd / OnUpdate. Also `Print(string)`, `Destroy`, `IsVisibleInTree` as calls.

`Pulse` static: type id/color, Overrides/Calls/Vars reflection. Exec pin id is 0.

**`Pulse.Vars` is strict on `EImpVarEdit`** — `Edit = None` (the default on all 296 `[ImpVar]`s today) means *not scriptable*, so it never appears in the graph. `ReadOnly` = Get only, `ReadWrite` = Get + Set. Same rule on `TScriptVar.edit` for script-local vars. Annotate `Edit =` on a var to make it scriptable; the Variables category is empty until then. `Inspect` / `Hidden` are unchanged and still drive the inspector.

## ScriptNode / SN_* (`Engine/Script/ScriptNode.cs`, `Engine/Script/Node/SN_*.cs`)

The SN_* classes own what a node **is**; `TPulseNode` stays the serialized form and `A_Script` stays the file. `ScriptNode : ImpGraphNode` has `context` (type the member came off), `member`, `script`, and:

- `Bind(TPulseNode, Type)` — copies id / member and fills any `[ImpVar]` field on the node from the matching `pin_values` entry, so `SN_If.condition` / `SN_Delay.time` equal their pin defaults.
- `GetNode_Title()` / `GetNode_Color()` (both from `ImpGraphNode`) / `GetNode_Chrome()`.
- `Slots_Build(List<TScriptSlot>)` — pin rows top to bottom. `TScriptSlot` mirrors `TGraphSlot`'s left/right layout **without** referencing the widget; an enabled pin with a null type is exec.

`is_available` = the node can be added anywhere from the menu. True for flow nodes (`SN_If`, and everything under `ScriptNodeAsync` — `SN_Delay`). **False** for the reflected ones, which exist only as one instance per member: `SN_Func` per `[PulseCall]`, `SN_Event` per `[PulseOverride]`, `SN_VarGet` / `SN_VarSet` per scriptable `[ImpVar]`.

`ScriptNodes` (static) scans the assembly once for concrete `ScriptNode` subclasses, keeping a prototype of each to read `is_available` / title.
- `Create(pn, ctx, script)` → instance, by `TPulseNode.node_class`; falls back to `kind` for nodes saved before `node_class` existed (VoidOverride → `SN_Event`, Var → `SN_VarGet`/`SN_VarSet` by `is_set`, else `SN_Func`).
- `Menu(ctx, self_ctx, script)` → `List<TScriptNodeMenu>` (category / text / node_class / kind / member / is_set / is_disabled).
- Abstract bases (`ScriptNodeAsync`, `SN_Var`) are skipped by the scan, so they never show up as addable nodes.

`SN_Var.Var_Type()` resolves the pin type: reflected `[ImpVar]`, else `A_Script.Var_TypeOf(name)` for script-local vars, else `object`.

## Pulse runtime (`Engine/Script/ScriptVM.cs`)

`A_Script.Compile()` turns the authored graph into a `TScriptProgram`: one `ScriptNode` per `TPulseNode` (via `ScriptNodes.Create`), the wires copied out of `connections`, and `errors`. Node pin rows are built **after** `Compile_Check`, because a check can rebind a node to another type. `compiled` / `compile_dirty` are plain fields — never serialized, so a script off disk always compiles once. `Program_Get()` compiles if dirty; `Clone()` resets both so a PIE copy compiles its own.

`ScriptVM(program, self)` is one running instance. **`self` is what an unwired `Target` means** — for a scene script that is the root comp.

- `Event_Run(member, args)` finds the `SN_Event` by name, stores args, and walks exec from its slot 0.
- `Exec_Follow(node, out_slot)` walks output→input; each `ScriptNode.Exec_Run(vm)` returns the exec **output slot** to continue from, or `EXEC_STOP` / `EXEC_LATENT`. `STEP_LIMIT = 4096` stops a looping graph from hanging the editor.
- `Input_Get(node, slot)` pulls through the wire (`Value_Get` on the source, so pure nodes run on demand, UE-style) or falls back to `TPulseNode.pin_values`.
- Latent: `Latent_Schedule` + `Update(dt)` run `SN_Delay` off a per-VM timer list — no threads.
- Script-local `A_Script.vars` live in the VM's `_locals`, keyed by name.
- `Value_As` coerces pin values into parameter types (defaults round-trip through text).

Node runtime lives on the SN classes: `SN_Func` reflection-invokes (`Slot_Target` / `Slot_Params` mirror `Slots_Build`), `SN_VarGet` / `SN_VarSet` read/write the member or the VM local, `SN_If` returns exec 0 (True) or 1 (False), `SN_Delay` schedules and returns `EXEC_LATENT`, `SN_Event` serves args from slot-1 onward.

**Stale `target_type` rebind:** nodes authored when scene scripts were parented to `ImpScene` still say `target_type: "ImpScene"`. `Compile_Check` rebinds a call/var to the script's own `ParentType_Get()` when the saved owner is not compatible with it — **unless `Target` is wired**, which means the author meant that other type. Without this those nodes throw at invoke time.

**Hooks:** `ImpScene.RBegin` compiles + creates `script_vm` (self = `root`), sets `root.script_vm = script_vm` (see `ImpComp.Script_Event` below), and fires `OnBegin`; `Update` ticks latents then fires `OnUpdate(dt)`; `REnd` fires `OnEnd`, then clears `script_vm` and `root.script_vm`/`root.input_owner`. Scripts only run while `is_running`, so the editor scene never executes. **Comp scripts (`ImpComp.script_builtin`) are not hooked** — no comp script editor, and only the scene's `RBegin`/`REnd` create a VM; a comp's own `script_builtin`/`script_override` is never compiled or run.

`ImpComp.script_vm` + `Script_Event(member, args)` — the comp-side half of routing. `Update_Input` calls both the C# virtual (`Input_Pressed` etc.) and `Script_Event("Input_Pressed", new object[]{ player, action, axis })` (matching parameter order, since an `SN_Event`'s output pins are the override's args in order). Only the scene **root** comp has `script_vm` set (by `RBegin`), so only input delivered to the root reaches the scene graph — a non-root comp's Input_* events currently have nowhere to go since comp scripts aren't hooked.

Editor: **Compile** button in `PNL_ScriptGraph` (left column) calls `Compile()` and reports count / errors to the label and Console. `MarkDirty()` sets `compile_dirty`.

`SN_VarGet.is_validate` and `EScriptVarSize` are intent for later; only `SN_Func.is_execute` is used today.

**Not yet:** custom functions (`FuncInOut`), node palette beyond RMB, comp Script tab / comp script execution, undo, pin textures, pure-node caching (a fanned-out pure node re-evaluates per consumer each frame).

Takeover brief: `Agents/Handoff_ScriptGraph.md`.

Right-click a comp: Duplicate / Delete / Change Type / Add Child. Right-click empty tree: Add Comp. Change Type is disabled on instance roots and packed foreign. Add Child/Comp disabled on instance roots and packed foreign. Dup/Delete disabled on packed foreign and the scene root. Change Type / Add Child / Add Comp open `DLG_ChooseComp` (`Dialog_ClassPicker` of `ImpComp`).

## ImpDialog (`Engine/ImpDialog.cs`)

Not an ImpComp. One modal at a time, like the popup. Slot is `ImpPlayer.current_dialog` (static). `Show()` closes any open dialog + popup, parents a full-screen overlay + dimmer shade on `C2_MenuBar.PopupHost()`, and hogs **all** input until a choice closes it.

| Member | Notes |
|---|---|
| `ImpPlayer.current_dialog` | The open instance, or null. |
| `ImpDialog.IsOpen` / `Host` / `Contains(comp)` | Gate for `Key_Allowed` and cursor. |
| `on_dismiss` | Shade click + Escape. Subclasses set this to their No / Cancel / OK. |

`Key_Allowed` gate is `ImpDialog.Host ?? Popup_Host ?? input_hog`. Widgets under the overlay still type. Cursor hover / events / grab / focus / RMB popup stay off anything outside. `C2_TextEdit` outside the dialog unfocuses. `Popup_Run` refuses while a dialog is open. `Update_Input` closes a dialog whose overlay left the live scene.

Chrome (`C2_DialogHost` / `C2_DialogShade`) is hidden. The panel is a later sibling of the shade so it hit-tests above it. `Run()` always news a fresh instance — do not Detach the panel before Close (that used to orphan the hog).

**Open via each type's static `Run(...)`** — args define the instance, `Action`s are the choices:

| Type | Run |
|---|---|
| `Dialog_Alert` | `Run(message, on_ok, text_ok?)` |
| `Dialog_Confirm` | `Run(message, on_yes, on_no?, text_yes?, text_no?)` |
| `Dialog_ClassPicker` | `Run(root_type, on_picked, on_cancel?, title?, current?, allow_none?)` |
| `Dialog_AssetPicker` | `Run(asset_type, on_picked, on_cancel?, current_path?, title?, allow_none?)` |
| `DLG_ChooseComp` | `Run(on_picked, title?)` |
| `DLG_ConfirmDelete` | `Run(message, on_yes, on_no?)` — Confirm with Delete / Cancel |
| `DLG_NewScene` / `DLG_NewAsset` | `Run(folder?)` |
| `DLG_SaveFile` | `Run(asset, on_save, folder?)` |

`Dialog_ClassPicker` hosts `C2_Tree.Tree_Populate_FromClasses` plus a search bar (`Tree_FilterClasses`). Scene tree rows use `C2_Tree.Class_Icon`. `DLG_ChooseComp` roots at `typeof(ImpComp)` and lists every concrete descendant. Skips the Editor assembly. `[ImpClass(Hidden = true)]` on a type hides it **and** every subclass (`C2_Tree.Class_IsHidden` walks bases). Labels strip `C1_` / `C2_` / `C3_` (`Class_DisplayName`). Icons autoload `{engine}/Icons/type/{Name}.png` then `Icons/Types/`, walking bases, then `ICO_COMP*`. Abstract classes stay in the tree as grey `is_disabled` rows (grouping only — click expands, no select/confirm). Confirm via OK or double-click. Shade / Cancel closes. Inspector `TClass<T>` rows open this with `allow_none: true` (a **None** row at the top) and pre-select the current type — do not use the old `C2_Picker` dropdown.

`Dialog_AssetPicker` is the Godot-style resource picker. Search + folder tree of `ImpAsset.Files_OfType` / `Builtins_OfType` (Game / Engine / Content / Builtins), **None** at the top, selected name + tokenized path, OK / Cancel / double-click. Clicking an inspector `TRef<T>` or `C2_AssetSlot` field (`C2_Picker.on_open`) opens it. Drag-drop onto the slot and the clear **×** still work on the compact field. `on_picked` gets a tokenized path (`{game}/…` / `builtin:…`) or `""` for None.

`C2_Picker.on_open` — if set, click runs that instead of the inline popup. Keep the compact field for display / drop / clear.

`is_create_new` (on `DLG_NewScene` / `DLG_NewAsset`) shows a **blank** name field — no default `NewScene` / `NewAsset` so Create is refused until they type one. Invalid filename chars and an already-existing path keep the picker open (`stay_open` + `Hint_Set`). Create writes into the current file-browser folder (`PNL_FileBrowser.Folder_ForCreate` — engine Content is redirected to game Content).

| Dialog | Root class | File | Root / instance |
|---|---|---|---|
| `DLG_NewScene` | `ImpComp` | `{name}.ImpScene` | Instantiates the picked class as `scene.root`, sets `root_type` |
| `DLG_NewAsset` | `ImpAsset` | `{name}.{File_GetExtension()}` | Instantiates the picked asset class |

`Scene_Editor.MOpt_New_Scene` / `MOpt_New_Asset` (File menu + main buttons) and the file-browser New Scene / New Asset entries all call `DLG_New*.Run(folder)`. After write: `Browsers_Notify` + `ImpAsset.Editor_OnOpenAsset`.

`TTreeItem` is a struct — never mutate a parent item after inserting it; build children first, then the node.

## Who assigns `ImpScene.current.root`

- `Editor/Program.cs` → `Scene_Editor`
- `Engine/Program.cs` → bare `ImpComp` + demo children

Editor chrome (windows, file browser, popups) lives **inside** `current` as UI comps, so they also get `scene == ImpScene.current`. Edited game scenes are separate `ImpScene` instances shown by `PNL_SceneView` through `C2_Viewport3D` / `C2_Viewport2D`.

## ImpGame / Play-in-Editor (`Engine/ImpGame.cs`)

C# statics cannot be instanced (one AppDomain, and Raylib/R3D/Jolt are process-global). Game-scoped state lives on `ImpGame` instead. **Not** `A_Game` — that asset is the project (`.ImpGame` file). `ImpGame` is the live session.

| Lookup | What |
|---|---|
| `Get(0)` / `ID_HOST` | Editor / standalone. Created by `EnsureHost` / first `Get(0)`. `scene` is the chrome tree. |
| `Get(1)` / `ID_PLAY` | PIE session, or null when stopped. `scene` is a `PlayCopy()` of the authored scene. |
| `current` | Ambient game for the tick in progress. `ImpScene.current` returns `current.scene`. Prefer `this.game_owner` on a comp. |

`ImpApp` calls `EnsureHost()` after post-init. `C2_GameView` binds `current` to `Get(1)` around the play `Update` / `Draw`, then restores `Get(0)`. Editor chrome therefore still lives under host; game comps that read `this.game_owner` or `ImpScene.current` during their own tick see the play session.

`C2_GameView.view_game` is the session it *shows* (PIE). `ImpComp.game_owner` on that widget is still Get(0) — it lives in the editor tree.

**Play** (`Scene_Editor.MOpt_Play`, also `PIE_Play` = Alt+P): `Play_Start` clones the active `PNL_SceneView` scene, binds it on that tab's `PNL_GameView` (`C2_GameView` inside), copies the 3D camera, selects the **Game** inner tab. Play again is a no-op while PIE is live. Scene / Script stay usable — switch back to edit the authored scene while play keeps ticking (Update does not skip hidden tabs).

**Stop** (`MOpt_Play_Stop` / toolbar Stop / `PIE_Quit` = Alt+Escape): unbinds every scene tab's Game view, then `Play_Stop`. Closing the hosting scene tab also stops PIE. Toolbar: Stop is `is_disabled` when `Get(1)` is null; Play and Play From Start are `is_disabled` during PIE.

`C2_GameView` ticks `view_game.scene` under `Bind`. It lives in `PNL_GameView` (the Game inner tab), not as an overlay on Scene. 3D is `C2_Viewport3D` with `R3D.SetAspectMode(Expand)` so the blit fills the widget. 2D HUD (`clear_background` / `draw_canvas` false) draws in-place over that blit — not through a second RT (R3D scissor leftover was covering the bottom of the 3D image). Physics is per-`ImpGame` (`ImpGame.phys`). `ImpPlayer.players` is still process-global, but **action input is now routed per session** — see below.

### Input target game

`ImpPlayer.target_game` (null = host) is the session a player sends action input to. `TargetGame_Get()` resolves null to `Get(ID_HOST)` — **never `ImpGame.current`**, because the gate runs inside `C2_GameView`'s `Bind(view_game)` window where `current` *is* the PIE game; a `current` fallback makes every PIE comp match and the feature silently no-ops. `TargetGame_Set` refuses a session that is no longer registered. `ImpPlayer.TargetGame_IsHost(player)` is the editor-side test (fails open).

`ImpComp.Update` delivers `Update_Input` only when `game_owner == input_owner.TargetGame_Get()`; a null `game_owner` counts as host (`Destroy()` nulls it, so "always allowed" would keep feeding a dead tree). `input_hog` stays the outer, absolute veto. This only affects comps that called `Input_SetOwnerActive` — editor widgets run off `Cursor_OnEvent` / `target_focus` and are untouched, so the editor stays usable during play.

`C2_GameView` claims on `Bind` (Play starts focused, for **all** players — only player 0 has a cursor, so gamepad players could never click in), on `_Notify_AsFocusTarget(Begin)`, and on `Cursor_OnEvent(Select_A/B)`; it releases on focus `End` and on `Unbind`. The `Cursor_OnEvent` claim is not redundant: focus `Begin` only fires when `target_focus` *changes*, so a future "force back to editor" hotkey would otherwise leave you unable to click back in.

Reset is three overlapping layers, because a lockout is unrecoverable: `ImpGame.Play_Stop` (authoritative, covers every stop path), `C2_GameView.Unbind`, and a stale-session backstop in `ImpPlayer.Update_Input` beside the `Target_IsLive` cleanups.

Ordering: `target_focus` is assigned in the **Cursor** phase, after Update. So click-in takes effect next frame (the focusing click is swallowed rather than double-firing as a game action — correct), and click-out leaks one frame of input to the game (harmless).

Editor gating: `WND_Scene.HandleEditHotkeys` early-returns unless the host holds input — Delete / Ctrl+D act on the *authored* selection and would otherwise let a game bound to Delete destroy real comps mid-play. Undo/redo and the menu bar are deliberately **not** gated. `PIE_Quit` uses `Action_IsPressed`, which ignores `Key_Allowed`, so the escape hatch always works. Do **not** fold the target test into `Key_Allowed` — it would kill panel resizing, scroll boxes, sliders and dialogs during PIE.

Not covered: PIE comps are not cursor targets (`Update_Cursor` traces only the editor root, with `ImpApp.app.camera`), so game HUD buttons / 3D click targets do not respond — action input only. A PIE comp holding `input_hog` would keep input after click-out (`Update_Input` calls the hog directly).

`A_Texture.ICO_STOP` = `{engine}/Icons/ico_editor_stop.png` (same mint as play).

Do **not** try AssemblyLoadContext or a second process for PIE. Shared GPU assets stay on `ImpAsset` cache. Physics is already per-game (`ImpGame.phys`). Per-game later: `C1_GameMode.current`, transit, play-local players.

## C2_Viewport3D / C2_Viewport2D (`Engine/Comps/2D/`)

Dumb display widgets. Each takes `view_scene` and/or `root` (root wins) plus optional `overlay` (drop ghost, not in the tree). Do **not** name the viewed scene `scene` — that hides `ImpComp.scene` (the editor chrome tree). `transpose_traces` (default true) remaps mouse picks through the widget camera / rect (`Trace_Ray` / `Trace_Pick` / `Trace_World`). Standalone default `cursor_filter = Hit`. Editor sets `Pass` so `PNL_SceneView` receives clicks.

- 3D: R3D into a render texture, blit. Camera is on the widget; editor writes it.
- 2D: canvas fill + `SceneLayout_Set` / `SceneDraw_*` of 2D comps. `clear_background` / `draw_canvas` (default true) — Game view turns both off so 2D HUD composites over the 3D blit.

No gizmos, selection, or camera-drag on the viewports.

**Asset drop** (on `PNL_SceneView`)
- `overlay` — live ghost drawn after the scene, **not** in the tree until commit.
- Panel `_Notify_OnGrabDrop` + `_Notify_AsCursorTarget(Update)` call ImpAsset SceneDrop_*.
- `C2_Viewport3D.Trace_World` — mesh pick, else Y=0 plane. `C2_Viewport2D.Trace_World` — canvas point.
- Any active grab (`player.grab_is_active`) blocks camera / gizmo / marquee.
- File-browser drop target is `PNL_SceneView` (`ImpScene` or `A_Mesh`).

## ImpPlayer input actions (`TInputAction` / `TInputKey`)

`ImpPlayer.Update_Input` builds `action_states` / `action_axis` from `native_actions` + `input_actions`. Each binding is a main `EInputKey` → `TInputKey`.

`TInputKey.prereq_keys` — every listed key must be Held (`Pressed` or `Down`) or that binding is ignored. Empty / null = no chord. `None` entries are skipped. Example: `PIE_Play` is `Key_P` with prereq `Key_LeftAlt` (Alt+P). Releasing a prereq while the main key is still down Releases the action.

## ImpPlayer notify (`ENotifyGeneric` / `ENotifyGrabTarget`)

Cursor / grab / focus no longer use `Cursor_OnEnter` / `CursorGrab_Begin` / etc. ImpPlayer calls:

| Hook | When |
|---|---|
| `_Notify_AsCursorTarget` Begin/Update/End | hover enter / per-frame / leave |
| `_Notify_AsFocusTarget` Begin/Update/End | `target_focus` gained / per-frame while no `input_hog` / lost |
| `_Notify_AsGrabbedTarget` Begin/Update/End | drag past threshold / while held / on release |
| `_Notify_OnGrabDrop` | hover + drop, both sides |

`ENotifyGrabTarget`: `Hover_AsTarget_*` / `Drop_AsTarget` fire on the **grabbed** comp (`other` = drop target). `Hover_AsInstigator_*` / `Drop_AsInstigator` fire on the **drop target** (`other` = grabbed). `Cursor_OnEvent`, `CursorGrab_IsEnabled`, `CursorGrab_Payload` stay.

`target_focus` is the last clicked Imp2D (`target_cursor as Imp2D` on mouse press, unless `input_hog`). ImpPlayer fires Begin/End when it changes (`last_focus_target`). Editor `Scene_Editor.OnDraw2DForeground` prints targets top-right. `EdFileThumbnail` selection follows focus: click/grab selects, focus End deselects (`Thumbnail_Select(null)`).

`ImpPlayer.Target_IsLive(comp)` — `comp` is `ImpScene.current.root` or a descendant. Update_Input clears `input_hog` / `target_focus` that fail this (detached dialog widgets), and clears `target_game` when that session is gone.

`C2_GameView` uses `_Notify_AsFocusTarget` Begin/End as the click-in / click-out edge for `ImpPlayer.target_game` — see **ImpGame / Play-in-Editor → Input target game**.

## C2_Inspector (`Engine/Comps/2D/C2_Inspector.cs`)

Inspects `[ImpVar]` fields/properties on the selected object(s). Categories are `[Category]` or the declaring type name.

- Category expanders (`C2_Expandable`) show the class autoload icon (`C2_Tree.Class_Icon`) after the chevron — same `{engine}/Icons/type` then `Icons/Types` walk as the outliner. `C2_Expandable.icon` / `C2_Button.icon2`.
- Top `C2_SearchBar` (`show_search`, default on) filters by member name, pretty name, category, or a nested field. Matching categories stay expanded. File-browser settings inspector turns search off (`show_search = false`).
- `filter_property` (`Func<MemberInfo, bool>`) — if set, a top-level member is listed only when this returns true. Nested group / `Rows_ForObject` rebuilds pass `apply_filter: false` so a Config filter does not hide `TRef.path` etc.
- `include_static` — also collect public static `[ImpVar]`s (`Members_GetStatic`). Off by default so instance inspectors stay instance-only.
- Target may be a `Type` (`InspectType`): inspect that type's members instead of `System.Type`. `TPropertyBind.Member` binds statics via `GetValue(null)` / `SetValue(null, …)` and pulls revert defaults from `ImpConfig.Default_TryGet`.
- Comp inspector: top **Components** tree (`Tree_Populate_Components`) for public owned slots + packed-foreign kids. `on_component_click` retargets vars / gizmo. Host is `ImpComp.OutlinerHost`. No reorder.

## C2_TabBox (`Engine/Comps/2D/C2_TabBox.cs`)

Pages are every Imp2D child except `list_tabs`. `selected_tab` is the page index. `show_close_tab_button` draws a `×` on each tab and fires `Action<int> request_close_tab` with that page index — the TabBox does **not** close anything itself. `WND_Scene.tab_scenes` / `WND_Asset.tab_assets` set the flag and call `Scene_Close` / `Asset_Close`. Main chrome tabs (`Scene_Editor.ui_main_tabs`) leave it off. Closing a tab before the active one decrements `selected_tab`; closing the active tab keeps the index (next page slides in) and clamps if it was last.

**File browser delete / duplicate** (`PNL_FileBrowser` / `EdFileThumbnail`)
- Delete key on a focused thumbnail: empty folder goes immediately; files and non-empty folders open `DLG_ConfirmDelete` (`Delete` / `Cancel`). Context-menu Delete uses the same ask (`Path_DeleteAsk`).
- Content roots (Game / Engine) cannot be deleted or duplicated.
- `Path_Delete` removes the file or the folder tree, drops `ImpAsset` / `ImpFile` cache entries (`Cache_Drop`, including anything under a deleted folder), strips matching favorites, and walks the current dir up if it lived inside the deleted path.
- Ctrl+D (either Control) / context-menu Duplicate copies next to the original via `UniqueName` (`name_1`, `name_2`, …). Folders copy recursively. The copy is a new file — do not rekey the original.
- `WND_Scene` Delete / Ctrl+D (scene comps) skip while focus is inside `file_browser`, so they do not fire alongside the thumbnail.

Live browsers register in `_browsers`. Any disk change (`Path_Delete` / `Path_Duplicate` / `Path_Rename` / `Path_MoveInto` / new folder/scene / import / save) calls `PNL_FileBrowser.Browsers_Notify(from, to)`. Every instance remaps its current dir and favorites (follow a move, walk up a delete), then `RefreshAll`. A second browser sitting in the same folder updates without a manual Refresh. Grid rebuild keeps the selection if that path still exists.

`CursorGrab_Payload` from `EdFileThumbnail` / tree rows is a **filesystem path string**. Hover/drop go to `target_cursor` (the 2D widget, `PNL_SceneView` in the editor). Scene-content comps are **not** cursor targets — the panel picks them via viewport `Trace_*`.

## Related

- `ImpUndo.Place_Set` uses Detach / Child_Add / Child_Insert — scene cache follows automatically.
- `ImpPlayer` hit-tests `ImpScene.current.root`.
- `ImpPlayer.Popup_Run` / `C2_MenuBar.PopupHost()` parent the one global popup under `ImpScene.current.root`.

## ImpPlayer popup (`A_PopupConfig` / `C2_PopupMenu`)

There is only one popup menu. `C1_PopupMenu` is gone.

`A_PopupConfig` (`Engine/Assets/A_PopupConfig.cs`): `searchable` + `options` (`List<TPopupMenuOption>`). `TPopupMenuOption` lives in `ImperiumEngine.Comps._1D` (text, separator, disabled, `on_press`, suboptions).

`ImpComp.popup_config` non-null: RMB walks from `target_cursor` up and opens that config. Pick fires `opt.on_press`, then `popup_menu_callback`, then `ImpComp.OnPopupSelect`.

Programmatic: `ImpPlayer.Popup_Run(target, config, on_select, screen_pos?)`. `screen_pos` null = cursor. Script graph sets `searchable = true`.

While open (`popup_menu_open`):
- `Key_Allowed` gate is `ImpDialog.Host ?? Popup_Host ?? input_hog` — keys only work under the popup. Dialog wins if both would be open (popup cannot open while a dialog is).
- Cursor events / hover / grab / focus stay on the popup. Click off (LMB/RMB) or Escape or picking a leaf option closes it.
- `C2_TextEdit` outside the popup unfocuses so `GetCharPressed` cannot leak into a leftover field.

Visual is hidden `C2_PopupMenu` (`C2_Box` + optional `C2_SearchBar` + `C2_List`). Search filters current page (separators hidden while typing; Enter picks the first enabled row). Submenus replace the page and open to the right.

## Godot: PackedScene instances (RefRepos/godot)

**Not a flattened copy.** Dropping `enemy.tscn` into `level.tscn` stores a *reference* to the packed scene plus local overrides. Runtime still builds real Node objects (so they draw/simulate); those objects are *owned by the packed scene*, not by the host.

### Two related features

| | Instancing | Inheritance |
|---|---|---|
| What | Child node in scene A is `PackedScene.instantiate()` of B | Scene A's **root** is an instance of B, then A adds/overrides |
| On disk | `[node name="Enemy" instance=ExtResource("enemy.tscn")]` | Root `type` omitted / `TYPE_INSTANTIATED` + `base_scene` |
| Editor | Nested under host; children locked unless "Editable Children" | Inherited nodes greyed; you add new siblings |

### Runtime (`PackedScene::instantiate` → `SceneState::instantiate`)

1. Walk `SceneState.nodes[]`. For a node with `instance >= 0`, load that `PackedScene` and **recursively instantiate it** (`GEN_EDIT_STATE_INSTANCE` in editor, `DISABLED` in game). The returned **root** of B becomes the child in A.
2. Then apply **only this scene's property overrides** onto that root (and onto any nested nodes that this scene recorded changes for).
3. Mark the root `scene_file_path = B.tscn`. `Node::is_instance()` is just `!scene_file_path.is_empty()`.
4. `NOTIFICATION_SCENE_INSTANTIATED`.
5. Children that came from B are created inside B's instantiate — A does **not** list them. Their `owner` is B's root (or a node inside B), **not** A's edited-scene root.

`GEN_EDIT_STATE_*` only affects editor bookkeeping (`scene_instance_state`, inherited state, duplicate arrays). Game instantiate is the same tree, no edit state.

### What gets saved in the host (`SceneState::_parse_node`)

Skip any node whose `owner` is not the scene being saved **unless** that owner is an **editable instance**.

For a nested instance root owned by the host:
- Store `instance = PackedScene` (or placeholder path).
- Store **delta properties only** — compare against the packed scene's defaults (`PropertyUtils` states stack). Unchanged transform/mesh/etc. are omitted.
- Do **not** store `type` (uses `TYPE_INSTANTIATED`).
- Do **not** walk/save the instance's children (they belong to B).

If the user overrode a *nested* node inside B (only possible with Editable Children), that one extra node is saved as `TYPE_INSTANTIATED` + changed props + a parent path, **still no type**. On load, instantiate finds the already-created child by name/id and patches it.

Optional `[editable path="Enemy"]` restores the editable-children flag.

`InstancePlaceholder`: deferred load — a stub node holding the path + stored property sets, `create_instance()` later.

### Editor ownership = lock

- **Owner** is who "owns" the node for save/edit. Host-owned nodes are yours. Nodes owned by an instance root are foreign.
- Outliner: instance roots get a link icon ("Open in Editor"). Foreign children cannot be deleted, reparented, or rearranged (`owner != edited_scene && owner->is_instance()`).
- Default: you **can** change properties on the instance *root* (position, name) — those are the deltas. You **cannot** edit/delete its children unless `set_editable_instance(node, true)`.
- "Make Local" / unbind: drop the `scene_file_path`, take ownership of the subtree, now it's a real copy.

### Imperium mapping (current vs target)

**Implemented.** See **ImpScene instances (design)** below.

## ImpScene instances (design)

Godot-like capsules. Live objects still exist (draw/simulate). Host document stores a **ref + root overrides**, not a flattened child dump.

Do **not** reuse `ImpComp.scene` for this. `scene` stays “which ImpScene document am I attached to” (the host). Packed identity is separate.

### State on `ImpComp` (not ImpVar)

| Field | Meaning |
|---|---|
| `TRef<ImpScene> packed` | Set on the **instance root** only. Empty = not an instance. |
| `ImpComp packed_from` | Instance root that spawned this node. `this` on the root; ancestor on packaged children; `null` on host-owned comps. |

```
IsInstanceRoot  = packed.Get() != null && packed_from == this
IsPackedForeign = packed_from != null && packed_from != this
```

`scene` on every live node (root and boxes) is still the **host** ImpScene (level). `packed` on the root points at `boxes_3`.

### `ImpScene.Instantiate()`

Authoritative template is `ImpScene.root` on the **cached asset** (`ImpAsset.Load`). Never mutate that tree from an instance.

```
ImpComp Instantiate()
{
    ImpComp inst = root.Clone();          // clone of the *template*, not of a host
    BindPacked(inst, inst);               // root.packed = this, walk kids packed_from = inst
    return inst;
}
```

Cycle: refuse if `this` is already in the instantiate stack (A instances B instances A), or if instantiating a scene into itself.

**Every ImpComp type can be an instance root.** `Instantiate()` is `root.Clone()` + bind. The live root is the same concrete class as the packed root (`ImpComp`, `ImpComp2D`, `C2_Button`, `C3_Mesh`, `C1_GameMode`, …). No wrapper type. No “prefabs must be ImpComp3D.” `packed` / `packed_from` / JSON `instance` / editor lock all live on `ImpComp`.

Placement is best-effort on **that** root, not a reason to change its type:

| Packed root | SceneDrop / gizmo |
|---|---|
| `ImpComp3D` (any subclass) | `Position_Set` world (3D view) |
| `ImpComp2D` (any subclass) | `Position_Set` canvas (2D view) |
| plain `ImpComp` / 1D | Parent as-is. Authored child locals stand. No invented transform. |

A 3D group pivot is an authoring choice (make the packed root an `ImpComp3D`), not an engine requirement. `boxes_3` uses an `ImpComp3D` root so the instance can be placed/moved as a group.

### Disk (`File_JSON.Comp_ToJson` / `FromJson`)

Instance root only. `_class` is the packed root’s real type:

```json
{
  "_class": "ImpComp",
  "name": "Boxes3",
  "instance": { "path": "{game}/Scenes/boxes_3.ImpScene" },
  "vars": { "is_visible": true },
  "children": []
}
```

A `C2_Button` prefab would be `"_class": "C2_Button"` plus that button’s ImpVars (text, size, …). A `C3_Mesh` prefab would include `transform` / `mesh` as root overrides.

- Write `instance` when `IsInstanceRoot`.
- Do **not** emit `IsPackedForeign` children.
- Do **not** emit `IsOwned` children in `children`. Write them as `owned.{fieldName}` and apply onto the ctor instance on load. Legacy files that stuffed natives into `children` merge by type + name (`mesh` / `C3_Mesh`).
- v1: write the instance root’s ImpVars as today. v2: diff against `packed.root`.
- `_class` kept so a missing packed file can still spawn a placeholder of the right family.

Load: if `instance` path set → `Load` packed → `Instantiate()` → overlay name/vars onto the **root only** → ignore JSON children (v1).

### SceneDrop

Ghost = `packed.Instantiate()` (real root type), parked on `overlay`. Drop parents that root. Undo stays `ImpUndo.Comp_Moved`. Never wrap children in a fresh `ImpComp2D` / `ImpComp3D`.

### Editor lock (v1)

Packaged children (`IsPackedForeign`) are selected and edited like any other comp (inspector + gizmo). Tree identity stays locked:

- No delete, duplicate, reparent, or reorder.
- No parenting into an instance root or a foreign node (`Child_Add` / `Child_Insert` / `Reparent` refuse).
- Outliner grab is off for instance roots / foreign / owned. Inspector **Components** tree (not a reorderable Children list) is how you reach them.
- Instance root itself is host-owned: move, delete, duplicate the whole capsule. Duplicate is `Clone()` of that capsule (keeps overrides). `Instantiate()` (drop/load) skips children that are instances of the same scene so a self-drop cannot explode the template.

Outliner hides owned natives and packed-foreign kids. Instance roots stay as a single outliner row. Inspector Components tree: owned names use the **field** name (`mesh`) with a blue tint (`140, 180, 220`); instance / packed use yellow (`236, 196, 82`). Click inspects that node.

Host JSON still skips foreign children (root overrides only). Child var edits are live; persistence is later.

**Not in v1:** host-added nodes under an instance, Make Local, scene inheritance. Inheritance can wait — `root_type` is already the hook.

Gizmo selection outlines and axis lines do **not** occlusion-test against the scene (that was O(samples × meshes) on select and showed up as ~80ms inside the scene view draw).

## Layout cache (`ImpComp2D`)

Global epoch + per-instance stamps on `Dimensions_Get` / `Transform_Get` / `Size_Layout` / `Anchor_Offset` / `Scene_IsContent`.

- ImpApp bumps the epoch once per phase (input / update / cursor / draw).
- Setters, Child_*/Detach/Destroy, SceneLayout_Set, SceneDraw_Begin/End also bump.
- **Do not** bump per-comp inside `ImpComp.Update` — that was ~2 × comp_count epochs/frame and made Fill rects jitter (and could recreate the scene-view RT every frame).
- F3 HUD: `epochs` should sit near 4–10, not 1000+. F4 toggles the cache off for A/B.

## Viewport draw cost

`C2_Viewport3D.OnDraw2D` owns the 3D pass: `R3D.BeginPro` / root.Draw / `R3D.End` + blit. Gizmo overlay is `PNL_SceneView.OnDraw2DForeground`. Mesh `OnDraw3D` is only submit time; `R3D.End` is shadows + SSAO + bloom.

F3 HUD `scene view` block splits `rt` / `r3d` / `blit` / `gizmo`. SSAO defaults to 8 samples (32 was a default-quality footgun).

### Reload

`ImpAsset` cache means every instance of `boxes_3` shares one template. When that asset `File_Write`s (or is reloaded), walk the open host scene for `IsInstanceRoot && packed.path == that file`, `Instantiate()` fresh, re-apply the host root overrides (name, transform, visibility).

### Phasing

1. Fields + `Instantiate` + JSON `instance` key + SceneDrop + delete/dup/outliner guards.
2. Open-packed button, reload-on-save, delta vars.
3. Make Local (`packed` cleared, `packed_from = null` on subtree). Then editable children / inheritance.
