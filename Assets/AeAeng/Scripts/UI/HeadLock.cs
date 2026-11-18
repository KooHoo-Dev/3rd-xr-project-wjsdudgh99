using Unity.XR.CoreUtils;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 머리(카메라) 앞 일정 거리에 부드럽게 따라오는 패널 (Lazy Follow).
    /// 고개를 돌리면 천천히 따라와서, 화면에 딱 붙은 HUD 보다 멀미가 적고 읽기 편하다.
    /// 높이는 고개를 숙이거나 들어도 눈높이 기준으로만 따라감 (yaw 만 따라감).
    /// </summary>
    public class HeadLock : MonoBehaviour
    {
        public Transform head;
        [Tooltip("머리 앞 거리 (m)")]
        public float distance = 1.2f;
        [Tooltip("눈높이에서 위(+)/아래(-) (m)")]
        public float height = -0.3f;
        [Tooltip("따라오는 빠르기 (클수록 빨리)")]
        public float followSpeed = 3f;
        [Tooltip("시선 방향과 이 각도 이상 벌어지면 따라오기 시작 (도)")]
        public float deadAngle = 20f;

        Vector3 _targetDir;
        bool _init;

        void Awake()
        {
            if (head == null)
            {
                var origin = FindFirstObjectByType<XROrigin>();
                if (origin != null && origin.Camera != null) head = origin.Camera.transform;
                else if (Camera.main != null) head = Camera.main.transform;
            }
        }

        void OnEnable() => _init = false;

        /// <summary>바로 정면으로 옮김 (패널을 새로 띄울 때)</summary>
        public void SnapToFront()
        {
            if (head == null) return;
            _targetDir = FlatForward();
            Place(1f);
            _init = true;
        }

        void LateUpdate()
        {
            if (head == null) return;
            if (!_init) { SnapToFront(); return; }

            var fwd = FlatForward();
            if (Vector3.Angle(fwd, _targetDir) > deadAngle) _targetDir = fwd;
            Place(1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
        }

        Vector3 FlatForward()
        {
            var f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        void Place(float k)
        {
            var target = head.position + _targetDir * distance + Vector3.up * height;
            transform.position = Vector3.Lerp(transform.position, target, k);
            // 글자가 머리를 바라보게 (TMP 는 transform.forward 방향에서 읽힘)
            var look = transform.position - head.position;
            look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
    }
}
