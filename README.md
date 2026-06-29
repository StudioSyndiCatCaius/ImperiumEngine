# Imperium Engine

> An open-source modern 3D game engine built with C# and [Silk.NET](https://github.com/dotnet/Silk.NET), targeting .NET 10.

**NOTE**: Imperium is currently in a *very early design & architecture phase*. No systems are complete — Claude Code was used for placeholder low-level code (rendering backends, serialization) while the architecture and editor are being built out.

Considerations
* IMGUI for Editor only OR Editor + Game UI
* OGRE for 3D rendering

---

## Architecture

### Node System

The core scene primitive is `ImpComponent`. It serves as both a standalone world object and a composable behavior node — there is no separate Actor/Component distinction. Meshes, lights, UI widgets, gameplay logic, and collision shapes are all `ImpComponent` nodes freely composed in a tree.

```
ImpComponent
├── O3D_Primitive  ("PlayerMesh")
├── O3D_Collider   ("CapsuleCollider")
└── O3D_Primitive  ("CameraArm")
    └── O2D_Window ("HUD")
```

The tree is split by dimension:
- `ImpComponent` — base node (dimension-agnostic)
- `ImpComponent2D` — 2D/UI node: anchor/offset layout, built-in box container, mouse & drag-drop
- `ImpComponent3D` — 3D node with a `TTransform3D`

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

- **`ImpApp`** — app host: window (Silk.NET), RHI, players, three level slots (`Level_Current / Level_Load / Level_Global`)
- **`ImpAsset`** — base for all assets; `Register` / `Load` / `Import` statics + TOML serialization
- **`ImpLevel`** — scene container; holds a list of root `ImpComponent` nodes and drives their lifecycle
- **`ImpGameState`** — tag-gated gameplay state node (`TTagSet` based activation/blocking)
- **`ImpGameMode`** — asset that defines which game states load pre/post/persistently
- **`ImpParse`** — unified JSON/TOML/CSV parser with format auto-sniffing
- **`ImpFile`** — path helpers with `{project}` / `{engine}` token substitution
- **`Assets/`** — all concrete asset types: `A_Material`, `A_Texture`, `A_Mesh`, `A_Sound`, `A_Skeleton`, `A_SkeletalAnim`, `A_Animation`, `A_2DTheme`, `A_Project`, and the material sub-types (`Material_Graph`, `Material_Instance`, `Material_Shader_Prop`)

### Engine

- **`Objects/1D/`** (`O1D_*`) — gameplay logic objects (combatants, movers, game states)
- **`Objects/2D/`** (`O2D_*`) — UI/HUD widgets: Button, Text, TextEdit, List, Tree, Window, TabContainer, GraphEdit, Gizmo, etc.
- **`Objects/3D/`** (`O3D_*`) — world objects: Mesh, Sprite, Skeleton, Collider, Lights (ambient/dir/point/spot), Landscape
- **`Renderers/`** — `RHI_DX11`, `RHI_DX12`, `RHI_OpenGL`, `RHI_Vulkan`
- **`Platforms/`** — `OS_Win10`, `OS_Linux`, `OS_Mac`, Android

### Editor

- **`Levels/`** — `EditorLevel_Projects → EditorLevel_Loading → EditorLevel_Main`
- **`Windows/`** — dockable panels: Level Editor, Entity Editor, Asset Editor, Content Browser, Pulse Editor. All extend `EditorWindow` → `O2D_Window`
- **`Config/`** — editor launch config (`CFG_ED_Launch`)

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
- Rider or Visual Studio 2022+

```bash
git clone https://github.com/your-username/ImperiumEngine.git
cd ImperiumEngine
dotnet build
```

To run the editor:

```bash
dotnet run --project Editor
```

---

## Roadmap

- [x] Core node lifecycle (`OnBegin` / `OnUpdate` / `OnDraw` / `OnEnd`)
- [x] Scene tree (parenting, `Component_*` drivers)
- [x] 2D layout system (anchor/offset, box containers, hit-testing)
- [x] Asset registry (`ImpAsset.Register` / `Load` / `Import`)
- [x] TOML serialization (`ImpSave`, `[ImpVar]`)
- [x] Unified parser (`ImpParse` — JSON/TOML/CSV)
- [x] Input system (via Silk.NET, routed through `ImpPlayer`)
- [x] RHI abstraction base (`ImpRender`) + four backend stubs
- [ ] Working OpenGL render backend
- [ ] Editor UI functional (level/entity/content panels)
- [ ] Material & shader graph
- [ ] 3D transform and scene rendering
- [ ] Physics integration
- [ ] Pulse visual scripting

---

## Contributing

Contributions are welcome. Since the engine is in early development, open an issue to discuss larger changes before submitting a PR.

1. Fork and create a feature branch.
2. Keep PRs focused — one feature or fix per PR.
3. Match code style: nullable enabled, implicit usings, minimal comments.
4. Open a PR against `main`.

---

## License

To be decided. The project is intended to be open-source — check back once a license is added.
