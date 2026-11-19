using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 모기를 잡았을 때 이펙트 (기획서 5.1): 잡은 자리에서 "딱!"(파리채) / "퍽!"(신문지) 글자가 톡 튀어나와
    /// 위로 떠오르며 사라지고, 작은 먼지 파티클이 퍼진다. 모기 자체는 Mosquito 가 납작해지거나 떨어짐.
    /// 글자와 파티클은 미리 몇 개 만들어 돌려 씀 (Quest 에서 생성/파괴 비용 없음).
    /// </summary>
    public class CatchEffects : MonoBehaviour
    {
        public MosquitoSwarm swarm;
        public TMP_FontAsset font;
        [Tooltip("파티클 재질 (URP Particles/Unlit, 동그란 텍스처). Setup 이 연결")]
        public Material particleMaterial;

        [Header("글자")]
        [Tooltip("TextMeshPro(3D) 크기: 10 = 1m")]
        public float textSize = 0.9f;
        public float riseHeight = 0.12f;
        public float textSeconds = 0.7f;
        public Color swatterColor = new Color(1f, 0.85f, 0.35f);
        public Color newspaperColor = new Color(1f, 0.6f, 0.35f);

        [Header("파티클")]
        public int burstCount = 14;
        public Color dustColor = new Color(0.85f, 0.85f, 0.8f, 0.9f);

        const int PoolSize = 4;
        readonly List<TextMeshPro> _texts = new List<TextMeshPro>();
        readonly List<ParticleSystem> _bursts = new List<ParticleSystem>();
        int _next;
        Transform _head;

        void Awake()
        {
            if (swarm == null) swarm = FindFirstObjectByType<MosquitoSwarm>();
            var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            _head = origin != null && origin.Camera != null ? origin.Camera.transform : (Camera.main != null ? Camera.main.transform : null);
            for (int i = 0; i < PoolSize; i++)
            {
                _texts.Add(MakeText(i));
                _bursts.Add(MakeBurst(i));
            }
        }

        void OnEnable()
        {
            if (swarm != null) swarm.onCaught.AddListener(OnCaught);
        }

        void OnDisable()
        {
            if (swarm != null) swarm.onCaught.RemoveListener(OnCaught);
        }

        void OnCaught(Mosquito m)
        {
            if (m == null) return;
            int i = _next;
            _next = (_next + 1) % PoolSize;

            bool paper = GameManager.Instance != null && GameManager.Instance.SelectedWeapon == Weapon.Newspaper;
            var pos = m.transform.position;
            var burst = _bursts[i];
            burst.transform.position = pos;
            burst.Play();
            StartCoroutine(PopText(_texts[i], pos, paper ? "퍽!" : "딱!", paper ? newspaperColor : swatterColor));
        }

        IEnumerator PopText(TextMeshPro t, Vector3 pos, string word, Color color)
        {
            t.text = word;
            t.color = color;
            t.gameObject.SetActive(true);
            // 머리 쪽으로 살짝 당겨서 벽에 파묻히지 않게
            var toHead = _head != null ? (_head.position - pos).normalized : Vector3.back;
            var start = pos + toHead * 0.06f + Vector3.up * 0.03f;
            for (float k = 0f; k < 1f; k += Time.deltaTime / textSeconds)
            {
                float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((k - 0.15f) / 0.15f));
                t.transform.position = start + Vector3.up * riseHeight * Mathf.SmoothStep(0f, 1f, k);
                t.transform.localScale = Vector3.one * pop;
                if (_head != null)
                {
                    var look = t.transform.position - _head.position;
                    if (look.sqrMagnitude > 1e-4f) t.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
                }
                t.alpha = k < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.6f) / 0.4f);
                yield return null;
            }
            t.gameObject.SetActive(false);
        }

        TextMeshPro MakeText(int i)
        {
            var go = new GameObject($"CatchText_{i}");
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.fontSize = textSize;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(0.4f, 0.15f);
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(30, 20, 10, 255);
            var r = t.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return t;
        }

        ParticleSystem MakeBurst(int i)
        {
            var go = new GameObject($"CatchBurst_{i}");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.016f);
            main.startColor = dustColor;
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            if (particleMaterial != null) r.sharedMaterial = particleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }
}
