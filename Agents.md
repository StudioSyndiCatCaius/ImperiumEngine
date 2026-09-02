# Imperium Engine
C# .NET 10. Godot×UE fusion. Nugets: Raylib, r3D, JoltPhysicsSharp.

### ImpAssets
JSON-ish wrappers. Many assets can share one ImpFile.

```
SK_Mannequin.glb
  _SKEL (A_Skeleton)
  . / _Blue / _Red / _Green (A_Mesh)
  _anim_idle / _walk / _run (A_Animation)
```

ImpScene = ImpComp tree.

### Keywords
`[Quick]` — selection only, no comments, no scope creep.

### Rules
- No oneshot helpers. Inline, or a local func if repeated only there.
- Editor work stays in `Editor/`. Don't change Engine unless asked. Editor UI is ImGui (`C2_*` is game-only).

### Refs
`D:\PROJECTS\ImperiumEngine\RefRepos\godot\`
