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

        [Header("딸깍 (11/18)")]
        [Tooltip("불을 켜고 끌 때 소리 (손에 든 스위치라 2D로 가깝게)")]
        public AudioClip clickClip;
        [Range(0f, 1f)] public float clickVolume = 0.7f;
        [Tooltip("소리 파일 앞 무음 건너뛰기 (light-switch-382712 는 앞 0.05초가 무음)")]
        public float clickStartTime = 0.05f;
        [Tooltip("딸깍할 때 왼손 진동 (세기, 초)")]
        [Range(0f, 1f)] public float clickHapticAmplitude = 0.35f;
        public float clickHapticDuration = 0.04f;

        InputAction _trigger;
        AudioSource _click;
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
                if (!_longFired && room != null)
                {
                    room.Toggle();
                    Click();
                }
            }
        }

        void Click()
        {
            if (clickClip != null)
            {
                if (_click == null)
                {
                    _click = gameObject.AddComponent<AudioSource>();
                    _click.playOnAwake = false;
                    _click.spatialBlend = 0f;
                }
                _click.clip = clickClip;
                _click.volume = clickVolume;
                _click.Play();
                if (clickStartTime < clickClip.length) _click.time = clickStartTime;
            }
            if (clickHapticAmplitude > 0f && clickHapticDuration > 0f)
            {
                var c = InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRControllerWithRumble>(CommonUsages.LeftHand);
                c?.SendImpulse(clickHapticAmplitude, clickHapticDuration);
            }
        }
    }
}
