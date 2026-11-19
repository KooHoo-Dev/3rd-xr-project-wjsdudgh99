using System.Collections;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 튜토리얼 (11/19: 메인 화면 Tutorial 버튼으로 고를 때만. 강제 아님).
    /// 플레이 씬에서 GameManager.TutorialMode 일 때만 동작하고, RoundController 는 판을 시작하지 않는다.
    /// 시간 제한 / 파티 없이 모기 1마리로 순서대로 안내 (알림과 같은 모양, 단계가 끝날 때까지 계속 보임):
    ///  1) 소리 듣기  2) 왼손 트리거로 불 켜기  3) 모기가 앉을 때까지 기다리기
    ///  4) 책상 위 무기 잡기  5) 앉은 모기에게 천천히 다가가 마지막에 빠르게 치기  → 완료, 메인으로
    /// 모기가 도망가면 "불을 끄고 5초 기다렸다가 다시 켜기"를 안내.
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        [Header("참조 (비워 두면 자동으로 찾음)")]
        public MosquitoSwarm swarm;
        public RoomLightController room;
        public RoundClock clock;
        public ScreenFade fade;
        public PartyBanner banner;
        public SubtitlePanel toast;

        [Tooltip("소리 듣기 안내를 보여 주는 시간 (초)")]
        public float listenSeconds = 5f;
        [Tooltip("완료 후 메인으로 돌아가기까지 (초)")]
        public float endSeconds = 3.5f;

        public int Step { get; private set; }

        bool _escaped;

        void Awake()
        {
            if (swarm == null) swarm = FindFirstObjectByType<MosquitoSwarm>();
            if (room == null) room = FindFirstObjectByType<RoomLightController>();
            if (clock == null) clock = FindFirstObjectByType<RoundClock>();
            if (fade == null) fade = FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
            if (banner == null) banner = FindFirstObjectByType<PartyBanner>(FindObjectsInactive.Include);
            if (toast == null) toast = FindFirstObjectByType<SubtitlePanel>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            if (swarm != null) swarm.onEscaped.AddListener(OnEscaped);
        }

        void OnDisable()
        {
            if (swarm != null) swarm.onEscaped.RemoveListener(OnEscaped);
        }

        void OnEscaped() => _escaped = true;

        void Start()
        {
            if (!GameManager.Get().TutorialMode) { enabled = false; return; }
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Debug.Log("[Tutorial] 시작");
            if (fade != null) fade.SetAlpha(1f);
            yield return null; // 자리 맞춤 뒤에 밝아지게

            // 시간 제한 없이: 시계는 1:00 에 멈춰 둠
            if (clock != null) { clock.StartRound(); clock.Pause(); }
            if (room != null) room.SetLight(false, false);
            if (swarm != null) swarm.SpawnRound(1);
            if (fade != null) yield return fade.FadeTo(0f, 1.2f);

            // 1) 소리 듣기
            Step = 1;
            Say("모기 소리가 들리나요?", "소리가 나는 쪽으로 고개를 돌려 보세요");
            yield return new WaitForSeconds(listenSeconds);

            // 2) 불 켜기
            Step = 2;
            Say("왼손 트리거를 눌렀다 떼서 불을 켜세요", "길게 누르면 메인으로 가는 창이 떠요");
            yield return new WaitUntil(() => room == null || room.IsOn);

            // 3) 착지 기다리기
            Step = 3;
            Say("불을 켜면 모기가 잠시 뒤 벽이나 가구에 앉아요", "소리가 멈추면 앉은 거예요");
            float t0 = Time.time;
            yield return new WaitUntil(() => AnyLanded() || Time.time - t0 > 8f);

            // 4) 무기 잡기
            Step = 4;
            Say("책상 위 무기에 오른손을 대고 트리거를 꾹 누르세요", "트리거를 떼면 무기는 책상으로 돌아가요");
            yield return new WaitUntil(WeaponHeld);

            // 5) 잡기 (도망가면 다시 안내)
            Step = 5;
            _escaped = false;
            SayCatchHint();
            while (swarm != null && swarm.Caught < 1)
            {
                if (_escaped)
                {
                    _escaped = false;
                    Say("모기가 눈치채고 도망갔어요", "불을 끄고 5초 기다렸다가 다시 켜 보세요");
                    // 다시 나와서 앉을 때까지 기다린 뒤 잡기 안내로
                    yield return new WaitUntil(() => swarm.Caught >= 1 || AnyLanded());
                    if (swarm.Caught < 1) SayCatchHint();
                }
                yield return null;
            }

            // 완료
            Step = 6;
            Debug.Log("[Tutorial] 완료");
            GameManager.Get().TutorialDone = true;
            var round = FindFirstObjectByType<RoundController>();
            if (round != null) round.PlayClear(); // 꿀잠 엔딩과 같은 소리
            if (toast != null) toast.Show("잡았다!", "이제 메인에서 Start 로 진짜 게임을 해 보세요", SubtitlePanel.Style.Catch, endSeconds);
            if (banner != null) banner.Show("튜토리얼 완료!", endSeconds);
            yield return new WaitForSeconds(endSeconds);
            GameManager.Get().SetTutorialMode(false);
            GameFlow.LoadMenu();
        }

        void SayCatchHint() =>
            Say("앉은 모기에게 천천히 다가가 마지막에 빠르게 내리치세요", "가까이서 빨리 움직이면 모기가 눈치채요");

        void Say(string main, string sub)
        {
            Debug.Log($"[Tutorial] {Step}단계: {main}");
            if (toast != null) toast.ShowSticky(main, sub);
        }

        bool AnyLanded()
        {
            if (swarm == null) return true;
            foreach (var m in swarm.Active)
                if (m != null && (m.Current == Mosquito.State.Landed || m.Current == Mosquito.State.Alerted)) return true;
            return false;
        }

        static bool WeaponHeld()
        {
            foreach (var g in FindObjectsByType<SwatterGrab>(FindObjectsSortMode.None))
                if (g.isActiveAndEnabled && g.IsHeld) return true;
            return false;
        }
    }
}
