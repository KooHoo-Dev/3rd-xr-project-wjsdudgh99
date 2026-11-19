using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AeAeng
{
    /// <summary>
    /// 시선 아래쪽에 톡 튀어나오는 알림 (토스트). 11/18: 예전 녹색 "LIGHT OFF" 자리.
    ///  - 잡았을 때: 노란 테두리 + 체크, "2마리 잡았다!" + 작게 "남은 모기 1"
    ///  - 도망갔을 때: 파란 테두리 + 바람, "모기 1마리가 사라졌다!" (0.6초 안에 여러 마리면 묶어서 한 번)
    ///  - 그 밖(튜토리얼 등): 회색 테두리, 글자만
    /// 반투명 어두운 알약 바탕이라 밝은 방 / 어두운 방 모두에서 읽힘. HeadLock 으로 부드럽게 따라옴.
    /// 등장 0.15초(살짝 커졌다 제자리) → 유지 → 0.3초 사라짐. 새 알림은 이전 것을 바로 바꿈.
    /// </summary>
    [RequireComponent(typeof(HeadLock))]
    public class SubtitlePanel : MonoBehaviour
    {
        public enum Style { Info, Catch, Escape }

        [Header("구성 (Setup Play Loop 가 연결)")]
        public CanvasGroup group;
        public RectTransform pill;
        public Image ring;
        public Image icon;
        public TMP_Text mainText;
        public TMP_Text subText;
        public Sprite checkIcon;
        public Sprite windIcon;

        [Header("시간 (초)")]
        public float popSeconds = 0.15f;
        public float holdSeconds = 1.5f;
        public float fadeSeconds = 0.3f;
        [Tooltip("이 시간 안에 여러 마리가 도망가면 한 번에 묶어서 알림")]
        public float escapeMergeSeconds = 0.6f;
        [Tooltip("큰 글 + 작은 글 글자 수가 이보다 많으면 작은 글을 아랫줄로 내림 (11/19 헤드셋: 튜토리얼 안내가 한 줄로 길어 화면 밖으로 잘림)")]
        public int stackOverChars = 28;

        static readonly Color CatchRing = new Color32(0xEF, 0x9F, 0x27, 0xFF);
        static readonly Color CatchIcon = new Color32(0xFA, 0xC7, 0x75, 0xFF);
        static readonly Color CatchText = new Color32(0xFA, 0xEE, 0xDA, 0xFF);
        static readonly Color EscapeRing = new Color32(0x85, 0xB7, 0xEB, 0xFF);
        static readonly Color EscapeIcon = new Color32(0xB5, 0xD4, 0xF4, 0xFF);
        static readonly Color EscapeText = new Color32(0xE6, 0xF1, 0xFB, 0xFF);
        static readonly Color InfoRing = new Color(0.75f, 0.78f, 0.85f, 0.9f);
        static readonly Color InfoText = new Color(0.95f, 0.96f, 1f);

        HeadLock _lock;
        Coroutine _show, _escapeWait;
        int _pendingEscapes;
        TextAlignmentOptions _mainAlign;
        float _subRatio = 0.63f;

        void Awake()
        {
            _lock = GetComponent<HeadLock>();
            if (mainText != null)
            {
                _mainAlign = mainText.alignment;
                if (subText != null && mainText.fontSize > 0f) _subRatio = subText.fontSize / mainText.fontSize;
            }
            if (group != null) group.alpha = 0f;
        }

        // ------------------------------------------------------------------
        // 알림 종류
        // ------------------------------------------------------------------

        /// <summary>일반 안내 (튜토리얼 등)</summary>
        public void Show(string message) => Show(message, null, Style.Info);

        /// <summary>잡았을 때: caught = 이번 판에서 지금까지 잡은 수</summary>
        public void ShowCatch(int caught, int remaining) =>
            Show($"{caught}마리 잡았다!", remaining > 0 ? $"남은 모기 {remaining}" : null, Style.Catch);

        /// <summary>모기가 도망감. 짧은 시간에 여러 번 오면 모아서 "모기 N마리가 사라졌다!" 한 번</summary>
        public void ReportEscape()
        {
            _pendingEscapes++;
            if (_escapeWait == null) _escapeWait = StartCoroutine(EscapeAfterMerge());
        }

        IEnumerator EscapeAfterMerge()
        {
            yield return new WaitForSecondsRealtime(escapeMergeSeconds);
            int n = _pendingEscapes;
            _pendingEscapes = 0;
            _escapeWait = null;
            if (n > 0) Show($"모기 {n}마리가 사라졌다!", null, Style.Escape);
        }

        public void Show(string main, string sub, Style style) => Show(main, sub, style, holdSeconds);

        /// <summary>hold = 머무는 시간. float.PositiveInfinity 면 Hide() 나 다음 알림까지 계속 보임 (튜토리얼 안내)</summary>
        public void Show(string main, string sub, Style style, float hold)
        {
            if (mainText == null || string.IsNullOrEmpty(main)) return;
            Apply(main, sub, style);
            if (_show != null) StopCoroutine(_show);
            _show = StartCoroutine(Run(hold));
        }

        /// <summary>계속 보이는 안내 (튜토리얼 단계)</summary>
        public void ShowSticky(string main, string sub = null) => Show(main, sub, Style.Info, float.PositiveInfinity);

        /// <summary>지금 알림을 부드럽게 숨김</summary>
        public void Hide()
        {
            if (_show != null) StopCoroutine(_show);
            _show = StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            float from = group != null ? group.alpha : 0f;
            for (float t = from; t > 0f; t -= Time.unscaledDeltaTime / fadeSeconds)
            {
                if (group != null) group.alpha = t;
                yield return null;
            }
            if (group != null) group.alpha = 0f;
            _show = null;
        }

        void Apply(string main, string sub, Style style)
        {
            Color ringC, textC, iconC;
            Sprite iconS;
            switch (style)
            {
                case Style.Catch: ringC = CatchRing; textC = CatchText; iconC = CatchIcon; iconS = checkIcon; break;
                case Style.Escape: ringC = EscapeRing; textC = EscapeText; iconC = EscapeIcon; iconS = windIcon; break;
                default: ringC = InfoRing; textC = InfoText; iconC = Color.white; iconS = null; break;
            }

            bool hasSub = !string.IsNullOrEmpty(sub);
            // 길면 두 줄: 큰 글 아래에 작은 글 (가운데 정렬). 짧으면 예전처럼 한 줄에 나란히
            bool stacked = hasSub && main.Length + sub.Length > stackOverChars;
            if (stacked)
            {
                string hex = ColorUtility.ToHtmlStringRGB(ringC);
                int pct = Mathf.RoundToInt(_subRatio * 100f);
                mainText.text = $"{main}\n<size={pct}%><color=#{hex}>{sub}</color></size>";
                mainText.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                mainText.text = main;
                mainText.alignment = _mainAlign;
            }
            mainText.color = textC;
            if (subText != null)
            {
                subText.gameObject.SetActive(hasSub && !stacked);
                subText.text = stacked ? "" : sub;
                subText.color = ringC;
            }
            if (ring != null) ring.color = ringC;
            if (icon != null)
            {
                icon.gameObject.SetActive(iconS != null);
                icon.sprite = iconS;
                icon.color = iconC;
            }
            if (pill != null) LayoutRebuilder.ForceRebuildLayoutImmediate(pill);
        }

        IEnumerator Run(float hold)
        {
            _lock.SnapToFront();

            // 톡 튀어나옴: 0.85 → 1.05 → 1.0
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / popSeconds)
            {
                SetPop(t);
                yield return null;
            }
            SetPop(1f);

            if (float.IsPositiveInfinity(hold)) { _show = null; yield break; } // 계속 보임
            yield return new WaitForSecondsRealtime(hold);

            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / fadeSeconds)
            {
                if (group != null) group.alpha = t;
                yield return null;
            }
            if (group != null) group.alpha = 0f;
            _show = null;
        }

        void SetPop(float t)
        {
            if (group != null) group.alpha = Mathf.Clamp01(t * 1.5f);
            if (pill != null)
            {
                float s = t < 0.7f ? Mathf.Lerp(0.85f, 1.05f, t / 0.7f) : Mathf.Lerp(1.05f, 1f, (t - 0.7f) / 0.3f);
                pill.localScale = Vector3.one * s;
            }
        }
    }
}
