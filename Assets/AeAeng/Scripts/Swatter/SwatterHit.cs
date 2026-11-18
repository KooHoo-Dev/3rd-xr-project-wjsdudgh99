using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 파리채로 모기 치기 (기획서: 채 부분에 모기가 닿고, 휘두르는 속도가 1.5m/s 이상이면 성공).
    /// 손에 들고 있을 때만 매 프레임 채 머리 영역을 검사한다. 빠르게 휘둘러도 놓치지 않게
    /// 지난 프레임과 이번 프레임 사이 중간 지점도 함께 검사한다.
    /// 거리 구간(근거리는 파리채 불가)은 Mosquito.TryCatch 에서 판정.
    /// </summary>
    public class SwatterHit : MonoBehaviour
    {
        [Tooltip("채 머리 가운데 (VelocityTracker 가 붙어 있어야 함)")]
        public VelocityTracker head;
        [Tooltip("채 머리 판정 크기의 절반 (파리채 로컬, m). 실제 채보다 조금 넉넉하게")]
        public Vector3 halfExtents = new Vector3(0.07f, 0.08f, 0.035f);
        [Tooltip("이 속도(m/s) 이상으로 휘둘러야 잡힘")]
        public float minSpeed = 1.5f;

        public SwatterGrab grab;

        // 11/18 헤드셋 테스트: 날고 있는 모기도 휘두르면 무조건 잡혀서 너무 쉬움
        // → 앉은 모기(Landed/Alerted)는 확정, 날고 있는 모기는 이 확률로만 잡힘
        [Header("공중 판정")]
        [Tooltip("날고 있는 모기가 판정에 걸렸을 때 잡힐 확률 (0~1). 앉은 모기는 항상 잡힘")]
        [Range(0f, 1f)] public float airCatchChance = 0.35f;
        [Tooltip("공중에서 놓친 모기는 이 시간(초) 동안 다시 굴리지 않음 (한 번 휘두를 때 한 번만 판정)")]
        public float airRetryDelay = 0.5f;

        [Header("손맛 (무기마다 다르게)")]
        [Tooltip("휘두를 때 소리 (채 머리에 붙은 3D AudioSource, clip = Whip)")]
        public AudioSource swingAudio;
        [Tooltip("이 속도(m/s) 이상으로 휘두르면 휘두르는 소리")]
        public float swingSpeed = 2.0f;
        [Tooltip("휘두르는 소리 최소 간격 (초)")]
        public float swingCooldown = 0.35f;
        [Tooltip("소리 파일 앞의 무음 건너뛰기 (whip-snap 은 앞 0.24초가 무음)")]
        public float swingStartTime = 0.24f;
        [Tooltip("속도가 swingSpeed 의 이 비율 아래로 떨어져야 다음 휘두르는 소리가 날 수 있음")]
        [Range(0.2f, 0.9f)] public float rearmRatio = 0.5f;
        [Tooltip("모기를 잡았을 때 오른손 진동 세기 (0~1)")]
        [Range(0f, 1f)] public float hitHapticAmplitude = 0.6f;
        [Tooltip("모기를 잡았을 때 오른손 진동 길이 (초)")]
        public float hitHapticDuration = 0.08f;

        /// <summary>모기를 잡았을 때 (효과음/이펙트용)</summary>
        public System.Action<Mosquito> onHit;

        readonly Collider[] _hits = new Collider[16];
        Vector3 _lastPos;
        bool _has;
        float _lastSwing = -10f;
        bool _swingArmed = true;

        void Awake()
        {
            if (grab == null) grab = GetComponent<SwatterGrab>();
        }

        void LateUpdate()
        {
            if (head == null || grab == null || !grab.IsHeld)
            {
                _has = false;
                return;
            }

            var pos = head.transform.position;
            // 11/18: 그냥 움직여도 "휙"이 계속 나서, 한 번 빨라졌으면 충분히 느려진 뒤에만 다시 소리 (한 번 휘두름 = 한 번)
            if (head.Speed < swingSpeed * rearmRatio) _swingArmed = true;
            if (swingAudio != null && _swingArmed && head.Speed >= swingSpeed && Time.time - _lastSwing >= swingCooldown)
            {
                _swingArmed = false;
                _lastSwing = Time.time;
                swingAudio.Play();
                if (swingAudio.clip != null && swingStartTime < swingAudio.clip.length) swingAudio.time = swingStartTime;
            }
            if (head.Speed >= minSpeed)
            {
                Check(pos);
                if (_has) Check((pos + _lastPos) * 0.5f);
            }
            _lastPos = pos;
            _has = true;
        }

        void Check(Vector3 center)
        {
            var scale = transform.lossyScale;
            var ext = Vector3.Scale(halfExtents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int n = Physics.OverlapBoxNonAlloc(center, ext, _hits, head.transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var m = _hits[i].GetComponentInParent<Mosquito>();
                if (m == null || !m.IsCatchable) continue;
                if (!RollAirCatch(m)) continue;
                if (m.TryCatch(Mosquito.Method.Swatter))
                {
                    RightHandHaptic(hitHapticAmplitude, hitHapticDuration);
                    onHit?.Invoke(m);
                }
            }
        }

        // 앉은 모기는 항상 true. 날고 있으면 한 번 휘두를 때 한 번만 확률을 굴림
        readonly System.Collections.Generic.Dictionary<Mosquito, float> _airMissUntil =
            new System.Collections.Generic.Dictionary<Mosquito, float>();

        bool RollAirCatch(Mosquito m)
        {
            var s = m.Current;
            if (s == Mosquito.State.Landed || s == Mosquito.State.Alerted) return true;
            if (_airMissUntil.TryGetValue(m, out float until) && Time.time < until) return false;
            if (Random.value < airCatchChance) return true;
            _airMissUntil[m] = Time.time + airRetryDelay;
            Debug.Log($"[SwatterHit] 공중에서 아깝게 놓침 ({name}, 확률 {airCatchChance:0.##})");
            return false;
        }

        /// <summary>오른손 컨트롤러 진동 (OpenXR 컨트롤러는 XRControllerWithRumble)</summary>
        public static void RightHandHaptic(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f) return;
            var c = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRControllerWithRumble>(
                UnityEngine.InputSystem.CommonUsages.RightHand);
            c?.SendImpulse(amplitude, duration);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (head == null) return;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.matrix = Matrix4x4.TRS(head.transform.position, head.transform.rotation, Vector3.one);
            var s = transform.lossyScale;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.Scale(halfExtents * 2f, s));
        }
#endif
    }
}
