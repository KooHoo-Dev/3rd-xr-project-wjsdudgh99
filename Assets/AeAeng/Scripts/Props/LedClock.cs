using System;
using TMPro;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 7-세그먼트 LED 벽시계. 세그먼트마다 MeshRenderer가 하나씩 있고, 켜짐/꺼짐 재질만 바꿔 끼운다.
    /// 기본은 실제 현재 시각. 게임 시간(새벽 2시 → 아침 7시)을 보여줄 때는 SetTime()을 부르면 된다.
    /// 세그먼트 순서: a(위) b(오른쪽 위) c(오른쪽 아래) d(아래) e(왼쪽 아래) f(왼쪽 위) g(가운데)
    /// 모델은 WallClockBuilder(AeAeng > Build Wall Clock)가 만든다.
    /// </summary>
    public class LedClock : MonoBehaviour
    {
        [Serializable]
        public class Digit
        {
            public MeshRenderer[] segments = new MeshRenderer[7];
        }

        [Header("재질")]
        public Material onMaterial;
        public Material offMaterial;

        [Header("세그먼트 (빌더가 연결)")]
        public Digit[] hourDigits = new Digit[2];
        public Digit[] minuteDigits = new Digit[2];
        public Digit[] monthDigits = new Digit[2];
        public Digit[] dayDigits = new Digit[2];
        public MeshRenderer[] colonDots;
        public TextMeshPro ampmLabel;
        public TextMeshPro weekdayLabel;

        [Header("표시")]
        [Tooltip("켜면 기기의 현재 시각, 끄면 SetTime()으로 넣은 시각을 보여줌")]
        public bool useRealTime = true;
        public bool use24Hour = false;
        [Tooltip("가운데 : 를 1초에 한 번 깜빡임")]
        public bool blinkColon = true;

        // 비트 0..6 = a..g
        static readonly byte[] Patterns = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
        static readonly string[] Weekdays = { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };

        DateTime _manualTime = new DateTime(2026, 11, 17, 2, 0, 0);
        long _shownKey = -1;
        int _colonState = -1;

        /// <summary>게임 속 시각을 보여준다. 부르면 useRealTime이 꺼진다.</summary>
        public void SetTime(DateTime time)
        {
            useRealTime = false;
            _manualTime = time;
        }

        public DateTime CurrentTime => useRealTime ? DateTime.Now : _manualTime;

        void OnEnable()
        {
            _shownKey = -1;
            _colonState = -1;
        }

        void Update()
        {
            var t = CurrentTime;
            long key = t.Ticks / TimeSpan.TicksPerMinute;
            if (key != _shownKey)
            {
                _shownKey = key;
                Show(t);
            }

            // timeScale = 0(메인 복귀 창)에서도 깜빡이도록 unscaledTime 사용
            int colon = !blinkColon || Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? 1 : 0;
            if (colon != _colonState)
            {
                _colonState = colon;
                SetColon(colon == 1);
            }
        }

        /// <summary>주어진 시각으로 모든 세그먼트와 글씨를 바꾼다. 에디터(빌더)에서도 부를 수 있다.</summary>
        public void Show(DateTime t)
        {
            int hour = t.Hour;
            if (!use24Hour)
            {
                hour %= 12;
                if (hour == 0) hour = 12;
            }

            SetNumber(hourDigits, hour, !use24Hour);
            SetNumber(minuteDigits, t.Minute, false);
            SetNumber(monthDigits, t.Month, true);
            SetNumber(dayDigits, t.Day, false);

            if (ampmLabel != null)
            {
                ampmLabel.gameObject.SetActive(!use24Hour);
                ampmLabel.text = t.Hour < 12 ? "AM" : "PM";
            }
            if (weekdayLabel != null) weekdayLabel.text = Weekdays[(int)t.DayOfWeek];
            SetColon(true);
        }

        void SetNumber(Digit[] digits, int value, bool blankLeadingZero)
        {
            if (digits == null || digits.Length < 2) return;
            int tens = value / 10 % 10, ones = value % 10;
            SetDigit(digits[0], tens == 0 && blankLeadingZero ? -1 : tens);
            SetDigit(digits[1], ones);
        }

        void SetDigit(Digit d, int value)
        {
            if (d == null || d.segments == null) return;
            int bits = value < 0 ? 0 : Patterns[value];
            for (int i = 0; i < d.segments.Length && i < 7; i++)
            {
                var r = d.segments[i];
                if (r != null) r.sharedMaterial = (bits >> i & 1) != 0 ? onMaterial : offMaterial;
            }
        }

        void SetColon(bool on)
        {
            if (colonDots == null) return;
            foreach (var r in colonDots)
                if (r != null) r.sharedMaterial = on ? onMaterial : offMaterial;
        }
    }
}
