using UnityEngine;
using UnityEngine.InputSystem;

namespace AeAeng
{
    /// <summary>
    /// 오른손 맨손 잡기 (기획서: 빈손으로 트리거를 누르는 순간 손바닥 범위 안에 모기가 있으면 성공).
    /// 파리채를 들고 있거나 파리채 바로 옆(트리거 = 파리채 잡기)일 때는 맨손 잡기를 하지 않는다.
    /// 거리 구간(원거리는 맨손 불가)은 Mosquito.TryCatch 에서 판정.
    /// 오른손(HandPose) 루트에 붙인다.
    /// </summary>
    public class HandCatch : MonoBehaviour
    {
        [Tooltip("손바닥(주먹) 가운데. 비워 두면 자식 SwatterGrip 을 씀")]
        public Transform palm;
        [Tooltip("잡히는 범위 반지름 (m). 넉넉하게")]
        public float radius = 0.1f;
        [Tooltip("파리채가 이 거리 안에 있으면 트리거는 파리채 잡기로 씀")]
        public float swatterPriorityDistance = 0.2f;

        public SwatterGrab swatter;

        public System.Action<Mosquito> onCatch;

        InputAction _trigger;
        readonly Collider[] _hits = new Collider[16];

        void Awake()
        {
            if (palm == null) palm = transform.Find("SwatterGrip");
            if (palm == null) palm = transform;
            if (swatter == null) swatter = FindFirstObjectByType<SwatterGrab>();
        }

        void OnEnable()
        {
            _trigger = new InputAction("HandCatch", InputActionType.Button);
            _trigger.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            _trigger.AddBinding("<Keyboard>/k"); // 에디터 테스트 (HandPose 와 같은 키)
            _trigger.Enable();
        }

        void OnDisable()
        {
            _trigger?.Disable();
            _trigger?.Dispose();
        }

        void Update()
        {
            if (!_trigger.WasPressedThisFrame()) return;
            if (swatter != null)
            {
                if (swatter.IsHeld || NearSwatter()) return;
            }

            int n = Physics.OverlapSphereNonAlloc(palm.position, radius, _hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var m = _hits[i].GetComponentInParent<Mosquito>();
                if (m != null && m.TryCatch(Mosquito.Method.Hand))
                {
                    onCatch?.Invoke(m);
                    return; // 한 번에 한 마리
                }
            }
        }

        bool NearSwatter()
        {
            // 파리채 콜라이더 중 가장 가까운 점이 손바닥 가까이에 있으면 파리채 우선
            foreach (var c in swatter.GetComponentsInChildren<Collider>())
                if ((c.ClosestPoint(palm.position) - palm.position).sqrMagnitude < swatterPriorityDistance * swatterPriorityDistance)
                    return true;
            return false;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var p = palm != null ? palm : transform.Find("SwatterGrip");
            if (p == null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(p.position, radius);
        }
#endif
    }
}
