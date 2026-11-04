using System;
using UnityEngine;

namespace Relay
{
    /// <summary>
    /// Pouring without fluid simulation: tilt sets the flow, and the point under the bottle's mouth
    /// decides where the water goes. The stream is a thin stretched cylinder.
    /// </summary>
    [RequireComponent(typeof(Bottle))]
    public class BottlePour : MonoBehaviour
    {
        public Material waterMaterial;
        public float maxFlowMlPerSecond = 80f;

        public event Action<Glass> PouredInto;
        public event Action Spilled;

        Bottle _bottle;
        Rigidbody _rb;
        Glass[] _glasses;
        Transform _stream;
        Glass _currentGlass;
        bool _spilling;
        float _lastFlowTime = float.NegativeInfinity;
        readonly RaycastHit[] _hits = new RaycastHit[8];

        public bool IsFlowing { get; private set; }
        public float SecondsSinceFlow => Time.time - _lastFlowTime;

        void Start()
        {
            _bottle = GetComponent<Bottle>();
            _rb = GetComponent<Rigidbody>();
            _glasses = FindObjectsByType<Glass>();

            GameObject stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stream.name = $"WaterStream_{_bottle.id}";
            Destroy(stream.GetComponent<Collider>());
            if (waterMaterial != null) stream.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            _stream = stream.transform;
            _stream.gameObject.SetActive(false);
        }

        void Update()
        {
            float flow = _bottle.cap.IsOff
                ? PourModel.FlowMlPerSecond(_bottle.TiltDegrees, _bottle.Fill01, maxFlowMlPerSecond)
                : 0f;

            if (flow <= 0f)
            {
                StopStream();
                return;
            }

            float ml = Mathf.Min(flow * Time.deltaTime, _bottle.volumeMl);
            _bottle.volumeMl -= ml;
            IsFlowing = true;
            _lastFlowTime = Time.time;

            Vector3 mouth = _bottle.mouth.position;
            Glass glass = FindGlassUnder(mouth);
            float landingY;

            if (glass != null)
            {
                glass.AddWater(ml);
                landingY = glass.WaterSurfaceY;
                _spilling = false;
                if (glass != _currentGlass)
                {
                    _currentGlass = glass;
                    PouredInto?.Invoke(glass);
                }
            }
            else
            {
                landingY = FindLandingY(mouth);
                _currentGlass = null;
                if (!_spilling)
                {
                    _spilling = true;
                    Spilled?.Invoke();
                }
            }

            DrawStream(mouth, landingY, flow);
        }

        void StopStream()
        {
            IsFlowing = false;
            _currentGlass = null;
            _spilling = false;
            if (_stream != null) _stream.gameObject.SetActive(false);
        }

        Glass FindGlassUnder(Vector3 point)
        {
            foreach (Glass glass in _glasses)
            {
                if (glass.IsUnder(point)) return glass;
            }

            return null;
        }

        float FindLandingY(Vector3 from)
        {
            int count = Physics.RaycastNonAlloc(from, Vector3.down, _hits, 3f, ~0, QueryTriggerInteraction.Ignore);
            float highest = 0f; // the floor
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].rigidbody == _rb) continue; // don't land on this bottle
                highest = Mathf.Max(highest, _hits[i].point.y);
            }

            return highest;
        }

        void DrawStream(Vector3 mouth, float landingY, float flow)
        {
            float length = Mathf.Max(mouth.y - landingY, 0.001f);
            float width = Mathf.Lerp(0.006f, 0.014f, flow / maxFlowMlPerSecond);
            _stream.gameObject.SetActive(true);
            _stream.position = new Vector3(mouth.x, landingY + length * 0.5f, mouth.z);
            _stream.rotation = Quaternion.identity;
            _stream.localScale = new Vector3(width, length * 0.5f, width); // a Unity cylinder is 2 units tall
        }
    }
}
