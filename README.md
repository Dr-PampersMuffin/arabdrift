# Arab Drift: روح الشارع

iPhone drift game set in 1980s Kuwait (Unity 6, URP). Built and shipped the same way as Sands of Empire:
GitHub Actions exports the Unity project, archives it on macOS and uploads it to TestFlight.

- Design doc: https://claude.ai/artifact/5Y3D2AaFtQDqJ9KXynX39N
- Bundle ID: `world.embur.arabdrift` (Embur Inc.), App Store name: Arab Drift: روح الشارع
- Unity 6000.0.62f1, project in `client/`. The whole level is built in code by `GameBootstrap` from an empty scene called `Salwa`.

## Layout

| Path | What |
| --- | --- |
| `client/Assets/DriftKuwait/Scripts` | Stage 1 v0.1: الشاهين car physics, Salwa district, drift camera, scoring, Arabic HUD, touch controls, engine and tyre audio |
| `client/Assets/Editor/BuildScript.cs` | CI build: URP setup, shader references, player settings, bundle ID |
| `client/Assets/Editor/IOSPostBuild.cs` | Xcode fixes: Game Center, privacy manifest, export compliance |
| `tools/elevenlabs` | ElevenLabs sound generator (`ELEVENLABS_API_KEY=... node tools/elevenlabs/generate-sounds.mjs`), logs every prompt |
| `tools/ci/check_scripts.py` | Syntax check run before the Unity step |
| `.github/workflows/ios.yml` | Build and TestFlight upload; push a tag `ios-N` to run it |

## Repository secrets (Settings, Secrets and variables, Actions)

`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`, `APPLE_TEAM_ID`, `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID`, `APPSTORE_P8`
(same values as Sands of Empire). Build reports land on the `ci-reports` branch (`latest.txt`).

## Build

```
git tag ios-1 && git push origin ios-1
```
