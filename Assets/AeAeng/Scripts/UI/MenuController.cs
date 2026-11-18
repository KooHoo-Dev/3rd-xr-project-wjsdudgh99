using System.Collections;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 메인 화면 (기획서 4.1): 불이 켜진 침실 + 정면 패널(제목 "애앵~", Start / Exit).
    /// Start → 무기 선택창 (파리채 / 신문지 카드: 이름, 이미지, 스펙, 시작) → 고른 무기로 게임 시작.
    /// 버튼은 오른손 레이로 누름 (XRI UI). 메인으로 돌아오면 기록(파티 횟수) 초기화.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        public ScreenFade fade;

        [Header("화면 (Build Scene Flow 가 연결)")]
        [Tooltip("제목 + Start / Exit")]
        public GameObject mainPanel;
        [Tooltip("무기 선택창 (처음엔 꺼 둠)")]
        public GameObject weaponPanel;

        bool _loading;

        void Awake()
        {
            if (fade == null) fade = FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
            ShowMain();
        }

        IEnumerator Start()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameManager.Get().ResetRecords();
            if (fade != null)
            {
                fade.SetAlpha(1f);
                yield return null;
                yield return fade.FadeTo(0f, 0.8f);
            }
        }

        // ---- 메인 ----
        public void OnStartButton() => ShowWeapons();
        public void OnExitButton() => GameFlow.Quit();

        // ---- 무기 선택창 ----
        public void OnStartFlySwatter() => StartWith(Weapon.FlySwatter);
        public void OnStartNewspaper() => StartWith(Weapon.Newspaper);
        public void OnBackButton() => ShowMain();

        void StartWith(Weapon w)
        {
            if (_loading) return; // 두 번 눌러도 한 번만
            _loading = true;
            GameManager.Get().SetWeapon(w);
            GameFlow.LoadPlay();
        }

        void ShowMain()
        {
            if (mainPanel != null) mainPanel.SetActive(true);
            if (weaponPanel != null) weaponPanel.SetActive(false);
        }

        void ShowWeapons()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (weaponPanel != null) weaponPanel.SetActive(true);
        }
    }
}
