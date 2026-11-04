using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
#if RELAY_SRANIPAL
using ViveSR.anipal.Eye;
#endif

namespace Relay
{
    /// <summary>
    /// One source of gaze with three versions. It never falls back to another version by itself:
    /// head direction standing in for eyes would quietly invalidate the data.
    /// </summary>
    public class GazeInput : MonoBehaviour
    {
        public enum Mode
        {
            Auto,  // eyes on a headset, mouse at a desk, so mouse data can't be recorded by mistake
            Mouse, // testing without a headset
            Head,  // head direction only; not valid for RELAY results
            Eyes   // Vive Pro Eye through SRanipal
        }

        public Mode mode = Mode.Auto;

        Camera _camera;
        bool _warnedNoSdk;

        public bool TryGetRay(out Ray ray)
        {
            ray = default;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;

            Mode active = mode == Mode.Auto ? (XRSettings.isDeviceActive ? Mode.Eyes : Mode.Mouse) : mode;
            switch (active)
            {
                case Mode.Mouse:
                    if (Mouse.current == null) return false;
                    ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                    return true;

                case Mode.Head:
                    ray = new Ray(_camera.transform.position, _camera.transform.forward);
                    return true;

                case Mode.Eyes:
                    return TryGetEyeRay(out ray);

                default:
                    return false;
            }
        }

        bool TryGetEyeRay(out Ray ray)
        {
            ray = default;
#if RELAY_SRANIPAL
            if (SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING) return false;
            if (!SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out Vector3 _, out Vector3 localDirection)) return false;

            // SRanipal gives the direction relative to the head.
            Transform head = _camera.transform;
            ray = new Ray(head.position, head.TransformDirection(localDirection));
            return true;
#else
            if (!_warnedNoSdk)
            {
                Debug.LogError("[RELAY] Eyes mode needs the SRanipal SDK and the RELAY_SRANIPAL scripting define. See the README.");
                _warnedNoSdk = true;
            }

            return false;
#endif
        }
    }
}
