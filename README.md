# HS2 Dynamic Dialogue

A BepInEx 5 plugin for **Honey Select 2 DX**, initially targeting **BetterRepack R16**.

The project aims to make characters react in Character Maker to changes in personality,
clothing, accessories, pose, expression, and camera context.

## First milestone

- Load safely through BepInEx 5.
- Keep dialogue content in editable JSON files.
- Select contextual lines without repeating the last response.
- Avoid modifying AssetBundles or original game files.
- Add HS2 Character Maker hooks after validating the exact local assemblies.

## Repository policy

Game binaries, extracted assets, proprietary assemblies, and BetterRepack files must not be
committed. Local game references belong in `lib/`, which is ignored by Git.

## Build prerequisites

- Visual Studio 2022 or MSBuild
- .NET Framework 4.6
- Honey Select 2 DX with BepInEx 5
- Local references copied to `lib/` as described in `docs/DEVELOPMENT.md`

## Current safety model

- Automatic pose changes use only Honey Select 2's original Character Maker pose list.
- The Maker selects the correct controller, MotionIK data, and female/male category.
- External Gravure, Studio, Action, Dance, Fighting, and Walking animations are not invoked.
- A timid character uses original pose 37 as a covering reaction while in underwear or nude.
- Pose changes wait for a loop boundary when possible, reducing abrupt mid-motion switches.
