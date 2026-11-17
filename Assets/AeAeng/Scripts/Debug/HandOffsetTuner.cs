using UnityEngine;
using UnityEngine.InputSystem;

namespace AeAeng
{
    /// <summary>
    /// (테스트용, 최종 빌드 전에 삭제) 헤드셋 안에서 컨트롤러 기준 손 위치/각도를 조이스틱으로 맞춘다.
    /// 양손에 좌우 대칭으로 같은 값을 적용하고, 바뀐 값을 로그로 남긴다 → adb logcat으로 읽어서 HandBuilder 기본값에 반영.
    ///   오른쪽 스틱 위/아래 : 손가락 각도(pitch)     키보드 I / O
    ///   오른쪽 스틱 좌/우   : 앞뒤 위치(z)           키보드 U / P
    ///   왼쪽 스틱 위/아래   : 위아래 위치(y)         키보드 ] / [
    ///   왼쪽 스틱 좌/우     : 손 비틀기(roll)
    /// 시작할 때 씬에 저장된 오른손 Transform 값에서 출발한다.
    /// </summary>
    public class HandOffsetTuner : MonoBehaviour
    {
        public float pitch = 45f;
        public float roll = 0f;
        public Vector3 wristOffset = new Vector3(0f, -0.035f, -0.11f);

        [Header("속도")]
        public float degreesPerSecond = 30f;
        public float metersPerSecond = 0.05f;

        HandPose[] _hands;
        InputAction _right, _left;
        float _logTimer;
        bool _dirty;

        void Start()
        {
            _hands = FindObjectsByType<HandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // 씬에 저장된 오른손 값으로 시작
            foreach (var h in _hands)
            {
                if (h.side != HandPose.Side.Right) continue;
                var t = h.transform;
                var e = t.localEulerAngles;
                pitch = Mathf.DeltaAngle(0f, e.x);
                roll = Mathf.DeltaAngle(0f, e.z) + 90f;
                wristOffset = t.localPosition;
            }
            Log();
        }

        void OnEnable()
        {
            _right = new InputAction("TunerRight", InputActionType.Value, expectedControlType: "Vector2");
            _right.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
            _right.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/i").With("Down", "<Keyboard>/o")
                .With("Left", "<Keyboard>/u").With("Right", "<Keyboard>/p");
            _left = new InputAction("TunerLeft", InputActionType.Value, expectedControlType: "Vector2");
            _left.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");
            _left.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/rightBracket").With("Down", "<Keyboard>/leftBracket");
            _right.Enable();
            _left.Enable();
        }

        void OnDisable()
        {
            _right?.Dispose();
            _left?.Dispose();
            _right = _left = null;
        }

        void Update()
        {
            if (_right == null || _hands == null) return;
            var r = DeadZone(_right.ReadValue<Vector2>());
            var l = DeadZone(_left.ReadValue<Vector2>());
            float dt = Time.unscaledDeltaTime;

            if (r != Vector2.zero || l != Vector2.zero)
            {
                pitch += r.y * degreesPerSecond * dt;
                wristOffset.z += r.x * metersPerSecond * dt;
                wristOffset.y += l.y * metersPerSecond * dt;
                roll += l.x * degreesPerSecond * dt;
                Apply();
                _dirty = true;
            }

            _logTimer -= dt;
            if (_dirty && _logTimer <= 0f)
            {
                _dirty = false;
                _logTimer = 0.5f;
                Log();
            }
        }

        void Apply()
        {
            foreach (var h in _hands)
            {
                float mirror = h.side == HandPose.Side.Right ? 1f : -1f;
                var t = h.transform;
                t.localPosition = new Vector3(wristOffset.x * mirror, wristOffset.y, wristOffset.z);
                t.localRotation = Quaternion.Euler(pitch, 0f, (-90f + roll) * mirror);
            }
        }

        void Log() =>
            Debug.Log($"[HandTuner] pitch {pitch:0.0} roll {roll:0.0} offset ({wristOffset.x:0.000}, {wristOffset.y:0.000}, {wristOffset.z:0.000})");

        static Vector2 DeadZone(Vector2 v) => v.magnitude < 0.2f ? Vector2.zero : v;
    }
}
