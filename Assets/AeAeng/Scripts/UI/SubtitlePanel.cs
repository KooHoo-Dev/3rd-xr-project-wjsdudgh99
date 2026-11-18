using System.Collections;
using TMPro;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 시야 아래쪽에 부드럽게 따라오는 자막 (기획서 4.2). "모기가 사라졌습니다", 튜토리얼 안내 등을 2초간 표시.
    /// </summary>
    [RequireComponent(typeof(HeadLock))]
    public class SubtitlePanel : MonoBehaviour
    {
        public TextMeshPro text;
        public float showSeconds = 2f;
        public float fadeSeconds = 0.25f;

        Coroutine _routine;

        void Awake()
        {
            if (text == null) text = GetComponentInChildren<TextMeshPro>(true);
            OverlayText.Apply(text);
            SetAlpha(0f);
        }

        public void Show(string message) => Show(message, showSeconds);

        public void Show(string message, float seconds)
        {
            if (text == null || string.IsNullOrEmpty(message)) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(message, seconds));
        }

        IEnumerator Run(string message, float seconds)
        {
            text.text = message;
            for (float t = text.alpha; t < 1f; t += Time.unscaledDeltaTime / fadeSeconds) { SetAlpha(t); yield return null; }
            SetAlpha(1f);
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / fadeSeconds) { SetAlpha(t); yield return null; }
            SetAlpha(0f);
            _routine = null;
        }

        void SetAlpha(float a)
        {
            if (text == null) return;
            text.alpha = a;
            text.enabled = a > 0.001f;
        }
    }
}
