# Imperium Engine

> An open-source modern 3D game engine built with C# and [Silk.NET](https://github.com/dotnet/Silk.NET), targeting .NET 10.

**NOTE**: Imperium is currently in a *very early design & architecture phase*. No systems are complete - Claude Code was used for placeholder for the OpenGL graphics rendering while the architecture and editor are being built out manually.

## CORE FREATURES
* Merge of Actor/Component style system into a single object heirarchy system
* Game Engine first, meaning many common features (attributes, abilities, level/entity state saving, menus, async systems) are included out of the box, so especially small teams & solo devs can get up and running quickly. This is to contrast engines sucha s godot which provide minimal foundation to build your own systems on top of.
* Built in 2-part save system:
    * GAME SAVE: saves a particular instance of a game to disk. New one created on game start.
    * GLOBAL SAVE: single file saved to disk and reloaded everytime the game is started again.    

### Examples of Out-Of-The-Box systems
* Menus
    * Confirmation (Text with a YES/NO option)
    * Info (Text with only a NEXT option)
    * Generic (text with a list of multiple scripted options)
    * Tutorial (struct array with a TEXT and IMAGE. By default played once per save)
* Abilities
    * Jump
    * Crouch
    * Sprint
    * SimpleAttack (probabyl old zelda-style, play a single animation with a notfy to check and damage overlapped targets)

### Considerations:
* Merge `Core` & `Engine` into the same project/module
* IMGUI for Editor only OR Editor + Game UI
* OGRE for 3D rendering (WOuld need some c++ implementation. Current Graphics use Claude-Code implementation of Open GL, but this should be removed and written by a proper graphics programmer)
* Posibly rewrite CORE systems in c++ (as minimal as possible) with C# for the whole engine/editor implementation
* Look into some sort of 'parameter welding' system so components can strip undeed/duplicate variables. E.G. if a mesh component always uses its parent's transform, it should not have its OWN transform taking up memory.

---
#### NOTE: The following remainder of the readme was written by Claude Code. Should be rewritten from scratch later when Engine+Editor are in a far more stable place.
---

## Architecture

### Node System

The core scene primitive is `ImpComponent`. It serves as both a standalone world object and a composable behavior node - there is no separate Actor/Component distinction. Meshes, lights, UI widgets, gameplay logic, and collision shapes are all `ImpComponent` nodes freely composed in a tree.

```
ImpComponent
├── O3D_Primitive  ("PlayerMesh")
├── O3D_Collider   ("CapsuleCollider")
└── O3D_Primitive  ("CameraArm")
    └── O2D_Window ("HUD")
```

The tree is split by dimension:
- `ImpComponent` - base node (dimension-agnostic)
- `ImpComponent2D` - 2D/UI node: anchor/offset layout, built-in box container, mouse & drag-drop
- `ImpComponent3D` - 3D node with a `TTransform3D`

Components can be bundled together in an `Entity`, a form a prefab with a script that can be reused. (Levels are just a special subclass of Entity)


### Solution Layout

Three-project layered solution. Each project may only reference those above it:

```
Core  ◄──  Engine  ◄──  Editor
```

| Project | Namespace | Role |
|---|---|---|
| `Core/` | `ImperiumCore` | Engine-agnostic foundation, all asset types, RHI base |
| `Engine/` | `ImperiumEngine` | Concrete nodes, RHI backends, platform layers |
| `Editor/` | `ImperiumEditor` | Editor application (exe) |

### Core

- **`ImpApp`** - app host: window (Silk.NET), RHI, players, three level slots (`Level_Current / Level_Load / Level_Global`)
- **`ImpAsset`** - base for all assets; `Register` / `Load` / `Import` statics + TOML serialization
- **`ImpLevel`** - scene container; holds a list of root `ImpComponent` nodes and drives their lifecycle
- **`ImpGameState`** - tag-gated gameplay state node (`TTagSet` based activation/blocking)
- **`ImpGameMode`** - asset that defines which game states load pre/post/persistently
- **`ImpParse`** - unified JSON/TOML/CSV parser with format auto-sniffing
- **`ImpFile`** - path helpers with `{project}` / `{engine}` token substitution
- **`Assets/`** - all concrete asset types: `A_Material`, `A_Texture`, `A_Mesh`, `A_Sound`, `A_Skeleton`, `A_SkeletalAnim`, `A_Animation`, `A_2DTheme`, `A_Project`, and the material sub-types (`Material_Graph`, `Material_Instance`, `Material_Shader_Prop`)

### Engine

- **`Objects/1D/`** (`O1D_*`) - gameplay logic objects (combatants, movers, game states)
- **`Objects/2D/`** (`O2D_*`) - UI/HUD widgets: Button, Text, TextEdit, List, Tree, Window, TabContainer, GraphEdit, Gizmo, etc.
- **`Objects/3D/`** (`O3D_*`) - world objects: Mesh, Sprite, Skeleton, Collider, Lights (ambient/dir/point/spot), Landscape
- **`Renderers/`** - `RHI_DX11`, `RHI_DX12`, `RHI_OpenGL`, `RHI_Vulkan`
- **`Platforms/`** - `OS_Win10`, `OS_Linux`, `OS_Mac`, Android

### Editor

- **`Levels/`** - `EditorLevel_Projects → EditorLevel_Loading → EditorLevel_Main`
- **`Windows/`** - dockable panels: Level Editor, Entity Editor, Asset Editor, Content Browser, Pulse Editor. All extend `EditorWindow` → `O2D_Window`
- **`Config/`** - editor launch config (`CFG_ED_Launch`)

---

## Tech Stack

| Technology | Purpose |
|---|---|
| **C# / .NET 10** | Primary language and runtime |
| **Silk.NET** | Cross-platform windowing, input, OpenGL/Vulkan/DirectX bindings |
| **Tomlyn** | TOML serialization (asset save/load) |

---

## Building

**Prerequisites**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
