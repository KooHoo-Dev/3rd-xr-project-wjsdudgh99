using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 씬 흐름 만들기 (AeAeng > Build Scene Flow). 플레이 씬을 고친 뒤 다시 실행하면 메인/결과 씬도 새로 만들어짐.
    ///  1) 플레이 씬(test): EventSystem(XR UI), 메인 복귀 확인 창(PauseMenu) + 왼손 길게 누르기 연결
    ///  2) 메인 씬(Menu): 플레이 씬을 복사해 게임 요소를 빼고, 불 켠 침실 + 제목/제작자/Start/Exit 패널
    ///  3) 결과 씬(Result): 플레이 씬을 복사해 게임 요소를 빼고, 아침 햇살 + 파티 횟수 패널
    ///  4) 빌드 목록: Menu → test → Result
    /// 버튼은 오른손 레이로 누름 (XRI TrackedDeviceGraphicRaycaster + XRUIInputModule).
    /// </summary>
    public static class SceneFlowSetup
    {
        const string PlayPath = "Assets/AeAeng/test.unity";
        const string MenuPath = "Assets/AeAeng/Menu.unity";
        const string ResultPath = "Assets/AeAeng/Result.unity";
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";

        const string Author = "전영호";

        static readonly Color PanelColor = new Color(0.04f, 0.06f, 0.11f, 0.88f);
        static readonly Color TitleColor = new Color(1f, 0.93f, 0.75f);
        static readonly Color TextColor = new Color(0.92f, 0.94f, 1f);
        static readonly Color ButtonColor = new Color(0.20f, 0.45f, 0.55f, 1f);
        static readonly Color ButtonAltColor = new Color(0.30f, 0.30f, 0.36f, 1f);

        static TMP_FontAsset _font;

        [MenuItem("AeAeng/Build Scene Flow")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (_font == null) { Debug.LogError($"[SceneFlow] 한글 폰트 {FontPath} 가 없습니다."); return; }

            // 1) 플레이 씬
            var play = EditorSceneManager.OpenScene(PlayPath, OpenSceneMode.Single);
            SetupPlayScene();
            EditorSceneManager.SaveScene(play);

            // 2) 메인 / 3) 결과 (플레이 씬 복사)
            MakeFromPlay(MenuPath, SetupMenuScene);
            MakeFromPlay(ResultPath, SetupResultScene);

            // 4) 빌드 목록
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(PlayPath, true),
                new EditorBuildSettingsScene(ResultPath, true),
            };

            EditorSceneManager.OpenScene(PlayPath, OpenSceneMode.Single);
            Debug.Log("[SceneFlow] 메인(Menu) → 플레이(test) → 결과(Result) 완료. 빌드 목록도 이 순서로 설정했습니다.");
        }

        // ------------------------------------------------------------------
        // 플레이 씬
        // ------------------------------------------------------------------

        static void SetupPlayScene()
        {
            var cam = XrCamera();
            EnsureEventSystem();
            SetRays(false);

            var systems = GameObject.Find("GameSystems");
            if (systems == null) { Debug.LogError("[SceneFlow] GameSystems 가 없습니다. AeAeng > Setup Play Loop 를 먼저 실행하세요."); return; }

            // 메인 복귀 확인 창
            var old = systems.transform.Find("PauseMenu");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var pauseGo = new GameObject("PauseMenu");
            pauseGo.transform.SetParent(systems.transform, false);
            var pause = pauseGo.AddComponent<PauseMenu>();

            var canvas = MakeCanvas("PausePanel", pauseGo.transform, cam, new Vector2(760, 380), 0.0012f);
            var hl = canvas.gameObject.AddComponent<HeadLock>();
            hl.distance = 1.2f; hl.height = -0.05f; hl.followSpeed = 4f;
            Label(canvas.transform, "Question", "메인 화면으로 가시겠습니까?", 54, new Vector2(0, 70), new Vector2(720, 100), TitleColor, FontStyles.Bold);
            var yes = MakeButton(canvas.transform, "Yes", "네", new Vector2(-170, -80), ButtonColor);
            var no = MakeButton(canvas.transform, "No", "아니오", new Vector2(170, -80), ButtonAltColor);
            UnityEventTools.AddPersistentListener(yes.onClick, pause.OnYes);
            UnityEventTools.AddPersistentListener(no.onClick, pause.OnNo);
            pause.panel = canvas.gameObject;
            pause.rays = Object.FindFirstObjectByType<RayVisibility>(FindObjectsInactive.Include);
            pause.lightSwitch = Object.FindFirstObjectByType<LightSwitchInput>(FindObjectsInactive.Include);
            canvas.gameObject.SetActive(false);

            // 왼손 트리거 길게 누르기 → 창 열기
            if (pause.lightSwitch != null)
            {
                var ev = pause.lightSwitch.onLongPress;
                for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(ev, i);
                UnityEventTools.AddPersistentListener(ev, pause.Open);
                EditorUtility.SetDirty(pause.lightSwitch);
            }
            else Debug.LogWarning("[SceneFlow] LightSwitchInput 이 없어 길게 누르기를 연결하지 못했습니다.");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ------------------------------------------------------------------
        // 메인 / 결과 씬
        // ------------------------------------------------------------------

        static void MakeFromPlay(string path, System.Action<Camera> setup)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(PlayPath, path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            StripGameplay();
            setup(XrCamera());
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>판 진행 / 모기 / 테스트 도구 / 손목 UI 를 뺌 (방, 가구, 손, 파리채, 조명은 그대로)</summary>
        static void StripGameplay()
        {
            var kill = new List<GameObject>();
            foreach (var name in new[] { "GameSystems", "MosquitoSwarm", "MosquitoLandingPoints", "LightingTuner (TEST)", "HandOffsetTuner (TEST)" })
            {
                var go = GameObject.Find(name);
                if (go != null) kill.Add(go);
            }
            foreach (var m in Object.FindObjectsByType<Mosquito>(FindObjectsInactive.Include, FindObjectsSortMode.None)) kill.Add(m.gameObject);
            foreach (var w in Object.FindObjectsByType<WristUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) kill.Add(w.gameObject);
            foreach (var go in kill) if (go != null) Object.DestroyImmediate(go);

            foreach (var c in Object.FindObjectsByType<RoundClock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.GetComponents<Component>().Length <= 2) Object.DestroyImmediate(c.gameObject); // Transform + RoundClock 뿐
                else Object.DestroyImmediate(c);
            }
            // 레이 보이기 + 원거리 (버튼 누르기), 왼손 스위치는 끔
            SetRays(true);
            foreach (var s in Object.FindObjectsByType<LightSwitchInput>(FindObjectsInactive.Include, FindObjectsSortMode.None)) s.enabled = false;
        }

        static void SetupMenuScene(Camera cam)
        {
            SetLight(on: true, dawn: -1f);   // 불이 켜진 밤 침실

            var go = new GameObject("MenuController");
            var ctrl = go.AddComponent<MenuController>();

            // 정면 약 2m (침대 자리 기준 +Z), 앉은 눈높이보다 조금 위
            var canvas = MakeCanvas("MenuPanel", null, cam, new Vector2(900, 620), 0.0013f);
            canvas.transform.SetPositionAndRotation(SeatFront(2.0f, 1.35f), SeatRotation());
            Label(canvas.transform, "Title", "애앵~", 170, new Vector2(0, 150), new Vector2(860, 220), TitleColor, FontStyles.Bold);
            Label(canvas.transform, "Author", Author, 52, new Vector2(0, 30), new Vector2(860, 80), TextColor, FontStyles.Normal);
            var start = MakeButton(canvas.transform, "Start", "Start", new Vector2(-170, -170), ButtonColor);
            var exit = MakeButton(canvas.transform, "Exit", "Exit", new Vector2(170, -170), ButtonAltColor);
            UnityEventTools.AddPersistentListener(start.onClick, ctrl.OnStartButton);
            UnityEventTools.AddPersistentListener(exit.onClick, ctrl.OnExitButton);
            ctrl.fade = Object.FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
        }

        static void SetupResultScene(Camera cam)
        {
            SetLight(on: false, dawn: 1f);   // 아침 햇살이 드는 방

            var go = new GameObject("ResultController");
            var ctrl = go.AddComponent<ResultController>();

            var canvas = MakeCanvas("ResultPanel", null, cam, new Vector2(900, 620), 0.0013f);
            canvas.transform.SetPositionAndRotation(SeatFront(2.0f, 1.35f), SeatRotation());
            Label(canvas.transform, "Title", "오늘 밤의 기록", 80, new Vector2(0, 200), new Vector2(860, 120), TitleColor, FontStyles.Bold);
            ctrl.dreamText = Label(canvas.transform, "Dream", "꿈나라 파티  0번", 64, new Vector2(0, 60), new Vector2(860, 100), TextColor, FontStyles.Normal);
            ctrl.danceText = Label(canvas.transform, "Dance", "모기와 댄싱 파티  0번", 64, new Vector2(0, -40), new Vector2(860, 100), TextColor, FontStyles.Normal);
            ctrl.hintText = Label(canvas.transform, "Hint", "아무 버튼이나 눌러 메인으로", 44, new Vector2(0, -210), new Vector2(860, 80), new Color(0.75f, 0.9f, 1f), FontStyles.Italic);
            ctrl.fade = Object.FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
        }

        // ------------------------------------------------------------------
        // UI 만들기
        // ------------------------------------------------------------------

        static Canvas MakeCanvas(string name, Transform parent, Camera cam, Vector2 size, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            go.AddComponent<TrackedDeviceGraphicRaycaster>(); // XR 레이로 버튼 누르기
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;

            var bg = new GameObject("Background", typeof(RectTransform)).GetComponent<RectTransform>();
            bg.SetParent(rt, false);
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            var img = bg.gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = PanelColor;
            img.raycastTarget = true; // 패널 위에서도 레이가 멈추게
            return canvas;
        }

        static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Vector2 pos, Vector2 rect,
            Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = rect;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string text, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(300, 110);
            var img = go.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(go.transform, "Label", text, 56, Vector2.zero, rt.sizeDelta, Color.white, FontStyles.Bold);
            return btn;
        }

        // ------------------------------------------------------------------
        // 기타
        // ------------------------------------------------------------------

        static Camera XrCamera()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            return origin != null ? origin.Camera : Camera.main;
        }

        static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
            foreach (var m in es.GetComponents<BaseInputModule>())
                if (!(m is XRUIInputModule)) Object.DestroyImmediate(m);
            if (es.GetComponent<XRUIInputModule>() == null) es.gameObject.AddComponent<XRUIInputModule>();
        }

        static void SetRays(bool show)
        {
            foreach (var r in Object.FindObjectsByType<RayVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                r.showRays = show;
                EditorUtility.SetDirty(r);
            }
        }

        static void SetLight(bool on, float dawn)
        {
            foreach (var room in Object.FindObjectsByType<RoomLightController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                room.startOn = on;
                EditorUtility.SetDirty(room);
            }
            foreach (var s in Object.FindObjectsByType<SunriseController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                s.previewDawn = dawn;
                EditorUtility.SetDirty(s);
            }
        }

        static Vector3 SeatFront(float distance, float height)
        {
            var p = Object.FindFirstObjectByType<StationaryPlayer>(FindObjectsInactive.Include);
            var seat = p != null ? p.seatPosition : Vector3.zero;
            float yaw = p != null ? p.seatYaw : 0f;
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var pos = seat + fwd * distance;
            pos.y = height;
            return pos;
        }

        static Quaternion SeatRotation()
        {
            var p = Object.FindFirstObjectByType<StationaryPlayer>(FindObjectsInactive.Include);
            return Quaternion.Euler(0f, p != null ? p.seatYaw : 0f, 0f);
        }
    }
}
