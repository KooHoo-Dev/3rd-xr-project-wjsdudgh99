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

        /// <summary>마지막으로 잡은 시각 (효과음/햅틱용)</summary>
        public System.Action<Mosquito> onHit;

        readonly Collider[] _hits = new Collider[16];
        Vector3 _lastPos;
        bool _has;

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
                if (m != null && m.TryCatch(Mosquito.Method.Swatter)) onHit?.Invoke(m);
            }
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
