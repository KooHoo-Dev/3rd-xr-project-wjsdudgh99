using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace AeAeng
{
    /// <summary>
    /// 왼손 스위치 입력.
    /// - 트리거를 1초 미만으로 눌렀다 "뗄 때" 불 On / Off
    /// - 1초 이상 누르고 있으면 onLongPress (메인 복귀 확인 창 등), 이때는 불 전환 안 함
    /// 에디터 테스트용으로 키보드 L 키도 같은 동작을 한다.
    /// </summary>
    public class LightSwitchInput : MonoBehaviour
    {
        public RoomLightController room;
        public float longPressSeconds = 1f;
        public UnityEvent onLongPress;

        InputAction _trigger;
        float _pressStart;
        bool _pressing;
        bool _longFired;

        void OnEnable()
        {
            _trigger = new InputAction("LightSwitch", InputActionType.Button);
            _trigger.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            _trigger.AddBinding("<Keyboard>/l");
            _trigger.Enable();
        }

        void OnDisable()
        {
            _trigger?.Disable();
            _trigger?.Dispose();
            _trigger = null;
            _pressing = false;
        }

        void Update()
        {
            if (_trigger == null) return;

            if (_trigger.WasPressedThisFrame())
            {
                _pressing = true;
                _longFired = false;
                _pressStart = Time.unscaledTime; // timeScale 0 이어도 동작
            }

            if (_pressing && !_longFired && Time.unscaledTime - _pressStart >= longPressSeconds)
            {
                _longFired = true;
                Debug.Log("[LightSwitch] Long press");
                onLongPress?.Invoke();
            }

            if (_pressing && _trigger.WasReleasedThisFrame())
            {
                _pressing = false;
                if (!_longFired && room != null) room.Toggle();
            }
        }
    }
}
