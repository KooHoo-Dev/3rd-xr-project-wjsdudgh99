using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 무기를 휘두를 때 채 끝에 남는 바람 궤적 (기획서 5.1 헛스윙 표시). 손에 들고 빠르게 움직일 때만 그림.
    /// 무기 루트에 붙이면 SwatterHit 의 채 머리(HeadCenter)에 TrailRenderer 를 단다.
    /// + 무기를 잡을 때 / 놓을 때 오른손 약한 진동 (기획서 5.3)
    /// </summary>
    [RequireComponent(typeof(SwatterGrab))]
    public class SwingTrail : MonoBehaviour
    {
        [Tooltip("궤적 재질 (URP Particles/Unlit 반투명). Setup 이 연결")]
        public Material trailMaterial;
        [Tooltip("이 속도(m/s) 이상일 때만 궤적을 그림. 0 이면 SwatterHit.swingSpeed 의 70%")]
        public float minSpeed = 0f;
        public float trailSeconds = 0.12f;
        public float width = 0.05f;
        public Color color = new Color(0.85f, 0.92f, 1f, 0.35f);

        [Header("진동 (오른손)")]
        public float grabHaptic = 0.25f;
        public float releaseHaptic = 0.12f;
        public float hapticSeconds = 0.04f;

        SwatterGrab _grab;
        SwatterHit _hit;
        TrailRenderer _trail;
        bool _wasHeld;

        void Awake()
        {
            _grab = GetComponent<SwatterGrab>();
            _hit = GetComponent<SwatterHit>();
            var head = _hit != null && _hit.head != null ? _hit.head.transform : transform;
            _trail = head.GetComponent<TrailRenderer>();
            if (_trail == null) _trail = head.gameObject.AddComponent<TrailRenderer>();
            _trail.time = trailSeconds;
            _trail.minVertexDistance = 0.01f;
            _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            _trail.widthMultiplier = width * Mathf.Abs(transform.lossyScale.x);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                      new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = g;
            if (trailMaterial != null) _trail.sharedMaterial = trailMaterial;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _trail.emitting = false;
        }

        void Update()
        {
            bool held = _grab != null && _grab.IsHeld;
            if (held != _wasHeld)
            {
                SwatterHit.RightHandHaptic(held ? grabHaptic : releaseHaptic, hapticSeconds);
                _wasHeld = held;
                if (!held) _trail.Clear();
            }

            float threshold = minSpeed > 0f ? minSpeed : (_hit != null ? _hit.swingSpeed * 0.7f : 2f);
            float speed = _hit != null && _hit.head != null ? _hit.head.Speed : 0f;
            _trail.emitting = held && speed >= threshold;
        }
    }
}
