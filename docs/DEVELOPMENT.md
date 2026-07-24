# Development setup

## Supported target

The initial target is Honey Select 2 DX from BetterRepack R16. HS2 uses the BepInEx 5
plugin model; Room Girl IL2CPP code must not be copied into this project.

## Local references

Create a local `lib/` directory and copy only the assemblies needed for compilation:

- `BepInEx.dll`
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`

The directory is ignored by Git and must never be committed. Additional HS2 or HS2API
references will be documented after their exact names and versions are confirmed from the
target installation.

## Milestone sequence

1. Verify that the plugin loads and logs its version.
2. Confirm the exact Character Maker scene and HS2API events. (Verified with HS2API 1.41.)
3. Observe clothing and accessory changes. (Implemented with polling plus HS2API events.)
4. Build a normalized `CharacterContext`. (Initial version implemented.)
5. Select a contextual line and display it through a dedicated UI presenter. (Initial IMGUI presenter implemented.)
6. Add cooldown, repetition protection, configuration, and localization.

## Design constraints

- Do not overwrite AssetBundles.
- Do not block the Unity main thread with network requests.
- Keep AI integrations optional and outside the first milestone.
- Fail safely when dialogue data is malformed or unavailable.
- Keep game-specific observation separate from dialogue selection and presentation.
