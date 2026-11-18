using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AeAeng
{
    /// <summary>
    /// 결과 화면 (기획서 4.3): 이번 플레이의 누적 기록(꿈나라 파티 N번 / 모기와 댄싱 파티 N번).
    /// 3초가 지나면 "아무 버튼이나 눌러 메인으로"가 깜빡이고, 그때부터 아무 버튼이나 누르면 메인으로.
    /// (3초 대기: 기록을 보기 전에 실수로 넘어가는 것 방지)
    /// </summary>
    public class ResultController : MonoBehaviour
    {
        public TMP_Text dreamText;
        public TMP_Text danceText;
        public TMP_Text hintText;
        public ScreenFade fade;
        public float waitSeconds = 3f;

        InputAction _any;
        bool _ready;

        void Awake()
        {
            if (fade == null) fade = FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            _any = new InputAction("AnyButton", InputActionType.Button);
            foreach (var b in new[]
                     {
                         "<XRController>/{PrimaryButton}", "<XRController>/{SecondaryButton}",
                         "<XRController>/{TriggerButton}", "<XRController>/{GripButton}",
                         "<XRController>/{Primary2DAxisClick}", "<Keyboard>/anyKey",
                     })
                _any.AddBinding(b);
            _any.Enable();
        }

        void OnDisable()
        {
            _any?.Disable();
            _any?.Dispose();
        }

        IEnumerator Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            var gm = GameManager.Get();
            if (dreamText != null) dreamText.text = $"{GameManager.DreamShort}  <b>{gm.DreamParties}</b>번";
            if (danceText != null) danceText.text = $"{GameManager.DanceShort}  <b>{gm.DanceParties}</b>번";
            if (hintText != null) hintText.alpha = 0f;

            if (fade != null)
            {
                fade.SetAlpha(1f);
                yield return null;
                yield return fade.FadeTo(0f, 0.8f);
            }
            yield return new WaitForSeconds(waitSeconds);
            _ready = true;
        }

        void Update()
        {
            if (!_ready) return;
            if (hintText != null) hintText.alpha = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.time * 4f)); // 깜빡임
            if (_any.WasPressedThisFrame())
            {
                _ready = false;
                GameFlow.LoadMenu();
            }
        }
    }
}
