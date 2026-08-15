# Imperium Engine

Imperium is an open-source free 3D game engine made in c# .Net 10. Design is somewhat of a streamlined fusion of Godot & Unreal Engine.

As you read probject, update `(Agents/Dictionary.md)` for efficient look up, so that you don't need to keep looking up and rereading the source every time.

Nugets
* Raylib
* r3D
* JoltPhysicsSharp

### ImpAssets

ImpAssets are Imperium's main asset file. typically Json formated. they can hold a relative reference to a source ImpFile for resource types, like textures, sound, models, etc. You can have mutiple assets reference the same ImpFile and its resources.

E.G.
```
SK_Mannequin.glb

Sk_Mannequin_SKEL.ImpAsset (A_Skeleton)
Sk_Mannequin.ImpAsset (A_Mesh)
Sk_Mannequin_Blue.ImpAsset (A_Mesh)
Sk_Mannequin_Red.ImpAsset (A_Mesh)
Sk_Mannequin_Green.ImpAsset (A_Mesh)
Sk_Mannequin_anim_idle.ImpAsset (A_Animation)
Sk_Mannequin_anim_walk.ImpAsset (A_Animation)
Sk_Mannequin_anim_run.ImpAsset (A_Animation)
```



### ImpScenes

ImpScenes is the scene/level type. they are made up of a heirarchy of ImpComps.


Agent Keywords
* [Quick] - try to only modify the selection, do it quickly, no comments unless absolutely needed, and don't go outside unless uterly critical

## Notes
* don't use oneshot functions when it can be avoided. just put code in main function instead of amaking a second oneshot. If you need to repeat a function but ONLY in a function, declare it in that function. or you can't then you can make it its own func.
* Avoid syntactic sugar unless it is really necessary — prefer the standard form. Ternaries (`a ? b : c`), nested `?:`, and similar shortcuts are harder to read than a normal `if`/`else`.
* Always brace `if` / `else` / `for` / `while` / etc. Do not use indent-only single-line bodies.


## Reference Repos:
* Github : `D:\PROJECTS\ImperiumEngine\RefRepos\godot\`