using System.Collections;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 메인 화면 (기획서 4.1): 불이 켜진 침실 + 정면 패널(제목 "애앵~", 제작자, Start / Exit).
    /// 버튼은 오른손 레이로 누름 (XRI UI). 메인으로 돌아오면 기록(파티 횟수) 초기화.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        public ScreenFade fade;

        void Awake()
        {
            if (fade == null) fade = FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
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

        public void OnStartButton() => GameFlow.LoadPlay();
        public void OnExitButton() => GameFlow.Quit();
    }
}
