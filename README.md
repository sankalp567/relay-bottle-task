# RELAY bottle task: VR environment

The virtual workbench for the CS 435 RELAY project. A person picks up a bottle, opens its cap, pours water to a line, closes the cap and puts the bottle back, while an instruction panel guides them. Teammates: start with [HANDOFF.md](HANDOFF.md).

## Status

Built and tested on a Mac in Unity 6000.6.8f1 with XR Interaction Toolkit 3.7.0.

- **Tested:** the project compiles with no errors. **Run Full Trial Test** plays a whole recorded trial using the XR rig's real hands and real near-grab detection (37 checks). **Run Task Test** covers the task and its mistakes (44 checks). **Save Panel Images** covers every panel state (29 checks).
- **Not tested:** anything on a real headset: how grabbing and twisting feel with Vive controllers, how readable the panel is, frame rate, vibration, and the SRanipal eye tracking code. Sounds haven't been checked by ear.
- **Not included:** RELAY's advance logic and voice input. See [HANDOFF.md](HANDOFF.md).

## Open it

1. In Unity Hub, go to **Projects > Add > Add project from disk** and choose this folder.
2. Open it with version **6000.6.8f1**.
3. In the **Project** panel, double-click `Assets/RELAY/RelayBottleScene`, then press **Play**.

## Desk mode: the whole task without a headset

Desk mode switches on by itself when no headset is active. It uses the same hands and grab detection as a headset. If you ticked **Use XR Interaction Simulator in scenes** earlier, you can untick it; desk mode replaces it.

| To do this | Do this |
|---|---|
| Move the hand | Move the mouse |
| Grab, or let go | Click (a hand keeps hold until you click again) |
| Use the other hand | `Tab` |
| Twist a cap open / closed | Hold `Q` / `E` |
| Tilt to pour | Hold `A` or `D` (it levels again when you let go) |
| Raise / lower what you hold | `W` / `S` |
| Look around | Hold the right mouse button and move the mouse |

**Try the task:** point at bottle A and click. Press `Tab`, point at its cap, click, and hold `Q` until it comes off. Press `Tab`, move the bottle next to glass 1, and hold `D` until the stream lands inside and the water reaches the line. Press `Tab`, point the cap at the bottle's mouth, and hold `E` until it's closed, then click to let go. Press `Tab`, point at bottle A's ring and click to set the bottle down.

In desk mode the mouse is both the hand and the gaze. That's fine for building and checking, but only a headset gives real eye data.

## Running a test session

The **Session** panel at the top left is on the desktop window only; the person in the headset can't see it.

1. Enter the participant ID, and choose the condition and task.
2. Click **Start trial**. The panel shows step 1 and recording starts.
3. The trial ends when the engine finishes the task, or click **End trial**.
4. Click **Next trial** to reset the bench and count up.

**Conditions:** **Manual** is the experimenter moving the panel with on-screen buttons. **Touch** is a button on the bench. Any new engine added to `RelayManager` appears here automatically.

**Data** is saved to `RELAY_Data` in this folder:

| File | Contents |
|---|---|
| `trials.csv` | One row per trial: duration, steps done, false advances, mean advance delay, pour error, mistakes, frame rate |
| `*_events.csv` | Every event with its time, such as grabs, looks, slips, and what the panel showed |
| `*_samples.csv` | One row per frame: head, gaze direction, what's being looked at, both hands, true step, shown step |

**False advances** count the panel moving on before the step was really finished. **Advance delay** is how long after a step was really finished the panel moved on. Together they're the main measures of any engine. `trials.csv` also reports frame rate, so check it on the headset: a headset needs a steady 90 frames per second.

## The bench

- Three look-alike bottles (A, B, C), each with a marked spot, and three glasses (1, 2, 3).
- The cap needs two hands: its handle only works while the other hand holds the bottle. Reaching for the top of a free bottle just picks the bottle up.
- A dropped object returns to the table.
- The room is deliberately plain, because anything eye-catching would pull people's gaze and spoil the data.
- All models and textures are generated in code by `RelayMeshes.cs`, so there's nothing to download or license.

## The RELAY menu

| Item | What it does |
|---|---|
| **Build Bottle Scene** | Rebuilds the scene from scratch, replacing any manual changes to it |
| **Run Full Trial Test** | Plays a whole recorded trial with the rig's real hands, then checks the data and the reset |
| **Run Task Test** | Plays the task and its mistakes with scripted hands |
| **Run Logic Self-Check** | Tests the cap, twist, pour and gaze maths, with no scene needed |
| **Save Panel Images** | Shows every panel state, checks it, and saves a picture of each to `Previews` |
| **Save Preview Images** | Saves pictures of the scene to `Previews`, for reports and slides |

## Move to the headset PC (Windows)

SteamVR doesn't run on macOS, so the Vive Pro Eye needs a Windows PC. Install the same Unity version (6000.6.8f1) there.

1. Install SteamVR and set it as the OpenXR runtime.
2. The OpenXR package is already in this project. In **Project Settings > XR Plug-in Management**, tick **OpenXR** on the Windows tab, then add **HTC Vive Controller Profile** under **OpenXR > Interaction Profiles**.
3. Untick **Use XR Interaction Simulator in scenes** if it's ticked.
4. Install the SRanipal runtime and import the SRanipal Unity SDK from HTC's developer site.
5. Add the SDK's `SRanipal_Eye_Framework` to the scene, set to eye data version 2.
6. In **Project Settings > Player > Scripting Define Symbols**, add `RELAY_SRANIPAL`.
7. Run SRanipal's eye calibration for each person.

Gaze is set to **Auto**, which uses eyes whenever a headset is active. It never falls back to head direction by itself: if eye data stops, the log shows `GAZE_LOST`. The eye code follows HTC's sample as seen in community copies and hasn't been checked against the SDK. If `GazeInput.cs` doesn't compile after step 6, compare it with `SRanipal_GazeRaySample` in the SDK.

## Settings you may want to change

- **Backup key:** `Backup Key` on `RelaySignals`, default Space. USB foot pedals usually send a key press, so set it to whatever yours sends.
- **Backup wording:** `Fallback Action` on `InstructionPanel`, default "Tap the pedal".
- **Sound:** `Muted` on `RelayPresenter`.
- **Cap effort:** `Turns To Close` on each cap, default 1.5.
- **Build-time helpers:** these are off by default and must stay off with participants. `DebugStepLabel` shows the true step, and `Show Debug Dot` on `GazeObjectTracker` shows a gaze dot that would draw people's eyes.
- **Not built:** Hindi and Gujarati panel text, which need fonts for those scripts.
