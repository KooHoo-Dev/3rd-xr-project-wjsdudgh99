using System;
using System.Collections;
using UnityEngine;

namespace AeAeng
{
    public enum PartyType { Dream, Dance }

    /// <summary>
    /// 씬이 바뀌어도 남는 기록 (기획서 3.3). DontDestroyOnLoad.
    /// - 꿈나라 파티 횟수, 모기와 댄싱 파티 횟수 (플레이 → 결과 화면으로 전달)
    /// - 첫 판 튜토리얼을 봤는지
    /// 메인 화면으로 돌아가면 ResetRecords().
    /// 씬에 없으면 GameManager.Get() 이 처음 부를 때 만든다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int DreamParties { get; private set; }
        public int DanceParties { get; private set; }
        public bool TutorialDone { get; set; }

        [Tooltip("Meta 메뉴를 다녀와서 포커스를 되찾으면 오디오 출력을 다시 시작 (녹화 시작 뒤 소리가 끊기던 문제)")]
        public bool restartAudioOnFocus = true;

        /// <summary>기록이 바뀌었을 때 (손목 UI 갱신 등)</summary>
        public event Action Changed;

        public static GameManager Get()
        {
            if (Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>(); // Awake 에서 Instance 설정
            }
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // 앱이 켜지자마자(첫 씬보다 먼저) 만들어 둠 → 시작할 때의 포커스 변화도 놓치지 않음
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => Get();

        IEnumerator Start()
        {
            // 11/18: 앱 시작 때 "포커스 얻음 → 잃음 → 다시 얻음"이 반복되는 사이에 오디오 출력이 만들어지면
            // 처음부터 소리가 안 남 (Meta 메뉴를 한 번 다녀와야 들렸음) → 시작 직후 한 번 더 새로 만듦
            yield return new WaitForSecondsRealtime(1.5f);
            if (restartAudioOnFocus) RestartAudio("시작 직후");
        }

        /// <summary>오디오 출력을 새로 만듦. 모든 소리가 멈추므로 Mosquito 가 OnAudioConfigurationChanged 를 받아 다시 재생</summary>
        public static void RestartAudio(string reason)
        {
            var cfg = AudioSettings.GetConfiguration();
            bool ok = AudioSettings.Reset(cfg);
            Debug.Log($"[GameManager] 오디오 출력 다시 시작 ({reason}): {(ok ? "성공" : "실패")}");
        }

        // Meta 메뉴를 열었다 닫은 뒤 소리가 안 나던 문제 대응: 포커스를 되찾으면 소리를 다시 켜고 상태를 기록
        void OnApplicationFocus(bool focus)
        {
            if (!focus) { Debug.Log("[GameManager] 포커스 잃음 (Meta 메뉴 등)"); return; }
            if (!PauseMenu.AnyOpen) AudioListener.pause = false;

            // 11/18: 소리가 "재생 중"인데도 헤드셋에서 안 들림 → 시스템 메뉴가 소리 출력을 가져갔다 돌려줄 때
            // 유니티 출력 스트림이 끊긴 것으로 보임. 오디오 출력을 새로 만들고(Reset) 모기 소리를 다시 튼다
            if (restartAudioOnFocus) RestartAudio("포커스 되찾음");
            int playing = 0, total = 0;
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                total++;
                if (s.isPlaying) playing++;
            }
            Debug.Log($"[GameManager] 포커스 되찾음: AudioListener.pause={AudioListener.pause}, volume={AudioListener.volume:0.00}, " +
                      $"재생 중인 소리 {playing}/{total}, 출력 {AudioSettings.outputSampleRate}Hz");
        }

        public void AddParty(PartyType type)
        {
            if (type == PartyType.Dream) DreamParties++;
            else DanceParties++;
            Debug.Log($"[GameManager] {(type == PartyType.Dream ? "꿈나라" : "모기와 댄싱")} 파티! (꿈나라 {DreamParties}, 댄싱 {DanceParties})");
            Changed?.Invoke();
        }

        public void ResetRecords()
        {
            DreamParties = 0;
            DanceParties = 0;
            TutorialDone = false;
            Changed?.Invoke();
        }
    }
}
