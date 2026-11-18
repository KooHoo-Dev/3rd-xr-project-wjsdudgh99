using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace AeAeng
{
    /// <summary>
    /// 한 판의 진행 (기획서 3.1, 3.2). 플레이 씬에 하나.
    ///  판 시작: 불 끄기, 시계 AM 1:00, 모기 N마리 등장
    ///  꿈나라 파티: 모기를 모두 잡음 → 화면이 서서히 어두워지며 잠듦 + "꿈나라 파티!"
    ///  모기와 댄싱 파티: 7시가 됨 → 남은 모기가 머리 주변을 맴돎 + "모기와 댄싱 파티!" (알람은 효과음 묶음에서)
    ///  → 파티 횟수 기록(GameManager) → 잠깐 어두워졌다가 다음 판 자동 시작
    /// </summary>
    public class RoundController : MonoBehaviour
    {
        public enum Phase { Idle, Playing, Party }

        [Header("참조 (비워 두면 자동으로 찾음)")]
        public RoundClock clock;
        public MosquitoSwarm swarm;
        public RoomLightController room;
        public ScreenFade fade;
        public PartyBanner banner;
        public SubtitlePanel subtitles;

        [Header("판")]
        // 11/18 헤드셋 테스트: 3마리는 파리채 약 35초, 신문지 약 5초 만에 끝남 → 판(3분)이 너무 짧음
        // → 첫 판 5마리, 판마다 1마리씩 늘려 최대 8마리 (판이 1~2분 걸리고, 늦으면 7시 댄싱 파티도 나오게)
        [Tooltip("첫 판의 모기 수")]
        public int firstRoundMosquitoes = 5;
        [Tooltip("다음 판마다 늘어나는 모기 수")]
        public int addPerRound = 1;
        [Tooltip("한 판의 최대 모기 수 (소리가 너무 겹치지 않게)")]
        public int maxMosquitoes = 8;

        /// <summary>이번 판의 모기 수 (RoundNumber 는 1부터)</summary>
        public int MosquitoesThisRound =>
            Mathf.Clamp(firstRoundMosquitoes + addPerRound * (RoundNumber - 1), 1, Mathf.Max(1, maxMosquitoes));
        [Tooltip("플레이를 누르면 바로 첫 판 시작")]
        public bool startOnPlay = true;

        [Header("파티 연출 (초)")]
        public float bannerSeconds = 2f;
        [Tooltip("꿈나라 파티: 잠드는(어두워지는) 시간")]
        public float sleepFadeSeconds = 2.5f;
        [Tooltip("판과 판 사이에 화면이 어두워지는 시간")]
        public float betweenFadeSeconds = 0.6f;
        [Tooltip("새 판이 밝아지는 시간")]
        public float wakeFadeSeconds = 1.2f;

        [Header("이벤트 (효과음/햅틱 묶음에서 연결)")]
        public UnityEvent onRoundStart;
        public UnityEvent<PartyType> onParty;

        public Phase Current { get; private set; } = Phase.Idle;
        public int RoundNumber { get; private set; }

        void Awake()
        {
            if (clock == null) clock = FindFirstObjectByType<RoundClock>();
            if (swarm == null) swarm = FindFirstObjectByType<MosquitoSwarm>();
            if (room == null) room = FindFirstObjectByType<RoomLightController>();
            if (fade == null) fade = FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
            if (banner == null) banner = FindFirstObjectByType<PartyBanner>(FindObjectsInactive.Include);
            if (subtitles == null) subtitles = FindFirstObjectByType<SubtitlePanel>(FindObjectsInactive.Include);
            GameManager.Get();
        }

        void OnEnable()
        {
            if (swarm != null)
            {
                swarm.onAllCaught.AddListener(OnAllCaught);
                swarm.onMessage.AddListener(ShowSubtitle);
                swarm.onCaught.AddListener(OnCaught);
                swarm.onEscaped.AddListener(OnEscaped);
            }
            if (clock != null) clock.onMorning.AddListener(OnMorning);
        }

        void OnDisable()
        {
            if (swarm != null)
            {
                swarm.onAllCaught.RemoveListener(OnAllCaught);
                swarm.onMessage.RemoveListener(ShowSubtitle);
                swarm.onCaught.RemoveListener(OnCaught);
                swarm.onEscaped.RemoveListener(OnEscaped);
            }
            if (clock != null) clock.onMorning.RemoveListener(OnMorning);
        }

        void Start()
        {
            if (startOnPlay) StartCoroutine(FirstRound());
        }

        IEnumerator FirstRound()
        {
            if (fade != null) fade.SetAlpha(1f);
            yield return null; // 헤드셋 자리 맞춤(StationaryPlayer)이 끝난 뒤 밝아지게
            BeginRound();
            if (fade != null) yield return fade.FadeTo(0f, wakeFadeSeconds);
        }

        /// <summary>새 판: 밤, 불 끔, 시계 1:00, 모기 등장</summary>
        public void BeginRound()
        {
            RoundNumber++;
            if (room != null) room.SetLight(false, false);
            if (clock != null) clock.StartRound();
            int n = MosquitoesThisRound;
            if (swarm != null) swarm.SpawnRound(n);
            Current = Phase.Playing;
            onRoundStart?.Invoke();
            Debug.Log($"[RoundController] {RoundNumber}번째 판 시작 (모기 {n}마리, 무기 {GameManager.Get().SelectedWeapon})");
        }

        void OnAllCaught()
        {
            if (Current == Phase.Playing) StartCoroutine(Party(PartyType.Dream));
        }

        void OnMorning()
        {
            if (Current == Phase.Playing) StartCoroutine(Party(PartyType.Dance));
        }

        IEnumerator Party(PartyType type)
        {
            Current = Phase.Party;
            if (clock != null) clock.Pause();
            GameManager.Get().AddParty(type);
            onParty?.Invoke(type);

            if (type == PartyType.Dream)
            {
                // 모두 잡았다 → 스르르 잠듦
                if (banner != null) banner.Show(GameManager.DreamTitle, bannerSeconds + sleepFadeSeconds);
                if (fade != null) yield return fade.FadeTo(1f, sleepFadeSeconds);
                yield return new WaitForSeconds(bannerSeconds);
            }
            else
            {
                // 아침까지 못 잡음 → 남은 모기들이 머리 주변에서 춤춤
                if (swarm != null) swarm.Dance();
                if (banner != null) banner.Show(GameManager.DanceTitle, bannerSeconds);
                yield return new WaitForSeconds(bannerSeconds + 0.3f);
                if (fade != null) yield return fade.FadeTo(1f, betweenFadeSeconds);
            }

            // 어두운 동안 다음 판 준비 → 밝아지며 시작
            if (swarm != null) swarm.DespawnAll();
            yield return new WaitForSeconds(0.3f);
            BeginRound();
            if (fade != null) yield return fade.FadeTo(0f, wakeFadeSeconds);
        }

        // 잡을 때마다 "N마리 잡았다!" (마지막 한 마리는 꿈나라 파티 문구가 대신함)
        void OnCaught(Mosquito m)
        {
            if (subtitles == null || swarm == null || Current != Phase.Playing) return;
            int remaining = swarm.Total - swarm.Caught;
            if (remaining > 0) subtitles.ShowCatch(swarm.Caught, remaining);
        }

        void OnEscaped()
        {
            if (subtitles != null && Current == Phase.Playing) subtitles.ReportEscape();
        }

        void ShowSubtitle(string text)
        {
            if (subtitles != null) subtitles.Show(text);
        }
    }
}
