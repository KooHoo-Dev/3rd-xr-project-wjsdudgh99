using System;
using UnityEngine;
using UnityEngine.Events;

namespace AeAeng
{
    /// <summary>
    /// 한 판의 게임 시간. 현실 3분 동안 시계가 AM 1:00 → 7:00으로 흐른다 (게임 1시간 = 현실 30초, 게임 1분 = 현실 0.5초).
    /// 판이 진행 중일 때만 LedClock에 게임 시각을 넣고, 판이 아닐 때(메인 화면 등)는 LedClock이 실제 시각을 보여준다.
    /// Time.deltaTime을 쓰므로 timeScale = 0(메인 복귀 확인 창)이면 게임 시간도 멈춘다.
    /// </summary>
    public class RoundClock : MonoBehaviour
    {
        [Tooltip("비워 두면 씬의 모든 LedClock을 찾아서 씀")]
        public LedClock[] clocks;

        [Header("한 판")]
        [Range(0, 23)] public int startHour = 1;
        [Range(1, 24)] public int endHour = 7;
        [Tooltip("한 판의 현실 시간 (초). 기획: 180초")]
        public float roundSeconds = 180f;

        [Tooltip("플레이를 누르면 바로 판 시작 (GameManager가 생기기 전 테스트용)")]
        public bool startOnPlay = true;

        [Header("이벤트")]
        [Tooltip("아침 7시가 되었을 때 (제한 시간 끝 → 모기와 댄싱 파티)")]
        public UnityEvent onMorning;

        public bool IsRunning { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, roundSeconds - Elapsed);
        public float Progress01 => roundSeconds > 0f ? Mathf.Clamp01(Elapsed / roundSeconds) : 1f;

        /// <summary>지금 게임 속 시각 (오늘 날짜 + 새벽 시각)</summary>
        public DateTime GameTime =>
            DateTime.Today.AddHours(startHour).AddSeconds(Progress01 * (endHour - startHour) * 3600.0);

        void Awake()
        {
            if (clocks == null || clocks.Length == 0)
                clocks = FindObjectsByType<LedClock>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        void Start()
        {
            if (startOnPlay) StartRound();
        }

        /// <summary>새 판 시작: 시계를 AM 1:00으로 돌리고 흐르게 한다.</summary>
        public void StartRound()
        {
            Elapsed = 0f;
            IsRunning = true;
            Apply();
            Debug.Log($"[RoundClock] 판 시작 {startHour}:00 → {endHour}:00, {roundSeconds:0}초");
        }

        /// <summary>지금 게임 시각 (시간 단위, 예: 5.5 = 5시 30분)</summary>
        public float GameHour => startHour + Progress01 * (endHour - startHour);

        /// <summary>게임 시각을 바로 옮김 (테스트용: 에디터 인스펙터 버튼). 7시로 옮기면 바로 판이 끝남.</summary>
        public void JumpToHour(float hour)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(startHour, endHour, hour));
            Elapsed = t * roundSeconds;
            IsRunning = true; // 7시면 다음 Update 에서 onMorning
            Apply();
            Debug.Log($"[RoundClock] 시간 이동 → {GameTime:h:mm} (남은 {Remaining:0}초)");
        }

        /// <summary>판을 끝내고 시계를 실제 시각으로 되돌린다 (메인 화면 / 결과 화면).</summary>
        public void StopRound()
        {
            IsRunning = false;
            ShowRealTime();
        }

        public void ShowRealTime()
        {
            foreach (var c in clocks)
                if (c != null) c.useRealTime = true;
        }

        void Update()
        {
            if (!IsRunning) return;

            Elapsed += Time.deltaTime;
            if (Elapsed >= roundSeconds)
            {
                Elapsed = roundSeconds;
                IsRunning = false;
                Apply(); // 7:00에서 멈춤
                Debug.Log("[RoundClock] 아침 7시, 판 종료");
                onMorning?.Invoke();
                return;
            }
            Apply();
        }

        void Apply()
        {
            var t = GameTime;
            foreach (var c in clocks)
                if (c != null) c.SetTime(t);
        }
    }
}
