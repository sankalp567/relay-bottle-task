using UnityEngine;

namespace Relay
{
    /// <summary>A glass that fills up. Its pivot is at the centre of its base.</summary>
    public class Glass : MonoBehaviour
    {
        public string id = "1";
        public float capacityMl = 250f;
        public float innerRadius = 0.035f;
        public float innerHeight = 0.11f;
        public float baseThickness = 0.005f;
        [Tooltip("Cylinder showing the water. Scaled to match the level.")]
        public Transform waterVisual;
        [Tooltip("Small mark on the glass showing how far to pour.")]
        public Transform fillLine;
        public float filledMl;

        bool _overfillLogged;

        public float Fill01 => filledMl / capacityMl;
        public float RimY => transform.position.y + baseThickness + innerHeight;
        public float WaterSurfaceY => transform.position.y + baseThickness + innerHeight * Fill01;

        void Awake() => UpdateWaterVisual();

        public bool IsUnder(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;
            return d.magnitude < innerRadius && worldPoint.y > RimY - 0.01f;
        }

        public void AddWater(float ml)
        {
            filledMl += ml;
            if (filledMl > capacityMl)
            {
                filledMl = capacityMl;
                if (!_overfillLogged)
                {
                    RelayLog.Event("GLASS_OVERFLOW", $"glass={id}");
                    _overfillLogged = true;
                }
            }

            UpdateWaterVisual();
        }

        public void SetFillLine(float fraction)
        {
            if (fillLine == null) return;
            Vector3 p = fillLine.localPosition;
            p.y = baseThickness + innerHeight * fraction;
            fillLine.localPosition = p;
        }

        void UpdateWaterVisual()
        {
            if (waterVisual == null) return;
            float height = innerHeight * Fill01;
            waterVisual.gameObject.SetActive(height > 0.0005f);
            float width = innerRadius * 2f * 0.95f;
            waterVisual.localPosition = new Vector3(0f, baseThickness + height * 0.5f, 0f);
            waterVisual.localScale = new Vector3(width, height * 0.5f, width); // a Unity cylinder is 2 units tall
        }
    }
}
