# Imperium Engine

A Free Open-Source 3D game engine written in C# & .NET 10. Inspired by Unreal Engine & Godot.

When deving, use `Projects/Test/` as the working game project template.


## Nuget Packages
* Raylib
* R3D (raylib 3d rendering)
* rlimgui (raylib imgui bindings)
* Jolt Physics


## Projects

### Engine
The base runtime engine. an pakcaged executable can be exported with `.ipk` (Imperium Package) files for game content, OR can run loose game files.

### Editor
The editor App for editing game content.

### Launcher
The standalone launcher App for launching Editor Projects (and maybe games too).

### Crasher
The Crash Reporter App, used with both the Engine & Editor.

## Features

### Cross-Platform
Imperium is designed to be cross-platform:
* Windows 10-11
* Linux
* SteamOS
* MacOS
* Android (Esp. handhelds)

### Mod Support
Imperium uses a modular content system, making it easy to add patches & mods that add new content or override existing content.

## Model Notes:
* Opus 5: cache reasonable path & engine infomation in a dictionary.md as you go, so you can cut down on think time and implement fast & simple.
* Sonnet 5: they to keep things simple & fast, namely just fixing/adding what i tell you and whatever minimum is needed to get that job done. 

## NOTES
* child Comp vars on a comp break the nomral snake case and use `c_compname` with the `c_` acting as the specifier for child/component