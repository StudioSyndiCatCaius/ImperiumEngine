# Dictionary

Living lookup so agents do not re-read source. Update this when you learn or change something.

## Editor (`_Deprecated/Editor/`)

Old ImpComp2D chrome editor. **Deprecated** — parked as solution project `Editor_Deprecated`, excluded from default build. `Editor/` name is free for the new UI-framework rebuild.

## ImpComp (`Engine/ImpComp.cs`)

Scene-graph node. `ImpComp2D` / `ImpComp3D` inherit.

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

**Do not confuse with** `ImpComp2D._scene_root` — static per-frame flag for “this subtree is scene canvas content” (layout/pivot/rotation). That is **not** `ImpComp.scene`.

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

**SceneDrop** — `Instantiate()` of this asset (packed instance, not a flattened copy). Ghost on `view.drop_preview`. Drop parents the instance root under a host-owned dest (`scene.root` if empty). Will not parent inside an instance capsule. Undo via `ImpUndo.Comp_Moved`.

Test asset: `Projects/Test/Content/Scenes/boxes_3.ImpScene` — root `Boxes3` (`ImpComp3D` group pivot) + `Box_A/B/C` (`C3_Mesh` / `GEO_CUBE`) at x = -2, 0, 2.

## ImpAsset (`Engine/ImpAsset.cs`)

JSON-backed asset. Cache keyed by resolved full path. Builtins are `builtin:Type.Member`.

- Load: peek `_class` → `Activator` → `File_Read` → `BindSource`.
- Save: `{ _class, vars }` via `File_JSON`. Only `[ImpVar]` fields.
- `source_file` is an `ImpFile` (png/glb/hdr/…) that many assets can share.

**SceneDrop hooks** (driven by `C2_SceneView`, not ImpPlayer 3D hit-test):

| Hook | When |
|---|---|
| `SceneDrop_Enter/Exit/Update(view, …)` | Cursor enters / leaves / hovers the scene view while this asset is the grab payload. |
| `SceneDrop_CompEnter/Exit(comp, …)` | Hovered **scene-content** comp changes (picked in the viewport, not the 2D widget). |
| `SceneDrop_DropOnComp(comp, …)` | Release. `comp` is the picked scene node, or `view.scene.root` if empty. |

Default impls are empty. `ImpScene` instances the hierarchy. `A_Mesh` spawns a `C3_Mesh`. File-browser payload is a **path string** → `ImpAsset.Load`.

`A_Mesh.GEO_CUBE` / `GEO_PLANE` set `filepath` to `builtin:A_Mesh.GEO_*` so they survive JSON.

## File_JSON (`Engine/Files/File_JSON.cs`)

Reads/writes ImpVar fields only (public+private instance). Public-field fallback for non-asset objects. Does not serialize `ImpComp.scene` / `parent` as fields.

Hierarchy: `Comp_ToJson` / `Comp_FromJson` — used when the asset is an `ImpScene` (`vars.root`). Children are a JSON array, not ImpVar.

## ImpComp.Type_FromName

Resolves a concrete `ImpComp` subclass by short type name (`C3_Mesh`, `ImpComp`, …) across loaded assemblies. Used by scene load.

## Who assigns `ImpScene.current.root`

- `_Deprecated/Editor/Program.cs` → `Scene_Editor` (old editor; project is `Editor_Deprecated`, not in default build)
- `Engine/Program.cs` → bare `ImpComp` + demo children

Editor chrome (windows, file browser, popups) lives **inside** `current` as UI comps, so they also get `scene == ImpScene.current`. Edited game scenes are separate `ImpScene` instances shown by `C2_SceneView.scene`.

## C2_SceneView (`Engine/Comps/2D/C2_SceneView.cs`)

Viewport widget. Its own `scene` field is the **edited** ImpScene (the asset on the tab), not `ImpComp.scene`. Draws `scene.root` as 2D/3D content via `ImpComp2D.SceneLayout_Set`.

**Asset drop**
- `drop_preview` — live ghost (`ImpComp`) drawn after the scene, **not** in the tree until commit.
- `CursorGrab_HoveredAsTarget` / `DroppedOn` / `Cursor_OnHover` call ImpAsset SceneDrop_*.
- `Drop_World3` — mesh pick, else Y=0 plane. `Drop_World2` — canvas point. `Drop_Pick` — hovered scene comp.
- Any active grab (`player.grab_is_active`) blocks camera / gizmo / marquee.
- File-browser ghost treats the view as valid when `Load(path)` is `ImpScene` or `A_Mesh`.

## ImpPlayer grab

`CursorGrab_Payload` from `EdFileThumbnail` / tree rows is a **filesystem path string**. Threshold then `CursorGrab_Begin`. Hover/drop go to `cursor_target` (the 2D widget). Scene-content comps are **not** cursor targets — `C2_SceneView` picks them itself.

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

Ghost = `packed.Instantiate()` (real root type), parked on `drop_preview`. Drop parents that root. Undo stays `ImpUndo.Comp_Moved`. Never wrap children in a fresh `ImpComp2D` / `ImpComp3D`.

### Editor lock (v1)

Packaged children (`IsPackedForeign`) are selected and edited like any other comp (inspector + gizmo). Tree identity stays locked:

- No delete, duplicate, reparent, or reorder.
- No parenting into an instance root or a foreign node (`Child_Add` / `Child_Insert` / `Reparent` refuse).
- Outliner grab and inspector Children tree are off for instance roots / foreign.
- Instance root itself is host-owned: move, delete, duplicate the whole capsule. Duplicate is `Clone()` of that capsule (keeps overrides). `Instantiate()` (drop/load) skips children that are instances of the same scene so a self-drop cannot explode the template.

Outliner lists live children (instance roots auto-expand). Instance root **and** packaged kids use a yellow-ish name tint (`236, 196, 82`). Click selects that node — inspect it like any other comp. No child sub-inspectors on the parent.

Host JSON still skips foreign children (root overrides only). Child var edits are live; persistence is later.

**Not in v1:** host-added nodes under an instance, Make Local, scene inheritance. Inheritance can wait — `root_type` is already the hook.

Gizmo selection outlines and axis lines do **not** occlusion-test against the scene (that was O(samples × meshes) on select and showed up as ~80ms inside `C2_SceneView.OnDraw2D`).

## Layout cache (`ImpComp2D`)

Global epoch + per-instance stamps on `Dimensions_Get` / `Transform_Get` / `Size_Layout` / `Anchor_Offset` / `Scene_IsContent`.

- ImpApp bumps the epoch once per phase (input / update / cursor / draw).
- Setters, Child_*/Detach/Destroy, SceneLayout_Set, SceneDraw_Begin/End also bump.
- **Do not** bump per-comp inside `ImpComp.Update` — that was ~2 × comp_count epochs/frame and made Fill rects jitter (and could recreate the scene-view RT every frame).
- F3 HUD: `epochs` should sit near 4–10, not 1000+. F4 toggles the cache off for A/B.

## C2_SceneView draw cost

`OnDraw2D` owns the whole 3D viewport: `R3D.BeginPro` / `scene.Draw` / `R3D.End` + blit + gizmo. Mesh `OnDraw3D` is only submit time; `R3D.End` is shadows + SSAO + bloom.

F3 HUD `scene view` block splits `rt` / `r3d` / `blit` / `gizmo`. SSAO defaults to 8 samples (32 was a default-quality footgun).

### Reload

`ImpAsset` cache means every instance of `boxes_3` shares one template. When that asset `File_Write`s (or is reloaded), walk the open host scene for `IsInstanceRoot && packed.path == that file`, `Instantiate()` fresh, re-apply the host root overrides (name, transform, visibility).

### Phasing

1. Fields + `Instantiate` + JSON `instance` key + SceneDrop + delete/dup/outliner guards.
2. Open-packed button, reload-on-save, delta vars.
3. Make Local (`packed` cleared, `packed_from = null` on subtree). Then editable children / inheritance.
