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
    ///  2) 메인 씬(Menu): 플레이 씬을 복사해 게임 요소를 빼고, 불 켠 침실 + 제목/Start/Exit 패널 + 무기 선택창(Start 뒤)
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

        // 11/19: 시연 영상 조건(시작 화면에 앱 이름 + 내 이름) 때문에 메인 화면에 다시 넣음
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
            // 이미 있으면 파일 내용만 덮어씀 → .meta(GUID)가 그대로라 빌드 목록/참조가 안 바뀜
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                System.IO.File.Copy(PlayPath, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            else AssetDatabase.CopyAsset(PlayPath, path);
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
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "TunerLabel") kill.Add(t.gameObject); // 녹색 LIGHT OFF 글씨 (테스트용)
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
            // 11/18: 제작자 이름 뺐다가 11/19에 제목 아래 작게 다시 넣음 (시연 영상 조건). 무기 버튼은 Start 뒤 무기 선택창으로 옮김
            var canvas = MakeCanvas("MenuPanel", null, cam, new Vector2(900, 520), 0.0013f);
            canvas.transform.SetPositionAndRotation(SeatFront(2.0f, 1.35f), SeatRotation());
            Label(canvas.transform, "Title", "애~앵", 170, new Vector2(0, 115), new Vector2(860, 220), TitleColor, FontStyles.Bold);
            Label(canvas.transform, "Author", Author, 46, new Vector2(0, -20), new Vector2(860, 60), TextColor, FontStyles.Normal);
            // 11/19: Tutorial / Start / Exit (튜토리얼은 강제가 아니라 고를 수 있게)
            var tutorial = MakeButton(canvas.transform, "Tutorial", "Tutorial", new Vector2(-300, -140), ButtonAltColor);
            var start = MakeButton(canvas.transform, "Start", "Start", new Vector2(0, -140), ButtonColor);
            var exit = MakeButton(canvas.transform, "Exit", "Exit", new Vector2(300, -140), ButtonAltColor);
            foreach (var b in new[] { tutorial, start, exit }) ((RectTransform)b.transform).sizeDelta = new Vector2(260, 110);
            UnityEventTools.AddPersistentListener(tutorial.onClick, ctrl.OnTutorialButton);
            UnityEventTools.AddPersistentListener(start.onClick, ctrl.OnStartButton);
            UnityEventTools.AddPersistentListener(exit.onClick, ctrl.OnExitButton);
            ctrl.mainPanel = canvas.gameObject;

            ctrl.weaponPanel = BuildWeaponPanel(cam, ctrl);
            ctrl.weaponPanel.SetActive(false);
            ctrl.fade = Object.FindFirstObjectByType<ScreenFade>(FindObjectsInactive.Include);
        }

        // ------------------------------------------------------------------
        // 무기 선택창: 카드 2장 (이름 / 정사각형 이미지 / 스펙 / 시작) + 뒤로
        // ------------------------------------------------------------------

        const string SwatterPrefabPath = "Assets/AeAeng/Prefabs/FlySwatter.prefab";
        const string NewspaperPrefabPath = "Assets/AeAeng/Prefabs/Newspaper.prefab";
        const string IconDir = "Assets/AeAeng/Textures/UI";
        const float IconOrthoSize = 0.22f;  // 무기 이미지 화면 반 높이 (m)
        static readonly Color CardColor = new Color(0.10f, 0.12f, 0.19f, 0.95f);
        static readonly Color IconBgColor = new Color(0.20f, 0.22f, 0.30f, 1f);
        static readonly Color StartColor = new Color(0.85f, 0.55f, 0.18f, 1f);
        static readonly Color SpecLabelColor = new Color(0.72f, 0.75f, 0.85f);

        static GameObject BuildWeaponPanel(Camera cam, MenuController ctrl)
        {
            var canvas = MakeCanvas("WeaponPanel", null, cam, new Vector2(1100, 1060), 0.0011f);
            canvas.transform.SetPositionAndRotation(SeatFront(2.0f, 1.35f), SeatRotation());
            Label(canvas.transform, "Title", "무기를 고르세요", 70, new Vector2(0, 460), new Vector2(1000, 100), TitleColor, FontStyles.Bold);

            var swatterHit = AssetDatabase.LoadAssetAtPath<GameObject>(SwatterPrefabPath)?.GetComponent<SwatterHit>();
            var paperHit = AssetDatabase.LoadAssetAtPath<GameObject>(NewspaperPrefabPath)?.GetComponent<SwatterHit>();

            var a = WeaponCard(canvas.transform, "Card_FlySwatter", new Vector2(-270, 10), "파리채",
                RenderWeaponIcon(SwatterPrefabPath, "WeaponIcon_FlySwatter"),
                new[]
                {
                    ("길이", "45cm"),
                    ("치는 면", "14 x 16cm"),
                    ("공중 저격 확률", Percent(swatterHit, 0.2f)),
                    ("스윙 속도", "보통"),   // 기준은 3.5m/s 지만 채가 길어(머리 38cm) 손목은 신문지보다 덜 돌려도 됨
                });
            var b = WeaponCard(canvas.transform, "Card_Newspaper", new Vector2(270, 10), "신문지",
                RenderWeaponIcon(NewspaperPrefabPath, "WeaponIcon_Newspaper"),
                new[]
                {
                    ("길이", "35cm"),
                    ("치는 면", "끝쪽 16cm"),
                    ("공중 저격 확률", Percent(paperHit, 0.35f)),
                    ("스윙 속도", "보통"), // 11/19: 35cm, 2.5m/s, 판정 중심 27cm → 손목 회전이 파리채(3.5m/s, 38cm)와 거의 같음 (전: 32cm, 3.0m/s → "빠르게")
                });
            UnityEventTools.AddPersistentListener(a.onClick, ctrl.OnStartFlySwatter);
            UnityEventTools.AddPersistentListener(b.onClick, ctrl.OnStartNewspaper);

            var back = MakeButton(canvas.transform, "Back", "뒤로", new Vector2(0, -465), ButtonAltColor);
            UnityEventTools.AddPersistentListener(back.onClick, ctrl.OnBackButton);
            return canvas.gameObject;
        }

        static string Percent(SwatterHit hit, float fallback) =>
            $"{Mathf.RoundToInt((hit != null ? hit.airCatchChance : fallback) * 100f)}%";

        /// <summary>카드 하나: 이름(위) → 정사각형 이미지 → 스펙 줄 → 시작 버튼. 시작 버튼을 돌려줌</summary>
        static Button WeaponCard(Transform parent, string name, Vector2 pos, string title, Sprite icon, (string, string)[] specs)
        {
            var card = Box(parent, name, pos, new Vector2(500, 820), CardColor);
            Label(card, "Name", title, 64, new Vector2(0, 350), new Vector2(460, 90), TitleColor, FontStyles.Bold);

            var frame = Box(card, "ImageFrame", new Vector2(0, 125), new Vector2(340, 340), IconBgColor);
            if (icon != null)
            {
                var imgGo = new GameObject("Image", typeof(RectTransform));
                imgGo.transform.SetParent(frame, false);
                var rt = (RectTransform)imgGo.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(8, 8); rt.offsetMax = new Vector2(-8, -8);
                var img = imgGo.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            for (int i = 0; i < specs.Length; i++)
            {
                float y = -85 - i * 52;
                var l = Label(card, "Spec" + i, specs[i].Item1, 38, new Vector2(-10, y), new Vector2(420, 50), SpecLabelColor, FontStyles.Normal);
                l.alignment = TextAlignmentOptions.Left;
                var v = Label(card, "Value" + i, specs[i].Item2, 38, new Vector2(10, y), new Vector2(420, 50), TitleColor, FontStyles.Bold);
                v.alignment = TextAlignmentOptions.Right;
            }

            var start = MakeButton(card, "Start", "시작", new Vector2(0, -335), StartColor);
            ((RectTransform)start.transform).sizeDelta = new Vector2(420, 100);
            ((RectTransform)start.transform.Find("Label")).sizeDelta = new Vector2(420, 100);
            return start;
        }

        static RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>무기 프리팹을 비스듬히 세워 정사각형 PNG 로 찍고 Sprite 로 가져옴 (게임 속 무기와 똑같이 보이게)</summary>
        static Sprite RenderWeaponIcon(string prefabPath, string fileName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning($"[SceneFlow] {prefabPath} 가 없어 무기 이미지를 못 만들었습니다."); return null; }
            if (!AssetDatabase.IsValidFolder(IconDir)) AssetDatabase.CreateFolder("Assets/AeAeng/Textures", "UI");
            string path = $"{IconDir}/{fileName}.png";

            var pru = new PreviewRenderUtility();
            try
            {
                var inst = pru.InstantiatePrefabInScene(prefab);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 0f, -35f)); // 머리가 오른쪽 위로
                var rs = inst.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return null;
                var bounds = rs[0].bounds;
                foreach (var r in rs) bounds.Encapsulate(r.bounds);

                var cam = pru.camera;
                cam.orthographic = true;
                // 두 무기를 같은 배율로 찍어 크기 차이가 보이게 (파리채 45cm 가 꽉 차는 크기 기준 → 신문지 35cm 는 약 78%)
                cam.orthographicSize = IconOrthoSize;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 10f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = IconBgColor;
                cam.transform.SetPositionAndRotation(bounds.center + Vector3.back * 2f, Quaternion.identity);
                pru.lights[0].intensity = 1.2f;
                pru.lights[0].transform.rotation = Quaternion.Euler(30f, 25f, 0f);
                pru.lights[1].intensity = 0.6f;
                pru.ambientColor = new Color(0.40f, 0.40f, 0.45f);

                pru.BeginStaticPreview(new Rect(0, 0, 512, 512));
                pru.Render(true, false);
                var tex = pru.EndStaticPreview();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SceneFlow] 무기 이미지 렌더 실패 ({fileName}): {e}");
                return null;
            }
            finally
            {
                pru.Cleanup();
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void SetupResultScene(Camera cam)
        {
            SetLight(on: false, dawn: 1f);   // 아침 햇살이 드는 방

            var go = new GameObject("ResultController");
            var ctrl = go.AddComponent<ResultController>();

            var canvas = MakeCanvas("ResultPanel", null, cam, new Vector2(900, 620), 0.0013f);
            canvas.transform.SetPositionAndRotation(SeatFront(2.0f, 1.35f), SeatRotation());
            Label(canvas.transform, "Title", "오늘 밤의 기록", 80, new Vector2(0, 200), new Vector2(860, 120), TitleColor, FontStyles.Bold);
            ctrl.dreamText = Label(canvas.transform, "Dream", "꿀잠  0번", 64, new Vector2(0, 60), new Vector2(860, 100), TextColor, FontStyles.Normal);
            ctrl.danceText = Label(canvas.transform, "Dance", "밤샘  0번", 64, new Vector2(0, -40), new Vector2(860, 100), TextColor, FontStyles.Normal);
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
