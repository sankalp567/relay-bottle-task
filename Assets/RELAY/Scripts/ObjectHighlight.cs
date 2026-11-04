using UnityEngine;

namespace Relay
{
    /// <summary>Lights up an object in the world: the virtual version of the bin LEDs.</summary>
    public class ObjectHighlight : MonoBehaviour
    {
        public enum Mode
        {
            Off,
            Solid,
            Pulse,
            FlashOnce
        }

        const float FlashSeconds = 0.4f;
        const float PulsesPerSecond = 1.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public string id = "object";
        public Renderer[] renderers;

        MaterialPropertyBlock _block;
        Color[] _baseColors;
        Color _color;
        float _startTime;

        public Mode CurrentMode { get; private set; }

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) _baseColors[i] = renderers[i].sharedMaterial.color;
        }

        public void Show(Color color, Mode mode)
        {
            _color = color;
            _startTime = Time.time;
            CurrentMode = mode;
            if (mode == Mode.Off) Clear();
        }

        public void Hide() => Show(Color.black, Mode.Off);

        void Update()
        {
            float elapsed = Time.time - _startTime;
            switch (CurrentMode)
            {
                case Mode.Solid:
                    Apply(1f);
                    break;
                case Mode.Pulse:
                    Apply(0.75f + 0.25f * (0.5f + 0.5f * Mathf.Sin(elapsed * PulsesPerSecond * 2f * Mathf.PI)));
                    break;
                case Mode.FlashOnce:
                    if (elapsed < FlashSeconds) Apply(1f);
                    else Hide();
                    break;
            }
        }

        // Tints as well as glows, so red still reads as red on a blue bottle.
        void Apply(float strength)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Color tinted = Color.Lerp(_baseColors[i], _color, 0.9f * strength);
                tinted.a = _baseColors[i].a;
                _block.SetColor(BaseColorId, tinted);
                _block.SetColor(EmissionColorId, _color * (0.6f * strength));
                renderers[i].SetPropertyBlock(_block);
            }
        }

        void Clear()
        {
            foreach (Renderer r in renderers) r.SetPropertyBlock(null);
        }
    }
}
