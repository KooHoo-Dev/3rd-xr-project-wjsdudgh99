using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace AeAeng
{
    /// <summary>
    /// 움직이지 않는 플레이어 (침대에 앉아서 플레이). XR Origin 루트에 붙인다.
    /// 1) CharacterController / XRBodyTransformer 를 꺼서, 몸 캡슐이 침대 콜라이더에 밀려
    ///    시점이 서서히 위로 올라가는 문제를 막는다.
    /// 2) 시작할 때(그리고 Meta 버튼 길게 눌러 재중심할 때) 실제로 앉은 위치와 바라보는 방향을
    ///    게임 속 자리(seatPosition, seatYaw)에 맞춘다. 실제 방이 좁거나 가디언 가운데에 앉지 않아도
    ///    항상 침대 위에서 +Z(침대 발치)를 보며 시작한다. 높이는 실제 머리 높이 그대로.
    /// </summary>
    [RequireComponent(typeof(XROrigin))]
    public class StationaryPlayer : MonoBehaviour
    {
        [Tooltip("머리가 올 게임 속 위치 (x, z만 사용, m)")]
        public Vector3 seatPosition = Vector3.zero;
        [Tooltip("시작할 때 바라볼 방향 (도). 0 = +Z (침대 발치)")]
        public float seatYaw = 0f;
        [Tooltip("시작할 때 자동으로 자리 맞추기")]
        public bool recenterOnStart = true;

        XROrigin _origin;
        bool _pending;
        float _startTime;
        readonly List<XRInputSubsystem> _subsystems = new List<XRInputSubsystem>();

        void Awake()
        {
            _origin = GetComponent<XROrigin>();
            foreach (var cc in GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;
            foreach (var body in GetComponentsInChildren<XRBodyTransformer>(true)) body.enabled = false;
            _pending = recenterOnStart;
            _startTime = Time.unscaledTime;
        }

        void OnEnable()
        {
            SubsystemManager.GetSubsystems(_subsystems);
            foreach (var s in _subsystems) s.trackingOriginUpdated += OnTrackingOriginUpdated;
        }

        void OnDisable()
        {
            foreach (var s in _subsystems) s.trackingOriginUpdated -= OnTrackingOriginUpdated;
            _subsystems.Clear();
        }

        // Meta 버튼 길게 눌러 재중심하면 호출됨 → 다시 자리 맞춤
        void OnTrackingOriginUpdated(XRInputSubsystem _) => _pending = true;

        void LateUpdate()
        {
            if (!_pending || _origin == null || _origin.Camera == null) return;
            // 헤드셋 추적이 시작되기 전(카메라가 원점)에는 기다림
            var cam = _origin.Camera.transform;
            if (cam.localPosition.sqrMagnitude < 0.0001f && Time.unscaledTime - _startTime < 3f) return;
            Recenter();
            _pending = false;
        }

        /// <summary>지금 머리 위치/방향을 게임 속 자리로 맞춤</summary>
        public void Recenter()
        {
            var cam = _origin.Camera.transform;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude > 1e-6f)
            {
                float camYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                _origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(camYaw, seatYaw));
            }
            var p = cam.position;
            _origin.MoveCameraToWorldLocation(new Vector3(seatPosition.x, p.y, seatPosition.z));
            Debug.Log($"[StationaryPlayer] 자리 맞춤: 머리 ({seatPosition.x:0.00}, {p.y:0.00}, {seatPosition.z:0.00}), 방향 {seatYaw:0}°");
        }
    }
}
