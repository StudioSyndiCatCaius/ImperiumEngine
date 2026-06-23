# Silkworm Engine

> An open-source 3D game engine built with C# and [Silk.NET](https://github.com/dotnet/Silk.NET), targeting .NET 10.

---

## Overview

Silkworm Engine is a from-scratch 3D game engine designed around a hybrid architectural philosophy:

- **Unreal Engine** for the big picture — a structured engine loop, a centralized `SilkApp` host, a dedicated rendering hardware interface (RHI) layer, and clear separation between engine systems (physics, audio, rendering, input).
- **Godot** for how scene objects work — a unified node-based hierarchy where there is no hard distinction between actors and components. Every scene object is a `SilkObject` node that can parent other nodes, own logic, and be composed freely. You don't bolt components onto actors; you just nest nodes.

The result is an engine that is organized and scalable at the systems level (Unreal DNA) while remaining simple and flexible at the scene-authoring level (Godot DNA).

---

## Architecture

### Node System

The core scene primitive is `SilkObject`. It serves both as an "actor" (a standalone entity in the world) and as a "component" (a child that provides behavior to its parent) — there is no separate class for each role. A mesh, a camera, a light, a collision shape, or a custom gameplay object are all just `SilkObject` nodes that can be freely parented and composed.

```
SilkObject (root / world)
├── SilkObject ("PlayerCharacter")
│   ├── SilkObject ("SkeletalMesh")
│   ├── SilkObject ("CapsuleCollider")
│   └── SilkObject ("CameraArm")
│       └── SilkObject ("Camera")
└── SilkObject ("PointLight")
```

### Engine Systems

| System | Class | Role |
|---|---|---|
| Application host | `SilkApp` | Window creation, main loop, system orchestration |
| Rendering | `SilkDraw` | Unified 2D/3D draw submission, backed by the RHI |
| RHI | `Commons/RHI/` | Low-level GPU abstraction over Silk.NET |
| Math | `SilkMath` | Static math utilities (vectors, matrices, transforms) |
| Attributes | `SilkAttributes` | Custom C# attributes for editor metadata, serialization hints, etc. |

### Solution Layout

```
SilkwormEngine.sln
├── SilkwormEngine/          # Engine runtime library
│   ├── Core/
│   │   ├── SilkApp.cs       # Application/window host
│   │   ├── SilkObject.cs    # Base node class
│   │   ├── SilkDraw.cs      # Rendering interface
│   │   └── SilkAttributes.cs
│   ├── Commons/
│   │   ├── 1D/              # Curves, timelines, 1D data types
│   │   ├── 3D/              # Meshes, transforms, scene utilities
│   │   ├── Assets/          # Asset loading and management
│   │   └── RHI/             # Rendering Hardware Interface
│   ├── Libraries/
│   │   └── SilkMath.cs
│   ├── Enums/
│   └── Structs/
└── SilkwormEditor/          # Editor application (hosts SilkApp)
```

---

## Tech Stack

| Technology | Purpose |
|---|---|
| **C# / .NET 10** | Primary language and runtime |
| **Silk.NET** | Cross-platform windowing, OpenGL/Vulkan/DirectX bindings, input |

---

## Building

**Prerequisites**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A C# IDE — Rider or Visual Studio 2022+ recommended

**Steps**

```bash
git clone https://github.com/your-username/SilkwormEngine.git
cd SilkwormEngine
dotnet build
```

To run the editor:

```bash
dotnet run --project SilkwormEditor
```

---

## Roadmap

The engine is in early development. Planned milestones, roughly in order:

- [ ] Core node lifecycle (`OnReady`, `OnUpdate`, `OnDestroy`)
- [ ] Scene tree traversal and parenting
- [ ] RHI abstraction layer (OpenGL backend first)
- [ ] Basic 3D rendering pipeline (forward renderer)
- [ ] Transform system (local/world space)
- [ ] Input system via Silk.NET
- [ ] Asset loading (meshes, textures)
- [ ] Physics integration
- [ ] Audio system
- [ ] Editor UI (SilkwormEditor)
- [ ] Scripting / hot-reload support

---

## Contributing

Contributions are welcome. Since the engine is in early development, it's worth opening an issue to discuss larger changes before submitting a PR, so work doesn't conflict.

1. Fork the repo and create a feature branch.
2. Keep PRs focused — one feature or fix per PR.
3. Match the existing code style (nullable enabled, implicit usings, no unnecessary comments).
4. Open a PR against `main`.

---

## License

To be decided. The project is intended to be open-source — check back once a license is added.
