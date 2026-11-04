using System;
using UnityEngine;

namespace Relay
{
    /// <summary>Works out which object is being looked at, from the angle between the gaze and each object.</summary>
    public class GazeObjectTracker : MonoBehaviour
    {
        public GazeInput gaze;
        [Tooltip("Added to each object's size to allow for eye tracker error.")]
        public float paddingDegrees = 2f;
        public float minDwellSeconds = 0.1f;
        public float gapToleranceSeconds = 0.08f;
        [Tooltip("How long gaze data can be missing before it's reported as lost. Longer than a blink.")]
        public float lostAfterSeconds = 0.5f;
        [Tooltip("For building only: a visible gaze dot draws the eyes and would spoil the gaze data.")]
        public bool showDebugDot;

        public event Action<string> LookStarted;
        public string CurrentHit { get; private set; }
        public bool HasGaze { get; private set; }
        public Ray LastRay { get; private set; }
        public event Action<string, double> LookEnded;

        AoiTarget[] _targets;
        AoiDwellFilter _filter;
        Transform _dot;
        float _lastGazeTime;
        bool _lost;

        void Start()
        {
            _targets = FindObjectsByType<AoiTarget>(FindObjectsInactive.Include);
            _filter = new AoiDwellFilter(minDwellSeconds, gapToleranceSeconds);
            _lastGazeTime = Time.time;

            if (showDebugDot)
            {
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "GazeDebugDot";
                Destroy(dot.GetComponent<Collider>());
                dot.transform.localScale = Vector3.one * 0.012f;
                _dot = dot.transform;
            }
        }

        void Update()
        {
            string hit = null;
            HasGaze = gaze.TryGetRay(out Ray ray);
            if (HasGaze)
            {
                LastRay = ray;
                _lastGazeTime = Time.time;
                if (_lost)
                {
                    _lost = false;
                    RelayLog.Event("GAZE_BACK");
                }

                AoiTarget target = FindTarget(ray, out float distance);
                hit = target != null ? target.id : null;
                if (_dot != null) _dot.position = ray.origin + ray.direction * distance;
            }
            else if (!_lost && Time.time - _lastGazeTime > lostAfterSeconds)
            {
                _lost = true;
                RelayLog.Event("GAZE_LOST", $"mode={gaze.mode}");
            }

            CurrentHit = hit;
            if (!_filter.Step(Time.timeAsDouble, hit, out string ended, out double duration, out string started)) return;

            if (ended != null)
            {
                RelayLog.Event("LOOK_END", $"{ended} {duration:F3}s");
                LookEnded?.Invoke(ended, duration);
            }

            if (started != null)
            {
                RelayLog.Event("LOOK_START", started);
                LookStarted?.Invoke(started);
            }
        }

        // Scores by angle relative to the object's apparent size, so looking at a small cap on a big
        // bottle picks the cap.
        AoiTarget FindTarget(Ray ray, out float distance)
        {
            AoiTarget best = null;
            float bestScore = 1f;
            distance = 1.2f;

            foreach (AoiTarget target in _targets)
            {
                if (!target.gameObject.activeInHierarchy) continue;

                Vector3 toTarget = target.Center - ray.origin;
                float targetDistance = toTarget.magnitude;
                if (targetDistance < 0.01f) continue;

                float angularRadius = Mathf.Atan2(target.radiusMeters, targetDistance) * Mathf.Rad2Deg + paddingDegrees;
                float score = Vector3.Angle(ray.direction, toTarget) / angularRadius;
                if (score <= bestScore)
                {
                    bestScore = score;
                    best = target;
                    distance = targetDistance;
                }
            }

            return best;
        }
    }
}
