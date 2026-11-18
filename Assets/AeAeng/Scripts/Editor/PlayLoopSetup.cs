using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 플레이 루프 준비 (AeAeng > Setup Play Loop). 다시 실행해도 됨.
    ///  - GameSystems: RoundController (판 진행, 파티, 다음 판)
    ///  - PartyBanner (정면 큰 문구), SubtitlePanel (아래쪽 자막): 페이드 위에도 보이는 Overlay 글자
    ///  - ScreenFade: XR 카메라 자식, 안쪽에서 보이는 작은 구
    ///  - WristUI: 왼손 손목 (잡은 수, 파티 횟수)
    ///  - RoundClock / MosquitoSwarm 의 "플레이 누르면 바로 시작"을 끄고 RoundController 가 시작하게
    /// </summary>
    public static class PlayLoopSetup
    {
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";
        const string UiMatDir = "Assets/AeAeng/Materials/UI";
        const string OverlayMatPath = UiMatDir + "/NotoSansKR Overlay.mat";
        const string FadeMatPath = UiMatDir + "/ScreenFade.mat";

        static readonly Color TextColor = new Color(1f, 0.96f, 0.88f);

        [MenuItem("AeAeng/Setup Play Loop")]
        public static void Setup()
        {
            EnsureFolder(UiMatDir);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) { Debug.LogError($"[PlayLoopSetup] 한글 폰트 {FontPath} 가 없습니다."); return; }
            var overlayMat = OverlayMaterial(font);

            var origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            var cam = origin != null && origin.Camera != null ? origin.Camera.transform : null;
            if (cam == null) { Debug.LogError("[PlayLoopSetup] XR Origin 카메라를 찾지 못했습니다."); return; }

            // ---- 시스템 ----
            var systems = FindOrCreate("GameSystems", null);
            var round = systems.GetComponent<RoundController>();
            if (round == null) round = Undo.AddComponent<RoundController>(systems);

            // ---- 정면 문구 / 자막 ----
            var bannerGo = FindOrCreate("PartyBanner", systems.transform);
            var banner = Ensure<PartyBanner>(bannerGo);
            var bannerLock = Ensure<HeadLock>(bannerGo);
            bannerLock.distance = 1.4f; bannerLock.height = 0.05f; bannerLock.followSpeed = 2f;
            // ※ TextMeshPro(3D) 글자 크기는 10 = 1m. 11/18 처음엔 m 단위로 넣어서 10배 작았음
            banner.text = Text(bannerGo, font, overlayMat, 1.3f, new Vector2(2.4f, 0.5f), FontStyles.Bold);   // 약 13cm (1.4m 앞)

            // 알림(토스트): 예전 녹색 LIGHT OFF 자리 (정면 1m, 시선 아래)
            var sub = BuildToast(systems.transform, font, cam.GetComponent<Camera>());

            // 밝기 튜너(테스트용)와 녹색 "LIGHT OFF" 글씨 제거 (11/18)
            var tuner = GameObject.Find("LightingTuner (TEST)");
            if (tuner != null) Undo.DestroyObjectImmediate(tuner);
            foreach (var t in cam.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == "TunerLabel") Undo.DestroyObjectImmediate(t.gameObject);

            // ---- 화면 페이드 (카메라 자식) ----
            var fade = cam.GetComponentInChildren<ScreenFade>(true);
            if (fade == null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "ScreenFade";
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(cam, false);
                Undo.RegisterCreatedObjectUndo(sphere, "Screen Fade");
                fade = sphere.AddComponent<ScreenFade>();
            }
            fade.transform.localPosition = Vector3.zero;
            fade.transform.localScale = Vector3.one * 0.5f; // 반지름 0.25m (Near Clip 0.01 보다 큼)
            var fr = fade.GetComponent<MeshRenderer>();
            fr.sharedMaterial = FadeMaterial();
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fr.receiveShadows = false;
            fr.enabled = false;

            // ---- 왼손 손목 UI ----
            string wrist = SetupWrist(font);

            // ---- 시작은 RoundController 가 ----
            var clock = Object.FindFirstObjectByType<RoundClock>(FindObjectsInactive.Include);
            if (clock != null) { Undo.RecordObject(clock, "Round start"); clock.startOnPlay = false; EditorUtility.SetDirty(clock); }
            var swarm = Object.FindFirstObjectByType<MosquitoSwarm>(FindObjectsInactive.Include);
            if (swarm != null) { Undo.RecordObject(swarm, "Round start"); swarm.spawnOnStart = false; EditorUtility.SetDirty(swarm); }

            round.clock = clock;
            round.swarm = swarm;
            round.room = Object.FindFirstObjectByType<RoomLightController>(FindObjectsInactive.Include);
            round.fade = fade;
            round.banner = banner;
            round.subtitles = sub;
            EditorUtility.SetDirty(round);

            EditorSceneManager.MarkSceneDirty(systems.scene);
            Debug.Log($"[PlayLoopSetup] RoundController, 파티 문구, 자막, 화면 페이드, 손목 UI({wrist}) 설정 완료. " +
                      $"RoundClock / MosquitoSwarm 자동 시작은 끄고 RoundController 가 시작합니다. 씬을 저장하세요.");
        }

        // ------------------------------------------------------------------
        // 알림(토스트): 반투명 알약 바탕 + 색 테두리 + 아이콘 + 글자 (UGUI, 1px = 1mm)
        // ------------------------------------------------------------------

        const string TexDir = "Assets/AeAeng/Textures/UI";

        static SubtitlePanel BuildToast(Transform parent, TMP_FontAsset font, Camera cam)
        {
            // 예전 3D 글자 자막 / 이전 토스트는 지우고 새로 만듦
            foreach (var name in new[] { "Subtitles", "Toast" })
            {
                var old = parent.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }

            EnsureFolder(TexDir);
            var pillFill = MakeSprite("PillFill", 128, (x, y) => PillAlpha(x, y, 128, 0f), 63);
            var pillRing = MakeSprite("PillRing", 128, (x, y) => PillAlpha(x, y, 128, 5f), 63);
            var check = MakeSprite("IconCheck", 96, (x, y) =>
                Mathf.Max(Stroke(x, y, new Vector2(18, 50), new Vector2(40, 72), 7f),
                          Stroke(x, y, new Vector2(40, 72), new Vector2(80, 26), 7f)), 0);
            var wind = MakeSprite("IconWind", 96, (x, y) =>
                Mathf.Max(Stroke(x, y, new Vector2(14, 30), new Vector2(70, 30), 6f),
                Mathf.Max(Stroke(x, y, new Vector2(26, 50), new Vector2(84, 50), 6f),
                          Stroke(x, y, new Vector2(14, 70), new Vector2(60, 70), 6f))), 0);

            var root = new GameObject("Toast", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Toast");
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(1200, 200);
            rootRt.localScale = Vector3.one * 0.001f; // 1px = 1mm
            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // 알약 (내용 길이에 맞춰 가로로 늘어남)
            var pillGo = new GameObject("Pill", typeof(RectTransform));
            pillGo.transform.SetParent(root.transform, false);
            var pill = (RectTransform)pillGo.transform;
            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            var fill = pillGo.AddComponent<Image>();
            fill.sprite = pillFill;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 64f / 52f; // 모서리 반지름 ≈ 높이의 절반 (104px → 52)
            fill.color = new Color(12 / 255f, 14 / 255f, 22 / 255f, 0.82f);
            fill.raycastTarget = false;
            var h = pillGo.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(34, 44, 18, 18);
            h.spacing = 18;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var fit = pillGo.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 색 테두리 (알약 전체를 덮음, 배치에서 제외)
            var ringGo = new GameObject("Ring", typeof(RectTransform));
            ringGo.transform.SetParent(pill, false);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = Vector2.zero; ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = Vector2.zero; ringRt.offsetMax = Vector2.zero;
            ringGo.AddComponent<LayoutElement>().ignoreLayout = true;
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = pillRing;
            ring.type = Image.Type.Sliced;
            ring.pixelsPerUnitMultiplier = fill.pixelsPerUnitMultiplier;
            ring.raycastTarget = false;

            // 아이콘
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(pill, false);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 52; iconLe.preferredHeight = 52;

            var main = ToastText(pill, "Main", font, 54, FontStyles.Bold);
            var subT = ToastText(pill, "Sub", font, 34, FontStyles.Normal);

            var panel = root.AddComponent<SubtitlePanel>();
            panel.group = group; panel.pill = pill; panel.ring = ring; panel.icon = icon;
            panel.mainText = main; panel.subText = subT;
            panel.checkIcon = check; panel.windIcon = wind;
            var hl = root.GetComponent<HeadLock>();
            hl.distance = 1.0f; hl.height = 0.2f; hl.followSpeed = 3f; // 11/18: 시선 아래(-0.28) → 위로 (모기 보는 데 안 가리게)
            return panel;
        }

        static TextMeshProUGUI ToastText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.text = name == "Main" ? "2마리 잡았다!" : "남은 모기 1";
            return t;
        }

        /// <summary>알약(가로로 늘이는 동그란 모서리) 알파. ring > 0 이면 그 두께의 테두리만</summary>
        static float PillAlpha(int x, int y, int size, float ring)
        {
            float r = size * 0.5f;
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)); // 원 = 9-slice 하면 알약
            float outer = Mathf.Clamp01(r - d);
            if (ring <= 0f) return outer;
            float inner = Mathf.Clamp01(r - ring - d);
            return Mathf.Clamp01(outer - inner);
        }

        /// <summary>두께 w 의 둥근 선분 알파 (안티에일리어싱)</summary>
        static float Stroke(int x, int y, Vector2 a, Vector2 b, float w)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float d = Vector2.Distance(p, a + ab * t);
            return Mathf.Clamp01(w * 0.5f - d + 0.5f);
        }

        /// <summary>흰색 + 알파 마스크 텍스처를 PNG 로 저장하고 스프라이트로 가져옴 (border = 9-slice 모서리)</summary>
        static Sprite MakeSprite(string name, int size, System.Func<int, int, float> alpha, int border)
        {
            string path = $"{TexDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x, size - 1 - y) * 255f)); // 위→아래 좌표
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spriteBorder = new Vector4(border, border, border, border);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static string SetupWrist(TMP_FontAsset font)
        {
            HandPose left = null;
            foreach (var h in Object.FindObjectsByType<HandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h.side == HandPose.Side.Left) left = h;
            if (left == null) return "왼손 없음";

            var go = FindOrCreate("WristUI", left.transform);
            // 손 좌표: 원점 = 손목, +Y = 손등, +Z = 손가락. 손등 쪽 손목(시계 자리)에 붙이고 손등 방향으로 읽힘
            go.transform.localPosition = new Vector3(0f, 0.032f, -0.035f);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var ui = Ensure<WristUI>(go);
            ui.text = Text(go, font, font.material, 0.12f, new Vector2(0.18f, 0.06f), FontStyles.Bold);    // 약 1.2cm (손목, 눈앞 30~40cm)
            ui.text.color = new Color(0.55f, 0.9f, 1f); // 유령 손과 어울리는 하늘색
            return "왼손 손목";
        }

        static TextMeshPro Text(GameObject go, TMP_FontAsset font, Material mat, float size, Vector2 rect, FontStyles style)
        {
            var t = go.GetComponent<TextMeshPro>();
            if (t == null) t = Undo.AddComponent<TextMeshPro>(go);
            t.font = font;
            t.fontSharedMaterial = mat;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = TextColor;
            t.rectTransform.sizeDelta = rect;
            t.text = go.name == "WristUI" ? "모기 0 / 3" : "";
            var r = t.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            EditorUtility.SetDirty(t);
            return t;
        }

        /// <summary>페이드(검은 화면) 위에도 보이는 글자 재질: TMP 모바일 Overlay 셰이더 (ZTest Always, 스테레오 지원)</summary>
        static Material OverlayMaterial(TMP_FontAsset font)
        {
            var shader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OverlayMatPath);
            if (mat == null)
            {
                mat = new Material(font.material) { name = "NotoSansKR Overlay" };
                AssetDatabase.CreateAsset(mat, OverlayMatPath);
            }
            mat.CopyPropertiesFromMaterial(font.material);
            if (shader != null) mat.shader = shader;
            mat.renderQueue = 4001;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material FadeMaterial()
        {
            var shader = Shader.Find("AeAeng/ScreenFade");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FadeMatPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "ScreenFade" };
                AssetDatabase.CreateAsset(mat, FadeMatPath);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject FindOrCreate(string name, Transform parent)
        {
            Transform t = parent != null ? parent.Find(name) : null;
            if (t == null && parent == null)
            {
                var found = GameObject.Find(name);
                if (found != null && found.transform.parent == null) t = found.transform;
            }
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
