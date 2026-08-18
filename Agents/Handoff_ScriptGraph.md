# Handoff — Pulse script graph (take over development)

Rewritten 2026-08-15. The previous version of this file described an authoring-only editor with
no compiler or VM — that is no longer true. A compiler, an interpreter, PIE hooks, input routing,
and per-player input focus have all been built and headlessly verified since. Read this file fresh;
do not trust anything you remember about "Pulse has no runtime yet."

Read first: `Agents.md`, this file, then `Agents/Dictionary.md` sections **PNL_ScriptGraph**,
**C2_GraphEdit / C2_GraphNode**, **A_Script / Pulse**, **ScriptNode / SN_\***, **Pulse runtime**,
**ImpGame / Play-in-Editor** (especially **Input target game**), **ImpPlayer notify**. Update the
Dictionary when you change behavior — it is the lookup cache so the next agent doesn't re-read
everything.

---

## Prompt (paste this)

Continue developing Imperium Engine's Pulse script graph. Project: C# .NET 10, Godot + Unreal
fusion. Rules in `Agents.md`:

- No oneshot helpers — put code in the main function; local repeats stay inside that function.
- No ternaries or other sugar. Always brace `if` / `else` / `for` / `while`.
- `[Quick]` = selection only, no comments, do not leave the selection unless critical.
- **`C2_Graph` is generic.** Pulse / A_Script / ScriptNode types do **not** belong in
  `Engine/Comps/2D/C2_Graph.cs`. Other graphs (anim, material) will reuse it.
- Update `Agents/Dictionary.md` when you learn or change something.

Work in small steps. Keep the editor usable after each step. Ask before changing public
`[PulseCall]` signatures or the `TPulseNode` / `TGraphConnection` persistence shape.

**Verification discipline that matters here:** the Editor locks `Engine.dll` while it runs, so you
usually cannot launch it to eyeball a change. Build to a scratch path to compile-check
(`dotnet build ImperiumEngine.sln -p:BaseOutputPath=d:/tmp/impbuild/` — only redirect the output
path, not `BaseIntermediateOutputPath`, or you drown in bogus CS0579 duplicate-attribute errors
from un-excluding the repo's own `obj/`). To actually **run** logic, a disposable console project
referencing `Engine/Engine.csproj` (create it under `D:\tmp`, not in the repo) that constructs an
`A_Script` by hand, calls `Compile()`, and drives `ScriptVM` / `ImpScene.Update` directly is how the
compiler and VM in this handoff were verified — there is no GUI required to test node logic,
compile errors, or lifecycle hooks. Only click-through (pan/zoom/wire dragging/pin widgets, and the
click-in/click-out input focus behavior) genuinely needs the running Editor and a human.

---

## Product intent

Pulse is how designers hook **events** (`[PulseOverride]`) and **calls** (`[PulseCall]`) on a
script's `parent_type`, plus get/set of scriptable `[ImpVar]`s, in a node graph.

- Looks and feels like UE Blueprints (exec pins, target pin, pure vs exec, get/set vars).
- Canvas widget looks and feels like Godot GraphEdit (pan / zoom / box select / rewire).
- Scripts live on **ImpScene** and **ImpComp**: always an embedded `script_builtin`, optional
  on-disk `script_override`. `Script_Get()` prefers the override.
- **A scene script is authored against the scene's root comp type, not `ImpScene`.** ImpAssets
  are not scriptable — the script treats the scene as if it were a subclass of `root_type`. See
  "Scene scripting context" below; this is the single most important semantic to internalize
  before touching anything here.
- Scene window **Script** tab edits the **open scene's** script, not the selected component.
- File ext for standalone scripts: `ImpScript`.

---

## Architecture

```
PNL_SceneView.tabs_view
  [0] Scene   — viewports / gizmos
  [1] Script  — PNL_ScriptGraph.Bind(scene) every OnUpdate

PNL_ScriptGraph          editor host (Pulse-aware)
  left: override/call/var list + Compile button
  center: C2_GraphEdit   generic canvas
  right: C2_Inspector    A_Script defaults (parent_type, vars)

C2_GraphNode.user_data = TPulseNode     live widget <-> serialized node
C2_GraphEdit.connections = TGraphLink   live refs
A_Script.connections = TGraphConnection Guid + pin bytes

A_Script.Compile()  ->  TScriptProgram  (List<ScriptNode> + links + errors)
ScriptVM(program, self)  ->  walks it at runtime
```

| Layer | File | Owns |
|---|---|---|
| Generic canvas | `Engine/Comps/2D/C2_Graph.cs` | `C2_GraphEdit`, `C2_GraphNode`, `TGraphSlot`, `TGraphLink`, `UI_Graph`. **No Pulse types.** |
| Persist wires | `Engine/ImpGraph.cs` | `TGraphConnection` (Guid + pins). `ImpGraph` / `ImpGraphNode` are the (mostly empty) runtime base. |
| Pulse data + reflection | `Engine/Assets/A_Script.cs` | `A_Script`, `TPulseNode`, `TPulsePinValue`, `TPulseFunc` / `TPulseVar`, `Pulse.*`, `EScriptNodeType`, `Compile()` |
| Node behavior (authoring + runtime) | `Engine/Script/ScriptNode.cs`, `Engine/Script/Node/SN_*.cs` | `ScriptNode` base, `ScriptNodes` registry/menu/factory, per-kind classes |
| Interpreter | `Engine/Script/ScriptVM.cs` | `TScriptProgram`, `ScriptVM`, `TScriptLatent` |
| Editor host | `Editor/Panel/PNL_ScriptGraph.cs` | Bind scene script, spawn/build nodes, RMB menu, Compile button, persist |
| Scene tab | `Editor/Panel/PNL_SceneView.cs` | Inner Scene/Script tabs; `script_graph.Bind(scene)` |
| Attributes | `Engine/Attributes.cs` | `[PulseCall]`, `[PulseOverride]` |
| Scene / comp scripts + lifecycle | `Engine/ImpScene.cs`, `Engine/ImpComp.cs` | `script_builtin` (Hidden) + `script_override`, `Script_Get()`, `RBegin`/`Update`/`REnd`, `Script_Event` |
| Input routing + focus | `Engine/ImpPlayer.cs`, `Engine/ImpComp.cs`, `Engine/ImpGame.cs`, `Engine/Comps/2D/C2_GameView.cs` | `target_game`, per-game input delivery gate, click-in/out |

### Persistence model (do not change without asking)

Persist:

- `TPulseNode` — id, kind, **node_class**, member, target_type, is_set, position, pin_values
- `TGraphConnection` — from_node / from_pin / to_node / to_pin

`TPulseNode.node_class` names the `SN_*` class (e.g. `"SN_Func"`). Nodes saved before this field
existed have it empty; `ScriptNodes.Create` falls back to `kind` (`VoidOverride` -> `SN_Event`,
`Var` -> `SN_VarGet`/`SN_VarSet` by `is_set`, else `SN_Func`). Do not remove that fallback — old
scenes still round-trip through it.

Live editor rebuilds widgets from `TPulseNode`/`TGraphConnection`. `A_Script.Clone()` deep-copies
nodes (needed so PIE does not share the editor graph) and resets `compiled`/`compile_dirty` so the
copy compiles its own program.

`MarkDirty()` dirties the script if it has a filepath, **always** dirties the scene (builtin is
embedded in the scene JSON), and now also sets `compile_dirty = true`.

### One node family, two roles — read this before adding a node kind

There used to be a documented split between "authoring" `TPulseNode` kinds and "runtime" `SN_*`
stubs that were never wired together. **That split is gone.** Today:

```
ImpGraphNode                    Engine/ImpGraph.cs        guid, virtual OnEnter/OnExit/OnUpdate (legacy, unused by the VM)
  ScriptNode                    Engine/Script/ScriptNode.cs
    is_available, context, member, script, data, slots
    Bind()            fills [ImpVar] fields from pin_values
    GetNode_Title/Color/Chrome() + Slots_Build()   <- what the editor draws
    Compile_Check()   <- validation / type rebind, runs once per compile
    Exec_Run() / Value_Get()   <- what the VM calls at runtime

    SN_Event   Engine/Script/Node/SN_Event.cs   one per [PulseOverride] member.  is_available=false
    SN_Func    Engine/Script/Node/SN_Func.cs    one per [PulseCall] member.      is_available=false
    SN_Var (abstract)  Engine/Script/Node/SN_Var.cs
      SN_VarGet / SN_VarSet                     one per scriptable [ImpVar] or script-local var.  is_available=false
    SN_If      Engine/Script/Node/SN_If.cs       is_available=true  (drop anywhere)
    ScriptNodeAsync (abstract)
      SN_Delay Engine/Script/Node/SN_Delay.cs    is_available=true  (drop anywhere)
```

A single `ScriptNode` subclass now owns **both** what the node looks like in the editor
(`Slots_Build`) **and** what it does when the graph runs (`Exec_Run`/`Value_Get`). When you add a
node kind you write **one class**, not a widget-layout function plus a separate stub. See
"Adding a node kind" below.

`ScriptNodes` (static, `Engine/Script/ScriptNode.cs`) reflects the assembly once for concrete
`ScriptNode` subclasses:
- `ScriptNodes.Create(TPulseNode, ctx, script)` -> a bound instance (used by both the editor's
  `BuildWidget` and `A_Script.Compile()`).
- `ScriptNodes.Menu(ctx, self_ctx, script)` -> what the RMB / drop-wire popup offers, grouped by
  category (Add Override / Call Function / Variables / Flow).
- Abstract bases (`ScriptNodeAsync`, `SN_Var`) are skipped, so they never appear as addable nodes.

---

## What already works

### Editor chrome

- Scene window inner tabs: **Scene** | **Game** | **Script**. Play (`MOpt_Play`) binds PIE into Game and selects that tab.
- Script panel: override/call/var tree (left, above it a **Compile** button + result label) +
  graph + `A_Script` inspector (parent_type, `vars`).
- Source label reads e.g. `Builtin (ImpComp)` / `Override: Foo (ImpComp)` — shows the resolved
  scripting context, not just builtin-vs-override.
- Bind is the **scene** script. Comp scripts exist on `ImpComp` (`script_builtin`/`script_override`)
  but have no editor tab and are **never compiled or run** — see "What is not built".

### Graph widget (generic, in `C2_Graph.cs` — unchanged, still Pulse-agnostic)

- Pan MMB/Space+LMB, zoom to cursor, multi-drag with Ctrl-inverted snap, Shift-additive box select,
  drag output->input to connect (one wire per input, new connect replaces; outputs fan out), drag a
  wired input to rewire, RMB a wire to delete it, Delete/Backspace on selection, Ctrl+A.
- `CanConnect`: same type id, or `to.data_left.IsAssignableFrom(from.data_right)`.
- Node chrome is 9-slice from `{engine}/Textures/Graph/`; pins/wires are still drawn in code.
- Unconnected value-typed left pins host an inspector-style default-value widget
  (`C2_TextEdit`/slider/checkbox/dropdown/vector/color), laid out via `SlotEdits_Layout` so it sits
  on the pin row and does not hog input. This works today — the alignment/hog bug from the previous
  handoff was fixed and confirmed in a running editor.

### Pulse authoring

- `parent_type` decides which overrides/calls/vars appear. Determined for a scene script by
  `ImpScene.RootType_Get()` (see below), settable directly for standalone `A_Script` assets.
- Left tree: overrides (green = already placed; click adds or focuses).
- RMB empty canvas / drop a wire on empty: `ScriptNodes.Menu` builds Add Override / Call Function /
  Variables / **Flow** (`SN_If`, `SN_Delay` — anything `is_available`). Dropping a wire on an
  **object**-typed pin narrows context to that type and auto-wires Target.
- Exec pin type id is 0. Void `[PulseCall]`/all overrides are exec nodes; non-void calls are pure
  (no exec, a Return pin, evaluated on pull not on tick).
- **`Pulse.Vars` is strict**: `[ImpVar]` with `Edit = EImpVarEdit.None` (the default — every
  pre-existing `[ImpVar]` in the repo) is **not** scriptable and will not appear in the Variables
  menu. You must add `Edit = EImpVarEdit.ReadOnly` or `.ReadWrite` to a field/property to expose it.
  Same rule for `TScriptVar.edit` on script-local vars.

### Compiler + runtime (new this cycle — the headline change)

`A_Script.Compile()` -> `TScriptProgram`: one `ScriptNode` per `TPulseNode`, wires copied from
`connections`, `errors` list. Order matters: `Compile_Check()` runs on every node **before**
`Slots_Build()`, because a check can rebind a node to a different type (see "stale target_type"
below), which changes what its pins should be.

`compiled`/`compile_dirty` are plain (unserialized) fields. `Program_Get()` compiles on demand;
`MarkDirty()` (called by every graph edit in the panel) sets `compile_dirty`. A script loaded fresh
off disk is always dirty, so it compiles itself the first time it's needed — no explicit compile
step is required to run, but there's a **Compile** button in the panel for manual verification
(reports node count or per-node errors to a label + Console).

`ScriptVM(program, self)` is one running instance:
- `Event_Run(member, args)` finds the matching `SN_Event`, stashes `args`, walks exec from slot 0.
- `Exec_Follow` walks the chain; each node's `Exec_Run` returns which exec output to continue from,
  or `ScriptVM.EXEC_STOP` / `EXEC_LATENT`. A 4096-step limit kills runaway loops instead of hanging.
- `Input_Get` pulls a value through a wire (calling `Value_Get` on the source — pure nodes
  evaluate on demand, UE-style, not on a clock) or falls back to the pin's saved default.
- `SN_Delay` uses `Latent_Schedule` + a per-VM timer list ticked by `ScriptVM.Update(dt)` — no
  threads.
- Script-local vars live in the VM's locals dictionary, keyed by name.

**Lifecycle hooks are live:**
- `ImpScene.RBegin` compiles (if dirty) and creates `script_vm` with `self = root`, assigns
  `root.script_vm = script_vm`, fires `OnBegin`.
- `ImpScene.Update` ticks latents then fires `OnUpdate(dt)` — every frame, while `is_running`.
- `ImpScene.REnd` fires `OnEnd`, clears both `script_vm` fields and `root.input_owner`.
- `ImpComp.Update_Input` now calls `Script_Event(member, args)` alongside each `Input_Pressed` /
  `Input_Released` / `Input_Update` C# virtual, with matching arg order (an `SN_Event`'s output
  pins are the override's parameters in order — pin 0 is exec, pin 1+ are args). Only the scene
  **root** comp has `script_vm` set, so only input delivered to the root reaches the graph.

**Stale `target_type` self-healing:** scene scripts used to be parented to `ImpScene`; they're now
parented to the root comp type (see next section). Nodes saved under the old scheme still carry
`target_type: "ImpScene"`. `Compile_Check` on `SN_Func`/`SN_Var` rebinds such a node to the script's
own `ParentType_Get()` **unless its Target pin is wired** (which means the author meant that other
type on purpose). This was verified against the exact stale scene in the test project — it compiles
with 0 errors and runs correctly without you having to touch old data.

**Input focus / per-game routing (new this cycle, adjacent but load-bearing):** input used to be
one global stream shared by editor and game. Now `ImpPlayer.target_game` says which `ImpGame`
(host/editor vs the PIE session) a player's actions go to; `ImpComp.Update` only calls
`Update_Input` when `game_owner` (or host, if null) matches `input_owner.TargetGame_Get()`.
`C2_GameView` claims the target on Play-start and on click-in, releases on click-out and on Stop.
Full detail, including the ordering/latency notes and the three-layer reset (Play_Stop / Unbind /
per-frame backstop), is in the Dictionary's **ImpGame / Play-in-Editor -> Input target game**
subsection — read it before touching input again, there are two non-obvious traps documented there
(`TargetGame_Get()` must fall back to `Get(ID_HOST)`, never `ImpGame.current`, or the whole feature
silently no-ops; and `WND_Scene`'s Delete/Ctrl+D hotkeys had to be gated so a game bound to Delete
can't destroy the authored scene mid-play).

### Scene scripting context — read before touching `ImpScene`/`ImpComp` scripting

`ImpScene.RootType_Get()` returns `root_type.Get()` (falling back to `ImpComp`).
`ImpScene.Script_Get()` re-syncs `script_builtin.parent_type` to that **every call** — so the scene
script always sees the root comp's `[PulseCall]`s/`[PulseOverride]`s/scriptable vars as if the scene
itself were a subclass of the root comp. `Self` / an unwired Target pin means the root comp, not the
`ImpScene` asset. `ImpAsset`s are not scriptable at all. Editing `parent_type` directly on a scene
builtin in the inspector will snap back next call — it's derived, not authored. If you need a
scene script to genuinely target something else, that's a design conversation with the user first.

### PIE

`A_Script.Clone()` + `ImpScene.PlayCopy()` mean the play scene compiles and runs its own program;
the editor's script/graph is never mutated by play. Verified: `Play_Start` -> `RBegin` -> `OnBegin`
fires, `Update` fires `OnUpdate` every frame, `Play_Stop` -> `REnd` -> `OnEnd` fires and the VM is
dropped, with no leaked state on a second Play.

---

## Adding a node kind

1. New file `Engine/Script/Node/SN_Whatever.cs`, subclass `ScriptNode` (or `ScriptNodeAsync` if it
   needs to hold exec open across frames).
2. Set `is_available = true` in the ctor if it should be droppable anywhere from the Flow menu;
   leave `false` if it should only ever be instanced per-member the way `SN_Func`/`SN_Event`/`SN_Var`
   are (then it needs its own entry added to `ScriptNodes.Menu`, not the automatic Flow scan).
3. Override `GetNode_Title()` / `GetNode_Color()` / `GetNode_Chrome()` (optional) and
   `Slots_Build(List<TScriptSlot>)` — describe pin rows top to bottom, `enable_left`/`enable_right`
   with `type_left`/`type_right` (null type = exec) and `name_left`/`name_right`.
4. Override `Exec_Run(ScriptVM vm)` (return the exec output slot to continue from, or
   `ScriptVM.EXEC_STOP`/`EXEC_LATENT`) and/or `Value_Get(ScriptVM vm, int slot)` for pure output.
   Use `vm.Input_Get(this, slot)` to read an input pin, `vm.Target_Get(this, slot)` for a Target pin
   (falls back to `vm.self` when unwired).
5. If it needs validation or can rebind to another type at compile time (see `SN_Func`/`SN_Var` for
   the stale-target_type pattern), override `Compile_Check(TScriptProgram, List<string> errors)`.
6. Nothing else. `ScriptNodes`'s reflection scan and `A_Script.Compile()` pick it up automatically —
   there is no registry to hand-edit, no `EScriptNodeType` enum entry required (that enum is now
   only used as a legacy fallback for `node_class`-less saved nodes; do not extend it for new kinds,
   set `node_class` instead).

Do not touch `C2_Graph.cs` for this. Do not add Pulse-specific fields to `C2_GraphNode`.

---

## What is not built

1. **Comp script execution.** `ImpComp.script_builtin`/`script_override` exist and Compile() would
   work on them, but nothing ever calls `RBegin`/`REnd`-equivalents for a non-root comp's own
   script, and there's no editor tab to author one. Only the **scene root's** script runs, driven
   by `ImpScene`. This is the natural next slice if the user wants per-comp behavior scripts.
2. **Custom functions.** `EScriptNodeType.FuncInOut` is unused; you cannot author a new callable
   function inside a graph.
3. **Node palette** beyond RMB + left tree (no drag-from-list).
4. **Standalone `A_Script` asset window** — no `WND_Asset` Pulse editor binding for a bare
   `.ImpScript` file opened outside a scene.
5. **Pin / wire textures** — still circles + bezier in code.
6. **Minimap, auto-arrange, comment boxes, reroute pins.**
7. **Undo** for graph edits — `MarkDirty()` only; no `ImpUndo` integration.
8. **Type pins for generics / object pickers on value inputs**; no asset-slot-on-pin.
9. **Pure-node result caching** — a pure node wired to multiple consumers re-evaluates once per
   consumer per pull, not once per frame. Fine for cheap getters, a real cost for an expensive pure
   call fanned out widely. Worth a per-VM per-frame cache if it becomes a problem.
10. **PIE scene comps are not cursor targets** — a game HUD button or 3D click target will not
    respond to mouse clicks; `Update_Cursor` only traces the editor root. Input **actions** (the
    thing `Input_Pressed` etc. deliver) work; direct point-and-click on in-game widgets does not.
11. Physics isolation between editor and PIE — unrelated to Pulse, mentioned in case it comes up.

---

## Known issues / landmines

- **Live Editor locks DLLs.** Close it (or compile-check with
  `-p:BaseOutputPath=d:/tmp/impbuild/`) before `dotnet build`. Restart to see changes.
- **`input_hog` bypasses the input-target gate.** `ImpPlayer.Update_Input` calls
  `input_hog.Update_Input` directly, before the per-game check. Every current hog is an editor
  widget, so this is harmless today — but if a PIE comp ever takes the hog, it would keep receiving
  input after click-out. Noted in code, not fixed.
- **A comp's `input_owner` is only ever set by `Input_SetOwnerActive` (a `[PulseCall]`)** — nothing
  else assigns it. If input "isn't working," check the graph actually calls this on `OnBegin` (or
  wherever) before assuming the VM or routing is broken.
- **`TTreeItem` is a struct** — build children first, then the parent item. Never mutate a parent
  after insert.
- **No ternaries / always braces** — match existing Pulse code style throughout this subsystem.
- Pre-existing nullable / hide warnings are fine.
- Click-in/click-out input focus and the pin-default widgets are the two areas that genuinely need
  a human in a running Editor to verify — everything else in this handoff was verified headlessly
  (see the Prompt section for how).

---

## Recommended next steps

Roughly in order of value, but ask the user — do not assume:

1. **Comp script execution** (item 1 above) — the natural continuation of "the VM runs," since
   right now only the scene root benefits.
2. **Custom functions** (`FuncInOut`) if the user wants graph-authored reusable functions, not just
   calls into C#.
3. Pure-node caching if a real graph shows the cost (item 9) — do not preemptively optimize.
4. Everything else in "What is not built" is smaller/cosmetic (palette, pin textures, undo,
   minimap) — good filler work, not architecturally interesting.

---

## Key files

| Path | Why |
|---|---|
| `Engine/Script/ScriptNode.cs` | `ScriptNode` base, `TScriptSlot`, `ScriptNodes` registry/menu/factory |
| `Engine/Script/Node/*.cs` | One class per node kind — shape *and* behavior |
| `Engine/Script/ScriptVM.cs` | `TScriptProgram`, `ScriptVM` interpreter, latent scheduling |
| `Engine/Assets/A_Script.cs` | Asset, `TPulseNode`, `Pulse.*` reflection, `Compile()` |
| `Editor/Panel/PNL_ScriptGraph.cs` | Editor host: menu, `BuildWidget`, Compile button, persist |
| `Editor/Panel/PNL_SceneView.cs` | Scene / Game / Script tabs + Bind |
| `Editor/Panel/PNL_GameView.cs` | PIE host tab; owns `C2_GameView` |
| `Engine/Comps/2D/C2_Graph.cs` | Generic graph widget + pin default widgets. **No Pulse types.** |
| `Engine/Attributes.cs` | `[PulseCall]` / `[PulseOverride]` / `[ImpVar(Edit=...)]` |
| `Engine/ImpScene.cs` | `script_builtin`/`script_override`, `RootType_Get`, `RBegin`/`Update`/`REnd` |
| `Engine/ImpComp.cs` | `Script_Event`, `Update_Input`, `Input_SetOwnerActive`, input-target gate |
| `Engine/ImpPlayer.cs` | `target_game`, `TargetGame_Get/Set/IsHost` |
| `Engine/Comps/2D/C2_GameView.cs` | Click-in/click-out input focus claim/release |
| `Engine/ImpGame.cs` | `Play_Start`/`Play_Stop`, input-target reset on stop |

### Graph input cheatsheet (unchanged)

| Action | Binding |
|---|---|
| Pan | MMB, Space+LMB |
| Zoom | Wheel |
| Context menu (Pulse) | RMB empty (`on_context_empty`) |
| Delete wire | RMB on wire |
| Connect | LMB drag output -> input |
| Rewire | LMB drag wired input |
| Box select | LMB empty drag |
| Delete nodes | Delete / Backspace (skipped if pin edit busy) |

---

## Out of scope unless asked

- Rewriting the generic graph widget into a script-specific one.
- Physics isolation, split-screen / per-game `ImpPlayer` lists.
- Material / anim graphs (they will reuse `C2_Graph` later; do not Pulse-specialize it now).
- Changing `Print(string)` or other existing `[PulseCall]` signatures without asking.
- AssemblyLoadContext or emitting/loading assemblies for compilation — the VM is a tree-walking
  interpreter on purpose, keep it that way unless the user explicitly wants to change strategy.

---

## First session checklist

1. Read the Key Files table, skim `Engine/Script/ScriptNode.cs` and one `SN_*` file end to end so
   the "one class = shape + behavior" pattern is concrete before you add anything.
2. Read the Dictionary's **Input target game** subsection — it documents two non-obvious traps
   (the `Get(ID_HOST)` vs `ImpGame.current` fallback, and the Delete/Ctrl+D gate) that are easy to
   silently regress if you touch input again.
3. Set up a scratch console project under `D:\tmp` referencing `Engine/Engine.csproj` if you need to
   verify compiler/VM behavior — you do not need the Editor running for that.
4. Ask the user which of "Recommended next steps" they want, or follow their explicit ask.
5. Keep Pulse types out of `C2_Graph.cs`. Persist via `TPulseNode` + `TGraphConnection` only.
6. Update `Agents/Dictionary.md` when you land something new.
