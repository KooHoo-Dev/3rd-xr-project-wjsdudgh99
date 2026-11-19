using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

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
        [Tooltip("해가 뜬 뒤 꺼진 형광등 색. 형광등은 빛을 안 받는 재질이라, 낮에 어두운 색이면 천장에 검은 구멍처럼 보임 (11/19 헤드셋)")]
        public Color panelOffDayColor = new Color(0.62f, 0.62f, 0.64f);

        [Header("해 뜨기 (SetDawn 0 → 1)")]
        [Tooltip("새벽 빛 색 (왼쪽 = 해 뜨기 시작, 오른쪽 = 아침)")]
        public Gradient sunColor = DefaultSunColor();
        [Range(0f, 3f)] public float sunIntensity = 1.2f;
        [Tooltip("해의 고도 (도): 해 뜰 때 → 아침")]
        public Vector2 sunPitch = new Vector2(3f, 22f);
        [Tooltip("불 끈 상태의 Ambient 가 아침에 이 색까지 밝아짐")]
        public Color ambientMorningColor = new Color(0.30f, 0.27f, 0.25f);
        [Tooltip("이 구간(0~1)에서 달빛이 꺼지고 해로 바뀜")]
        [Range(0.01f, 0.5f)] public float moonFadeOut = 0.15f;

        [Header("연출")]
        public bool flickerOnTurnOn = true;
        [Tooltip("켜질 때 깜빡임 패턴 (켜짐/꺼짐이 번갈아 가며 각 구간의 길이, 초)")]
        public float[] flickerPattern = { 0.05f, 0.08f, 0.04f, 0.12f, 0.06f };

        [Tooltip("씬 시작할 때 불 켜기 (메인 화면: 켬)")]
        [FormerlySerializedAs("startOn")] public bool startOn = false;

        public bool IsOn { get; private set; }

        /// <summary>불 상태가 최종적으로 바뀌었을 때 (깜빡임 시작 시점에 호출)</summary>
        public event Action<bool> LightChanged;

        /// <summary>해 뜨기 진행도 0(밤) ~ 1(아침)</summary>
        public float Dawn { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;
        Coroutine _flicker;
        Quaternion _moonRot;
        Color _moonColor;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            RenderSettings.ambientMode = AmbientMode.Flat;
            if (moonLight != null)
            {
                _moonRot = moonLight.transform.rotation;
                _moonColor = moonLight.color;
            }
        }

        /// <summary>해 뜨기 진행도 설정 (SunriseController 가 매 프레임 호출). 0 = 밤, 1 = 아침</summary>
        public void SetDawn(float t)
        {
            t = Mathf.Clamp01(t);
            if (Mathf.Abs(t - Dawn) < 0.0005f) return;
            Dawn = t;
            ApplyCurrent();
        }

        static Gradient DefaultSunColor()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.00f, 0.45f, 0.25f), 0f),   // 붉은 새벽
                    new GradientColorKey(new Color(1.00f, 0.70f, 0.45f), 0.45f), // 주황
                    new GradientColorKey(new Color(1.00f, 0.93f, 0.80f), 1f),   // 아침 햇살
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
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
                if (Dawn <= 0f)
                {
                    moonLight.transform.rotation = _moonRot;
                    moonLight.color = _moonColor;
                    moonLight.intensity = moonIntensity;
                }
                else if (Dawn < moonFadeOut)
                {
                    // 1단계: 달빛이 서서히 꺼짐 (각도는 그대로)
                    moonLight.transform.rotation = _moonRot;
                    moonLight.color = _moonColor;
                    moonLight.intensity = Mathf.Lerp(moonIntensity, 0f, Dawn / moonFadeOut);
                }
                else
                {
                    // 2단계: 같은 창문 방향에서 해가 낮게 떠올라 점점 높아지고 밝아짐
                    float s = Mathf.InverseLerp(moonFadeOut, 1f, Dawn);
                    var e = _moonRot.eulerAngles;
                    moonLight.transform.rotation = Quaternion.Euler(Mathf.Lerp(sunPitch.x, sunPitch.y, s), e.y, 0f);
                    moonLight.color = sunColor.Evaluate(s);
                    moonLight.intensity = sunIntensity * Mathf.SmoothStep(0f, 1f, s);
                }
            }

            if (ceilingLight != null)
            {
                ceilingLight.enabled = on;
                ceilingLight.intensity = ceilingIntensity;
            }

            Color offAmbient = Color.Lerp(ambientOffColor * ambientOffIntensity, ambientMorningColor, Mathf.SmoothStep(0f, 1f, Dawn));
            Color ambient = on ? ambientOnColor : offAmbient;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            // URP는 ambient probe(SH)를 읽으므로 직접 갱신해 준다.
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = sh;

            if (ceilingPanel != null)
            {
                ceilingPanel.GetPropertyBlock(_mpb);
                Color offPanel = Color.Lerp(panelOffColor, panelOffDayColor, Mathf.SmoothStep(0f, 1f, Dawn));
                _mpb.SetColor(BaseColorId, on ? panelOnColor : offPanel);
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
