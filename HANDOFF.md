# Team handoff: plugging into the VR environment

The bench, task, panel, feedback, test tools and data recording are built. This note shows where your part connects. Setup, controls and data files are in the [README](README.md).

**Run this first:** open the project in Unity 6000.6.8f1, then click **RELAY > Run Full Trial Test**. The Console should end with `failed=0`.

## Logic engine

Add a class that subclasses `RelayEngine`, and add it as a component on `RelayManager`. It then appears as a condition in the Session panel. Override the signals you need and call the helpers:

```csharp
namespace Relay
{
    public class RelayGazeEngine : RelayEngine
    {
        public override string ConditionName => "RELAY";

        protected override void OnLookStarted(string objectId)
        {
            // objectId is something like "bottle_A", "cap_A", "glass_1", "spot_A", "tray" or "display"
            if (Current < LastStep && objectId == StepContent.TargetId(Steps[Current + 1])) PreviewNext();
        }

        protected override void OnGrabbed(string objectId, string hand)
        {
            if (Current < LastStep && objectId == StepContent.TargetId(Steps[Current + 1])) AdvanceStep();
        }
    }
}
```

That's only an example; the real rule is yours to design.

| You can observe | Override |
|---|---|
| The eyes settled on, or left, an object | `OnLookStarted`, `OnLookEnded` |
| A hand closed on, or let go of, an object | `OnGrabbed`, `OnReleased` |
| A hand or a held bottle reached, or left, an object | `OnArrived`, `OnDeparted` |
| The backup input: a tap advances and a double tap goes back, by default | `OnBackupPressed` |

| You can do | Call |
|---|---|
| Show the next step's ghost, or cancel it | `PreviewNext()`, `CancelPreview()` |
| Move to the next step (past the last step, this finishes the task) | `AdvanceStep()` |
| Go back a step | `UndoStep()` |
| Warn about a slip | `Presenter.SlipLooking(Current, wrongId)`, `Presenter.SlipGrabbed(Current, wrongId)`, `Presenter.SlipCleared()` |
| Pause and resume | `Presenter.Paused(Current)`, `Presenter.Resumed()` |
| Report lost eye tracking | `Presenter.TrackingChanged(true)` |

Each of these already drives the panel, the object highlights, the sounds and the vibration.

**One hard rule: never read `TaskTruth`.** It's the answer key used to score your engine. If the engine reads it, RELAY becomes auto-advance that only works because VR knows everything. The recorder marks every advance against it for you (`false_advances` and `mean_advance_delay_s` in `trials.csv`).

**You can test with no headset.** Desk mode drives the same hands and signals, using the mouse as both the hand and the gaze. Only a headset gives real eye data, though, so tune your timing thresholds there.

## LLM pipeline

Write a JSON file to `Assets/StreamingAssets/RELAY/`. It appears in the Session panel automatically. Copy the format of `task_one_round.json`:

```json
{
  "title": "Bottle task",
  "steps": [
    { "kind": "PickBottle", "text": "Pick up bottle A", "bottle": "A", "glass": "", "pourFraction": 0 },
    { "kind": "Pour", "text": "Pour into glass 1, up to the line", "bottle": "A", "glass": "1", "pourFraction": 0.5, "pedalConfirm": false }
  ]
}
```

| Field | Allowed values |
|---|---|
| `kind` | `PickBottle`, `OpenCap`, `Pour`, `CloseCap`, `PutBack` |
| `bottle` | `A`, `B`, `C` |
| `glass` | `1`, `2`, `3` (only used by `Pour`) |
| `pourFraction` | 0 to 1: how far up the glass the line sits (only used by `Pour`) |
| `pedalConfirm` | `true` shows "tap the pedal when done" on steps gaze can't be trusted on |
| `text` | Short, ideally 7 words or fewer |

A wrong `bottle` or `glass` stops the task at load and prints an error in the Console. The panel's step pictures are made automatically from the bottle or glass named in the step. A new task can only use these 5 kinds of step and these objects; anything else needs a change to the environment.

The panel can't show Hindi or Gujarati yet, because that needs fonts for those scripts.

## VR setup

Follow "Move to the headset PC" in the README. Then please check these, because none of them could be tested without the hardware:

1. `GazeInput.cs` compiles against the real SRanipal SDK, and looking at an object logs `LOOK_START`.
2. Grabbing and twisting feel workable with Vive controllers. If 1.5 turns is too much, lower `Turns To Close`.
3. The panel text is readable in the headset.
4. `trials.csv` shows `mean_fps` near 90 and few `slow_frames`.

Please tell the environment owner what you find.

## Decisions the team still needs to make

1. **The advance rule.** RELAY's "eyes leave the part at the grab" signal is real for only one of the four step changes in the 5-step task. In the others, the eyes stay put or the bottle is already in hand, so nothing departs. Either widen the rule (eyes reach the next target, then a hand or the held bottle arrives, using `OnArrived`), or use `task_three_rounds.json`, where 5 of the 14 step changes are real grabs. This also affects the novelty claim.
2. **The backup action.** Will there be a real foot pedal? USB pedals usually send a key press, so set `Backup Key` on `RelaySignals` to match, and change `Fallback Action` on `InstructionPanel` if the wording should differ.
3. **Voice.** Voice was part of the planned comparison, but no speech recogniser is built. If you want it, it can be an engine that calls the same helpers.
