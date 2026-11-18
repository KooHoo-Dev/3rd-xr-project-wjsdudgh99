using System.Collections;
using TMPro;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 판이 끝날 때 정면에 크게 띄우는 문구 ("꿈나라 파티!" / "모기와 댄싱 파티!").
    /// HeadLock 으로 정면에 붙고, 화면 페이드(검은 화면) 위에도 보이도록 글자 재질을 Overlay 다음 순서로 그린다.
    /// </summary>
    [RequireComponent(typeof(HeadLock))]
    public class PartyBanner : MonoBehaviour
    {
        public TextMeshPro text;
        public float fadeSeconds = 0.3f;

        HeadLock _lock;
        Coroutine _routine;

        void Awake()
        {
            _lock = GetComponent<HeadLock>();
            if (text == null) text = GetComponentInChildren<TextMeshPro>(true);
            OverlayText.Apply(text);
            SetAlpha(0f);
        }

        public void Show(string message, float seconds)
        {
            if (text == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(message, seconds));
        }

        IEnumerator Run(string message, float seconds)
        {
            text.text = message;
            _lock.SnapToFront();
            for (float t = 0f; t < 1f; t += Time.deltaTime / fadeSeconds) { SetAlpha(t); yield return null; }
            SetAlpha(1f);
            yield return new WaitForSeconds(Mathf.Max(0f, seconds - fadeSeconds * 2f));
            for (float t = 1f; t > 0f; t -= Time.deltaTime / fadeSeconds) { SetAlpha(t); yield return null; }
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

    /// <summary>TMP 글자를 다른 모든 것(페이드 포함) 위에 그리기</summary>
    public static class OverlayText
    {
        public static void Apply(TMP_Text t, int queue = 4001)
        {
            if (t == null) return;
            var mat = t.fontMaterial; // 이 글자 전용 재질 인스턴스
            mat.renderQueue = queue;
            if (mat.HasProperty("_ZTestMode")) mat.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
        }
    }
}
