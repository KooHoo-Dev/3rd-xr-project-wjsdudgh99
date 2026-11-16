using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeAeng
{
    /// <summary>
    /// 방 불 On / Off 상태를 관리한다.
    /// - 불 끔: 달빛(Directional) + 아주 어두운 Ambient
    /// - 불 켬: 천장 형광등(Point) + 밝은 Ambient, 켜질 때 짧게 깜빡임
    /// </summary>
    public class RoomLightController : MonoBehaviour
    {
        [Header("Lights")]
        public Light moonLight;
        public Light ceilingLight;
        [Tooltip("천장 형광등 모양 오브젝트 (Unlit 머티리얼). 불 상태에 따라 색이 바뀜")]
        public Renderer ceilingPanel;

        [Header("불 끈 상태 (달빛)")]
        public Color ambientOffColor = new Color(0.035f, 0.045f, 0.08f);
        [Range(0f, 4f)] public float ambientOffIntensity = 1f;
        [Range(0f, 2f)] public float moonIntensity = 0.1f;

        [Header("불 켠 상태 (형광등)")]
        public Color ambientOnColor = new Color(0.42f, 0.42f, 0.44f);
        [Range(0f, 6f)] public float ceilingIntensity = 1f;
        public Color panelOnColor = new Color(0.95f, 0.97f, 1f);
        public Color panelOffColor = new Color(0.07f, 0.07f, 0.08f);

        [Header("연출")]
        public bool flickerOnTurnOn = true;
        [Tooltip("켜질 때 깜빡임 패턴 (켜짐/꺼짐이 번갈아 가며 각 구간의 길이, 초)")]
        public float[] flickerPattern = { 0.05f, 0.08f, 0.04f, 0.12f, 0.06f };

        [SerializeField] bool startOn = false;

        public bool IsOn { get; private set; }

        /// <summary>불 상태가 최종적으로 바뀌었을 때 (깜빡임 시작 시점에 호출)</summary>
        public event Action<bool> LightChanged;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;
        Coroutine _flicker;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            RenderSettings.ambientMode = AmbientMode.Flat;
        }

        void Start()
        {
            SetLight(startOn, false);
        }

        public void Toggle() => SetLight(!IsOn, true);

        public void SetLight(bool on, bool withFlicker = true)
        {
            IsOn = on;
            if (_flicker != null) { StopCoroutine(_flicker); _flicker = null; }

            if (on && withFlicker && flickerOnTurnOn && flickerPattern != null && flickerPattern.Length > 0)
                _flicker = StartCoroutine(FlickerRoutine());
            else
                ApplyVisual(on);

            LightChanged?.Invoke(on);
        }

        /// <summary>인스펙터나 튜너에서 값을 바꾼 뒤 현재 상태로 다시 적용</summary>
        public void ApplyCurrent()
        {
            if (_flicker == null) ApplyVisual(IsOn);
        }

        IEnumerator FlickerRoutine()
        {
            bool visualOn = true;
            foreach (float t in flickerPattern)
            {
                ApplyVisual(visualOn);
                yield return new WaitForSeconds(t);
                visualOn = !visualOn;
            }
            ApplyVisual(true);
            _flicker = null;
        }

        void ApplyVisual(bool on)
        {
            if (moonLight != null)
            {
                moonLight.enabled = true; // 달빛은 항상 있음 (불 켜면 묻힘)
                moonLight.intensity = moonIntensity;
            }

            if (ceilingLight != null)
            {
                ceilingLight.enabled = on;
                ceilingLight.intensity = ceilingIntensity;
            }

            Color ambient = on ? ambientOnColor : ambientOffColor * ambientOffIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            // URP는 ambient probe(SH)를 읽으므로 직접 갱신해 준다.
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = sh;

            if (ceilingPanel != null)
            {
                ceilingPanel.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, on ? panelOnColor : panelOffColor);
                ceilingPanel.SetPropertyBlock(_mpb);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying && _mpb != null) ApplyCurrent();
        }
#endif
    }
}
