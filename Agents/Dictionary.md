# Dictionary

Living lookup so agents do not re-read source. Update this when you learn or change something.

## ImpComp (`Engine/ImpComp.cs`)

Scene-graph node. `Imp2D` / `Imp3D` inherit.

| Member | Notes |
|---|---|
| `name` | ImpVar. Defaults to type name. |
| `is_visible` | ImpVar. Local only — `IsVisibleInTree()` walks ancestors. |
| `parent` / `children` | Tree. `children` is readonly list, mutate via Child_* / Detach. |
| `scene` | Cached owning `ImpScene`. **Not** ImpVar. Property: assign cascades to descendants. |
| `input_owner` | `ImpPlayer` that feeds input into this subtree. |

**`scene` contract**
- Set on attach: `Child_Add` / `Child_Insert` copy `parent.scene` onto the child (cascades).
- Cleared on `Detach` and `Destroy`.
- Scene root is bound by `ImpScene.root` setter (`root.scene = this`).
- Loose / cloned comps have `scene == null` until attached under a bound root.
- Clone skips `_scene` (and parent/children/input_owner/is_destroying). Re-attach to bind.

**Tree ops**
- `Child_Add` / `Child_Insert` Detach first (or reorder if already a child).
- `Reparent` keeps world transform for 2D/3D, then Child_Add/Insert.
- `Destroy` destroys children first, then unhooks parent + clears scene.
- Update/Draw snapshot children via `ArrayPool` (not `ToArray`) because those passes may reparent/destroy.

**Do not confuse with** `Imp2D._scene_root` — static per-frame flag for “this subtree is scene canvas content” (layout/pivot/rotation). That is **not** `ImpComp.scene`.

## Engine `_Content`

Source lives at `Engine/_Content`. `Engine.csproj` copies it next to the exe (`CopyToOutputDirectory`). `{engine}` resolves to `AppContext.BaseDirectory/_Content` via `ImpFile.ContentDir_Engine()`. Game content is still `{game}` → `<game>/Content`.

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

## ImpScene (`Engine/ImpScene.cs`)

`ImpAsset` subclass. File ext `ImpScene`. A scene **is** a hierarchy of ImpComps under `root`.

| Member | Notes |
|---|---|
| `current` / `global` | Static scenes. `current` is the live app/editor tree. |
| `root` | Property. Assign unbinds old tree (`scene=null`), Detach, binds new (`scene=this`). Null coalesces to a fresh ImpComp. |
| `root_type` | ImpVar `TClass<ImpComp>` — intended root class when creating a scene, not the live instance. |
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
| `[window]` | Main tab name, inspector/outliner tab, scene + asset file-browser expanded/stretch (`file_browser_expanded`, `file_browser_asset_expanded`, `browser_stretch`, `browser_asset_stretch`), splitter sizes (`panel_width`, sidebar) |
| `[tabs]` | Active scene tab index, active asset tab index |
| `[[open_scenes]]` | Saved scenes only (`File_CanWrite`). Camera 3D/2D, edit/gizmo/snap, selected comp name-paths |
| `[[open_assets]]` | Open asset file paths |
| `[file_browser]` | Scene window `PNL_FileBrowser`: current dir, Game/Engine tab, search, show flags, thumbnail size, favorites, expanded folders |
| `[file_browser_asset]` | Asset window `PNL_FileBrowser`, same keys. Independent of the scene browser. |

Untitled tabs are not persisted (no path). Missing `Editor.TOML` keeps the default preview scene. Saved scenes replace that preview.

`A_Game.GetRootDir()` uses `gamepath` first and ignores `builtin:` filepath. `Builtins_All` must not stamp `GAME_TEST.filepath` — that made `ContentDir_Game()` fall back to `{cwd}/Content` (the engine content copy). Result: Game tab listed Fonts/Icons, and `{game}/Scenes/…` restore missed project scenes.

`File_TOML` (`Engine/Files/File_TOML.cs`) is a small tables / array-of-tables reader-writer used by `EdState`. Not a full TOML 1.0 impl.

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

Hierarchy: `Comp_ToJson` / `Comp_FromJson` — used when the asset is an `ImpScene` (`vars.root`). Children are a JSON array, not ImpVar.

## ImpComp.Type_FromName

Resolves a concrete `ImpComp` subclass by short type name (`C3_Mesh`, `ImpComp`, …) across loaded assemblies. Used by scene load.

## PNL_SceneTree (`Editor/Panel/PNL_SceneTree.cs`)

Editor outliner panel. Search bar + `C2_Tree`. Binds `scene` (or `root_comp` if set). Refreshes on hierarchy sig. `WND_Scene` owns click/drop (inspector + gizmo + reparent). External payload drop (`on_item_drop_external`) of a `Type` adds that comp as a child (`AddComp` / `AddChild`).

## PNL_CommonComps (`Editor/Panel/PNL_CommonComps.cs`)

"Comps" tab next to the Outliner (`WND_Scene.tab_outliners`). Inner 3D / 2D tabs are `EdCommonCompsCategory` lists (`root_type` = `Imp3D` / `Imp2D`). Each category collects concrete, non-hidden descendants with `[ImpClass(Common = true)]` (`C2_Tree.Class_IsCommon` — not inherited). Tiles (`EdCommonCompTile`) grab with payload `Type`. Drop on `PNL_SceneView` spawns a ghost at the cursor (switches 2D/3D mode to match) then parents under the pick / scene root. Drop on the outliner parents under that row. Double-click adds under the current selection (or root).

## ImpClass

`[ImpClass(Hidden = true)]` hides the type **and** subclasses from class pickers. `[ImpClass(Common = true)]` lists the type on the Comps palette (per-type, not inherited). Already on the usual spawnables: `C3_Mesh` / `Light` / `Camera` / `Audio` / `PlayerStart` / `Transit`, `C2_Box` / `Button` / `List` / `Image` / `Text`.

## PNL_SceneView (`Editor/Panel/PNL_SceneView.cs`)

Scene viewport tab (`C2_Box`, `cursor_filter = Hit`). Owns `scene`, `undo`, `edit_mode`, `gizmo_data`, `C2_Viewport3D` + `C2_Viewport2D` (both `Pass` so clicks land on the panel), 2D/3D gizmos, camera nav, marquee/selection, asset drop, and the mode/gizmo/space/snap toolbar. `WND_Scene` still hosts the tab box, selection/inspector bind, and dup/delete hotkeys.

Right-click a comp: Duplicate / Delete / Change Type / Add Child. Right-click empty tree: Add Comp. Change Type is disabled on instance roots and packed foreign. Add Child/Comp disabled on instance roots and packed foreign. Dup/Delete disabled on packed foreign and the scene root. Change Type / Add Child / Add Comp open `DLG_ChooseComp` (`Dialog_ClassPicker` of `ImpComp`).

## Dialog_ClassPicker / DLG_ChooseComp

`C1_Dialog` is a pass-through overlay + dimmer shade on `C2_MenuBar.PopupHost()`; the panel is a later sibling so it hit-tests above the shade. While open it hogs input: `C1_Dialog.Host` / `input_hog` make `ImpPlayer.Key_Is*` return false for every comp outside that subtree (`ImpComp.Updating` is the caller). Gizmo/menu hotkeys, camera, etc. go quiet; widgets inside the dialog still type. Escape closes. `Dialog_ClassPicker` hosts `C2_Tree.Tree_Populate_FromClasses` plus a search bar (`Tree_FilterClasses`). Scene tree rows use `C2_Tree.Class_Icon`. `DLG_ChooseComp` roots at `typeof(ImpComp)` and lists every concrete descendant. Skips the Editor assembly. `[ImpClass(Hidden = true)]` on a type hides it **and** every subclass (`C2_Tree.Class_IsHidden` walks bases). Labels strip `C1_` / `C2_` / `C3_` (`Class_DisplayName`). Icons autoload `{engine}/Icons/type/{Name}.png` then `Icons/Types/`, walking bases, then `ICO_COMP*`. Abstract classes stay in the tree as grey `is_disabled` rows (grouping only — click expands, no select/confirm). Confirm via OK or double-click. Shade / Cancel closes.

Reusable dialog panels (`Dialog_ClassPicker`, `Dialog_Confirm`, `DLG_SaveFile`) Detach their box so `EnsureUi` can reuse it. **Hog_Release first, then Detach, then `base.Close()`.** Detaching first orphans the search/name text edit, so `Hog_Release` no longer sees it under the overlay and `input_hog` stays set. Scene view then treats every `Key_Is*` as hogged — no orbit, no click-deselect. `ImpPlayer.Target_IsLive` drops hog/focus that left `ImpScene.current`. `Hog_Release` is `protected` so subclasses can call it.

`TTreeItem` is a struct — never mutate a parent item after inserting it; build children first, then the node.

## Who assigns `ImpScene.current.root`

- `Editor/Program.cs` → `Scene_Editor`
- `Engine/Program.cs` → bare `ImpComp` + demo children

Editor chrome (windows, file browser, popups) lives **inside** `current` as UI comps, so they also get `scene == ImpScene.current`. Edited game scenes are separate `ImpScene` instances shown by `PNL_SceneView` through `C2_Viewport3D` / `C2_Viewport2D`.

## C2_Viewport3D / C2_Viewport2D (`Engine/Comps/2D/`)

Dumb display widgets. Each takes `view_scene` and/or `root` (root wins) plus optional `overlay` (drop ghost, not in the tree). Do **not** name the viewed scene `scene` — that hides `ImpComp.scene` (the editor chrome tree). `transpose_traces` (default true) remaps mouse picks through the widget camera / rect (`Trace_Ray` / `Trace_Pick` / `Trace_World`). Standalone default `cursor_filter = Hit`. Editor sets `Pass` so `PNL_SceneView` receives clicks.

- 3D: R3D into a render texture, blit. Camera is on the widget; editor writes it.
- 2D: canvas fill + `SceneLayout_Set` / `SceneDraw_*` of 2D comps.

No gizmos, selection, or camera-drag on the viewports.

**Asset drop** (on `PNL_SceneView`)
- `overlay` — live ghost drawn after the scene, **not** in the tree until commit.
- Panel `_Notify_OnGrabDrop` + `_Notify_AsCursorTarget(Update)` call ImpAsset SceneDrop_*.
- `C2_Viewport3D.Trace_World` — mesh pick, else Y=0 plane. `C2_Viewport2D.Trace_World` — canvas point.
- Any active grab (`player.grab_is_active`) blocks camera / gizmo / marquee.
- File-browser drop target is `PNL_SceneView` (`ImpScene` or `A_Mesh`).

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

`ImpPlayer.Target_IsLive(comp)` — `comp` is `ImpScene.current.root` or a descendant. Update_Input clears `input_hog` / `target_focus` that fail this (detached dialog widgets).

## C2_Inspector (`Engine/Comps/2D/C2_Inspector.cs`)

Inspects `[ImpVar]` fields/properties on the selected object(s). Categories are `[Category]` or the declaring type name.

- Category expanders (`C2_Expandable`) show the class autoload icon (`C2_Tree.Class_Icon`) after the chevron — same `{engine}/Icons/type` then `Icons/Types` walk as the outliner. `C2_Expandable.icon` / `C2_Button.icon2`.
- Top `C2_SearchBar` (`show_search`, default on) filters by member name, pretty name, category, or a nested field. Matching categories stay expanded. File-browser settings inspector turns search off (`show_search = false`).

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
- `C1_PopupMenu` / `C2_MenuBar.PopupHost()` parent popups under `ImpScene.current.root`.

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
- v1: write the instance root’s ImpVars as today. v2: diff against `packed.root`.
- `_class` kept so a missing packed file can still spawn a placeholder of the right family.

Load: if `instance` path set → `Load` packed → `Instantiate()` → overlay name/vars onto the **root only** → ignore JSON children (v1).

### SceneDrop

Ghost = `packed.Instantiate()` (real root type), parked on `overlay`. Drop parents that root. Undo stays `ImpUndo.Comp_Moved`. Never wrap children in a fresh `ImpComp2D` / `ImpComp3D`.

### Editor lock (v1)

Packaged children (`IsPackedForeign`) are selected and edited like any other comp (inspector + gizmo). Tree identity stays locked:

- No delete, duplicate, reparent, or reorder.
- No parenting into an instance root or a foreign node (`Child_Add` / `Child_Insert` / `Reparent` refuse).
- Outliner grab and inspector Children tree are off for instance roots / foreign.
- Instance root itself is host-owned: move, delete, duplicate the whole capsule. Duplicate is `Clone()` of that capsule (keeps overrides). `Instantiate()` (drop/load) skips children that are instances of the same scene so a self-drop cannot explode the template.

Outliner lists live children (instance roots auto-expand). Instance root **and** packaged kids use a yellow-ish name tint (`236, 196, 82`). Click selects that node — inspect it like any other comp. No child sub-inspectors on the parent.

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
