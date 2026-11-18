using System;
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
