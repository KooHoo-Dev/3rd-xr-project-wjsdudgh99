using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AeAeng
{
    /// <summary>
    /// 씬 이동 (기획서 4장): 메인(Menu) → 플레이(test) → 결과(Result) → 메인.
    /// 화면을 어둡게 한 뒤 씬을 바꾼다. 새 씬은 각 컨트롤러가 밝힌다.
    /// 코루틴은 씬이 바뀌어도 남는 GameManager 에서 돌린다.
    /// </summary>
    public static class GameFlow
    {
        public const string MenuScene = "Menu";
        public const string PlayScene = "test";
        public const string ResultScene = "Result";

        static bool _loading;

        public static void LoadMenu() => Load(MenuScene);
        public static void LoadPlay() => Load(PlayScene);
        public static void LoadResult() => Load(ResultScene);

        public static void Load(string scene)
        {
            if (_loading) return;
            GameManager.Get().StartCoroutine(LoadRoutine(scene));
        }

        static IEnumerator LoadRoutine(string scene)
        {
            _loading = true;
            // 멈춤 창에서 넘어오는 경우를 위해 시간과 소리를 되돌림
            Time.timeScale = 1f;
            AudioListener.pause = false;

            var fade = Object.FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
            if (fade != null) yield return fade.FadeTo(1f, 0.5f);

            Debug.Log($"[GameFlow] → {scene}");
            SceneManager.LoadScene(scene);
            _loading = false;
        }

        public static void Quit()
        {
            Debug.Log("[GameFlow] 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
