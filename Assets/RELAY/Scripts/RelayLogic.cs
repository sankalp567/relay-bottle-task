using System;

// Plain C# with no Unity types, so RELAY > Run Logic Self-Check can test it directly.
namespace Relay
{
    /// <summary>How far a cap is screwed on. Clockwise (seen from above) tightens.</summary>
    public sealed class CapThreadModel
    {
        const float FreeAfterDegrees = 30f; // stops tracking noise popping a loose cap off

        public readonly float TotalDegrees;
        public float TightDegrees { get; private set; }
        float _unscrewPastLoose;

        public CapThreadModel(float totalDegrees, float startDegrees)
        {
            TotalDegrees = totalDegrees;
            TightDegrees = Math.Max(0f, Math.Min(totalDegrees, startDegrees));
        }

        public float Tightness => TightDegrees / TotalDegrees;
        public bool IsClosed => TightDegrees >= TotalDegrees - 0.01f;

        /// <summary>Applies a twist. Returns true when unscrewing has taken the cap off.</summary>
        public bool ApplyTwist(float clockwiseDegrees)
        {
            float next = TightDegrees + clockwiseDegrees;
            if (next > 0f)
            {
                _unscrewPastLoose = 0f;
                TightDegrees = Math.Min(next, TotalDegrees);
                return false;
            }

            _unscrewPastLoose += -next;
            TightDegrees = 0f;
            return _unscrewPastLoose >= FreeAfterDegrees;
        }
    }

    /// <summary>Turns a stream of hand angles (-180..180) into clockwise-positive changes.</summary>
    public sealed class TwistTracker
    {
        float _last;
        bool _hasLast;

        public void Reset() => _hasLast = false;

        public float Step(float angleDegrees, float maxStepDegrees = 45f)
        {
            if (!_hasLast)
            {
                _last = angleDegrees;
                _hasLast = true;
                return 0f;
            }

            float delta = ((angleDegrees - _last + 540f) % 360f) - 180f;
            _last = angleDegrees;
            return Math.Abs(delta) > maxStepDegrees ? 0f : delta; // a big jump is a tracking glitch
        }
    }

    public static class PourModel
    {
        /// <summary>Tilt from upright where water reaches the mouth: a full bottle pours sooner.</summary>
        public static float StartAngle(float fill01) => 105f + (60f - 105f) * Clamp01(fill01);

        public static float FlowMlPerSecond(float tiltDegrees, float fill01, float maxFlow)
        {
            if (fill01 <= 0f) return 0f;
            return Clamp01((tiltDegrees - StartAngle(fill01)) / 30f) * maxFlow;
        }

        static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));
    }

    /// <summary>
    /// Decides which object is being looked at from a stream of per-frame hits. A look starts after
    /// minDwell seconds on one object and survives gaps (blinks, noise) shorter than gapTolerance.
    /// Works from hits rather than eye speed, so following a moving bottle still counts as looking at it.
    /// </summary>
    public sealed class AoiDwellFilter
    {
        readonly double _minDwell;
        readonly double _gapTolerance;
        string _candidate;
        double _candidateStart;
        double _activeStart;
        double _lastActiveHit;

        public string Active { get; private set; }

        public AoiDwellFilter(double minDwell, double gapTolerance)
        {
            _minDwell = minDwell;
            _gapTolerance = gapTolerance;
        }

        /// <summary>Returns true if a look ended or started on this sample.</summary>
        public bool Step(double t, string hit, out string ended, out double endedDuration, out string started)
        {
            ended = null;
            endedDuration = 0;
            started = null;

            if (hit != null && hit == Active)
            {
                _lastActiveHit = t;
                _candidate = null;
                return false;
            }

            if (Active != null && t - _lastActiveHit > _gapTolerance)
            {
                ended = Active;
                endedDuration = _lastActiveHit - _activeStart;
                Active = null;
            }

            if (hit == null)
            {
                _candidate = null;
            }
            else
            {
                if (hit != _candidate)
                {
                    _candidate = hit;
                    _candidateStart = t;
                }

                if (t - _candidateStart >= _minDwell)
                {
                    if (Active != null)
                    {
                        ended = Active;
                        endedDuration = _lastActiveHit - _activeStart;
                    }

                    Active = _candidate;
                    _activeStart = _candidateStart;
                    _lastActiveHit = t;
                    _candidate = null;
                    started = Active;
                }
            }

            return ended != null || started != null;
        }
    }
}
