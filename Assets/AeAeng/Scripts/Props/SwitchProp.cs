using UnityEngine;
using UnityEngine.InputSystem;

namespace AeAeng
{
    /// <summary>
    /// 왼손에 늘 쥐고 있는 무선 스위치 소품 (기획서 2.3 왼손 스위치).
    /// - 왼손 트리거를 당기는 만큼 버튼이 눌려 들어감
    /// - 작은 LED: 방 불이 켜져 있으면 초록, 꺼져 있으면 아주 어두운 초록 (어두운 방에서 손 위치 단서)
    /// </summary>
    public class SwitchProp : MonoBehaviour
    {
        public Transform button;
        [Tooltip("버튼이 눌리는 깊이 (m)")]
        public float pressDepth = 0.003f;
        [Tooltip("버튼이 눌려 들어가는 방향 (소품 로컬). 버튼이 앞면(+Z)에 있으므로 -Z")]
        public Vector3 pressDirection = Vector3.back;
        public Renderer led;
        public Color ledOn = new Color(0.35f, 1f, 0.45f);
        public Color ledOff = new Color(0.04f, 0.12f, 0.05f);
        public RoomLightController room;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        InputAction _trigger;
        Vector3 _buttonRest;
        MaterialPropertyBlock _mpb;
        bool _lastOn;
        bool _init;

        void Awake()
        {
            if (button != null) _buttonRest = button.localPosition;
            _mpb = new MaterialPropertyBlock();
            if (room == null) room = FindFirstObjectByType<RoomLightController>();
        }

        void OnEnable()
        {
            _trigger = new InputAction("SwitchPropTrigger", InputActionType.Value);
            _trigger.AddBinding("<XRController>{LeftHand}/trigger");
            _trigger.Enable();
        }

        void OnDisable()
        {
            _trigger?.Disable();
            _trigger?.Dispose();
        }

        void Update()
        {
            if (button != null)
            {
                float v = _trigger != null ? Mathf.Clamp01(_trigger.ReadValue<float>()) : 0f;
                // 트리거 끝까지가 아니라 60% 쯤에서 다 눌림 (딸깍 느낌)
                button.localPosition = _buttonRest + pressDirection.normalized * pressDepth * Mathf.Clamp01(v / 0.6f);
            }

            bool on = room != null && room.IsOn;
            if (led != null && (!_init || on != _lastOn))
            {
                led.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, on ? ledOn : ledOff);
                led.SetPropertyBlock(_mpb);
                _lastOn = on;
                _init = true;
            }
        }
    }
}
