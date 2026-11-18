using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

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

            var subGo = FindOrCreate("Subtitles", systems.transform);
            var sub = Ensure<SubtitlePanel>(subGo);
            var subLock = Ensure<HeadLock>(subGo);
            subLock.distance = 1.1f; subLock.height = -0.32f; subLock.followSpeed = 3f;
            sub.text = Text(subGo, font, overlayMat, 0.5f, new Vector2(1.6f, 0.2f), FontStyles.Bold);      // 약 5cm (1.1m 앞)

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
