using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AeAeng
{
    /// <summary>
    /// 헤드셋 안에서 다시 빌드하지 않고 밝기를 조절하는 테스트용 도구. 최종 빌드에서는 오브젝트를 끄거나 지운다.
    ///  오른손 B (키보드 =) : 값 올리기
    ///  오른손 A (키보드 -) : 값 내리기
    ///  왼손 X   (키보드 N) : 조절할 항목 바꾸기 (Ambient → Moon → Ceiling)
    ///  왼손 Y   (키보드 H) : 안내 글자 숨기기 / 보이기 (녹화용)
    /// (키보드 키는 XR Interaction Simulator 조작 키와 겹치지 않게 골랐음)
    /// </summary>
    public class LightingTuner : MonoBehaviour
    {
        public RoomLightController room;
        public TextMeshPro label;

        enum Param { Ambient, Moon, Ceiling }
        Param _param = Param.Ambient;

        InputAction _up, _down, _next, _hide;

        void OnEnable()
        {
            _up = MakeAction("TunerUp", "<XRController>{RightHand}/{SecondaryButton}", "<Keyboard>/equals");
            _down = MakeAction("TunerDown", "<XRController>{RightHand}/{PrimaryButton}", "<Keyboard>/minus");
            _next = MakeAction("TunerNext", "<XRController>{LeftHand}/{PrimaryButton}", "<Keyboard>/n");
            _hide = MakeAction("TunerHide", "<XRController>{LeftHand}/{SecondaryButton}", "<Keyboard>/h");
            if (room != null) room.LightChanged += OnLightChanged;
        }

        void OnDisable()
        {
            foreach (var a in new[] { _up, _down, _next, _hide })
            {
                a?.Disable();
                a?.Dispose();
            }
            if (room != null) room.LightChanged -= OnLightChanged;
        }

        static InputAction MakeAction(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            a.Enable();
            return a;
        }

        void Start() => Refresh();

        void Update()
        {
            if (room == null) return;

            if (_next.WasPressedThisFrame())
            {
                _param = (Param)(((int)_param + 1) % 3);
                Refresh();
            }

            if (_up.WasPressedThisFrame()) Change(+1);
            if (_down.WasPressedThisFrame()) Change(-1);

            if (_hide.WasPressedThisFrame() && label != null)
                label.gameObject.SetActive(!label.gameObject.activeSelf);
        }

        void Change(int dir)
        {
            switch (_param)
            {
                case Param.Ambient:
                    room.ambientOffIntensity = Mathf.Clamp(room.ambientOffIntensity + dir * 0.25f, 0f, 4f);
                    break;
                case Param.Moon:
                    room.moonIntensity = Mathf.Clamp(room.moonIntensity + dir * 0.05f, 0f, 2f);
                    break;
                case Param.Ceiling:
                    room.ceilingIntensity = Mathf.Clamp(room.ceilingIntensity + dir * 0.2f, 0f, 6f);
                    break;
            }
            room.ApplyCurrent();
            Refresh();
            Debug.Log($"[LightingTuner] {Summary()}");
        }

        void OnLightChanged(bool on) => Refresh();

        string Summary() =>
            $"Light {(room.IsOn ? "ON" : "OFF")} | Ambient x{room.ambientOffIntensity:0.00} | Moon {room.moonIntensity:0.00} | Ceiling {room.ceilingIntensity:0.0}";

        void Refresh()
        {
            if (label == null || room == null) return;
            string Mark(Param p) => _param == p ? ">" : " ";
            label.text =
                $"LIGHT {(room.IsOn ? "ON" : "OFF")}\n" +
                $"{Mark(Param.Ambient)} Ambient x{room.ambientOffIntensity:0.00}\n" +
                $"{Mark(Param.Moon)} Moon {room.moonIntensity:0.00}\n" +
                $"{Mark(Param.Ceiling)} Ceiling {room.ceilingIntensity:0.0}\n" +
                "<size=60%>B/A: +/-  X: next  Y: hide  L-Trigger: light</size>";
        }
    }
}
