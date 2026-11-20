# DRIFT KUWAIT — روح الشارع · Stage 1 prototype (v0.1)

One car (الشاهين), one Salwa-inspired district with two roundabouts, drift physics, drift scoring
with Kuwaiti callouts, the drift camera, tyre smoke and skid marks, engine and tyre sound, an
Arabic right-to-left HUD and touch controls. Everything is built from code when you press Play,
so there are no scene or model files to break.

## 1. Install (one time)

1. Install **Unity Hub**, then **Unity 6 LTS (6000.0.x)**. Personal licence is free.
   Tick **iOS Build Support** while installing (and Windows/Mac build support for your machine).
2. In Unity Hub: **New project → Universal 3D** template, name it `DriftKuwait`.
3. Close Unity. Copy the folder `Assets/DriftKuwait` from this zip into your project's `Assets` folder.
   (Optional: copy `Tools/` next to `Assets/` for the sound generator.)
4. Open the project again. Wait for scripts to compile (bottom-right spinner).

## 2. Play it

1. **File → New Scene → Empty**, then **File → Save As…** and name it `Salwa` (the name matters).
2. Press **Play**. The district, car, lighting and HUD build themselves.

| Action | Keyboard | Gamepad | Phone |
| --- | --- | --- | --- |
| Throttle | W / ↑ | Right trigger | بنزين |
| Brake / reverse (hold) | S / ↓ | Left trigger | بريك |
| Steer | A D / ← → | Left stick | ◄ ► |
| Handbrake (start the slide) | Space | A / RB | هاند |
| Camera mode | C | | كاميرا (tap) |
| Near / far camera | V | | كاميرا (hold) |
| Weather: golden hour → noon → غبار → night | | | الجو |
| Reset car | R | | رجّع |
| Show touch controls in the editor | T | | |

**How to drift الشاهين:** build speed in 2nd, flick the steering, tap handbrake, then hold throttle.
The counter-steer assist catches the slide; keep the throttle on to hold the angle. Chain drifts for
a multiplier up to x5. Brush a wall without hitting it for "على الشعرة!". Hit it and you lose the chain.

## 3. Sound (ElevenLabs)

```bash
# Node 18+, from the project folder
ELEVENLABS_API_KEY=your_key node tools/elevenlabs/generate-sounds.mjs
```
On Windows Git Bash the same line works. It writes the files into
`Assets/DriftKuwait/Resources/DriftKuwait/Audio/` and logs every prompt to
`tools/elevenlabs/audio-provenance.json` (keep that file: it's your licence trail).
Until the files exist, the car uses a built-in synthesised turbo-four and tyre squeal.
Regenerate one sound: `node tools/elevenlabs/generate-sounds.mjs --only shaheen_4000 --force`.

## 4. Run on an iPhone

Unity builds an Xcode project, and Xcode needs a Mac. Two routes:
- **You have a Mac:** File → Build Profiles → iOS → Switch Platform → Build → open in Xcode → run on the phone.
- **No Mac (Windows):** use Unity Build Automation (cloud builds) with your Apple Developer account
  certificates, then install through TestFlight.

Before the first iPhone build: Project Settings → Player → iOS → set Bundle Identifier, Landscape only,
Target minimum iOS 15+, Graphics API Metal.

## 5. Tuning knobs (select the car in the Hierarchy while playing)

`DriftCarController`: `rearGrip` (lower = looser), `latC` (higher = slides hold longer),
`gameplayPower`, `turboSpoolTime` (the lag), `counterSteerAssist` (0 = pro, 1 = easy).

## 6. What's placeholder in v0.1

- الشاهين's body is a code-built blockout. The photoreal car replaces only the `Body` child.
- Textures are procedural noise; scanned PBR textures come in the art pass.
- The district is an original layout in the spirit of Salwa, not the real street plan yet.
- Kuwaiti callouts (حلو، عدل، كفو، يا سلام، على الشعرة، خربتها) and button words need a Kuwaiti writer's pass.

## 7. Send me back

When something breaks, send: a screenshot of the Console (red errors) and the Unity version.
When it works, send: a 20-second screen recording of a drift. I tune from that.
