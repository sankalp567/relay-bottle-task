using System;
using UnityEditor;
using UnityEngine;

namespace Relay.EditorTools
{
    /// <summary>Tests the plain-C# logic with no headset and no scene.</summary>
    public static class RelaySelfCheck
    {
        static int _failures;

        [MenuItem("RELAY/Run Logic Self-Check")]
        public static void Run()
        {
            _failures = 0;
            CheckCap();
            CheckTwist();
            CheckPour();
            CheckDwell();

            if (_failures == 0) Debug.Log("[RELAY] Self-check passed.");
            else Debug.LogError($"[RELAY] Self-check failed: {_failures} problem(s).");
        }

        static void Check(bool condition, string what)
        {
            if (condition) return;
            _failures++;
            Debug.LogError($"[RELAY] Self-check: {what}");
        }

        static void CheckCap()
        {
            var cap = new CapThreadModel(540f, 540f);
            Check(cap.IsClosed, "a new cap should start closed");
            Check(!cap.ApplyTwist(-539f), "the cap should not come off before it is loose");
            Check(!cap.IsClosed, "a loosened cap should not count as closed");
            Check(!cap.ApplyTwist(-5f), "noise at the loose end should not pop the cap off");
            Check(cap.ApplyTwist(-40f), "unscrewing past the loose end should take the cap off");

            var seated = new CapThreadModel(540f, 0f);
            Check(!seated.ApplyTwist(540f) && seated.IsClosed, "twisting clockwise should close the cap");
            Check(seated.TightDegrees <= 540f, "tightness should not pass the maximum");
        }

        static void CheckTwist()
        {
            var twist = new TwistTracker();
            Check(Near(twist.Step(170f), 0f), "the first sample should give no change");
            Check(Near(twist.Step(-170f), 20f), "crossing +/-180 clockwise should give +20");
            Check(Near(twist.Step(170f), -20f), "crossing +/-180 counter-clockwise should give -20");
            Check(Near(twist.Step(60f), 0f), "a large jump should be ignored as a glitch");
        }

        static void CheckPour()
        {
            Check(PourModel.StartAngle(1f) < PourModel.StartAngle(0.1f), "a full bottle should pour at a smaller tilt");
            Check(Near(PourModel.FlowMlPerSecond(10f, 1f, 80f), 0f), "an upright bottle should not pour");
            Check(PourModel.FlowMlPerSecond(90f, 1f, 80f) > 0f, "a tipped full bottle should pour");
            Check(Near(PourModel.FlowMlPerSecond(170f, 0f, 80f), 0f), "an empty bottle should not pour");
        }

        static void CheckDwell()
        {
            var filter = new AoiDwellFilter(0.1, 0.08);
            string ended, started;
            double duration;

            filter.Step(0.00, "glass", out ended, out duration, out started);
            Check(filter.Active == null, "a look should not start before the minimum dwell");
            filter.Step(0.12, "glass", out ended, out duration, out started);
            Check(started == "glass", "a look should start after the minimum dwell");
            filter.Step(0.16, null, out ended, out duration, out started);
            Check(ended == null && filter.Active == "glass", "a short gap should not end the look");
            filter.Step(0.30, null, out ended, out duration, out started);
            Check(ended == "glass" && Math.Abs(duration - 0.12) < 1e-6, "a long gap should end the look");

            filter.Step(0.40, "bottle", out ended, out duration, out started);
            filter.Step(0.52, "bottle", out ended, out duration, out started);
            filter.Step(0.58, "cap", out ended, out duration, out started);
            Check(filter.Active == "bottle", "the old look should hold until the new one qualifies");
            filter.Step(0.72, "cap", out ended, out duration, out started);
            Check(ended == "bottle" && started == "cap", "a qualified new look should replace the old one");
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
    }
}
