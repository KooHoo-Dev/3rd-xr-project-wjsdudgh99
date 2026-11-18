using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 메뉴 AeAeng > Build Test Bedroom : 현재 열린 씬에 대충 만든 침실 + 방 불 + 달빛 + XR Origin 을 만든다.
    /// 다시 실행하면 기존 TestBedroom 을 지우고 새로 만든다.
    ///
    /// 좌표 기준 (XR Origin = 원점, 플레이어는 +Z 를 바라봄)
    ///   정사각형 방 3m x 3m (작은 원룸 침실, 플레이어는 이동하지 않음)
    ///   뒤(머리 쪽) 벽 z = -0.45 / 왼쪽 벽 x = -0.6 / 오른쪽 x = 2.4 / 앞 z = 2.55 / 천장 y = 2.4
    ///
    /// 문 / 창문: 씬에 "Free Wood Door Pack" 문, "Brick Project Studio" 창문(이름에 Window) 프리팹이 놓여 있으면
    ///     가장 가까운 벽에 그 크기에 맞는 구멍을 뚫고, 벽 두께 가운데로 맞춘다.
    ///     창문이 있으면 달빛 그림자를 켜서 창문으로만 달빛이 들어오게 한다 (유리는 그림자 끔).
    /// </summary>
    public static class TestBedroomBuilder
    {
        const string RootName = "TestBedroom";
        const string MatDir = "Assets/AeAeng/Materials/TestBedroom";
        const string XROriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string DoorPackPath = "Assets/Free Wood Door Pack/";
        const string WindowPackPath = "Assets/Brick Project Studio/";

        const float RoomSize = 3f;
        const float Left = -0.6f, Back = -0.45f;
        const float Right = Left + RoomSize, Front = Back + RoomSize;
        const float Height = 2.4f, Wall = 0.1f;
        /// <summary>벽이 문틀을 살짝 덮도록 구멍을 이만큼 작게 뚫음 (틈으로 빛이 새지 않게)</summary>
        const float DoorOverlap = 0.01f;

        enum Side { Back, Front, Left, Right }

        [MenuItem("AeAeng/Build Test Bedroom")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // 직접 꺼 둔 그룹(예: 상자 가구)은 다시 만들어도 꺼진 채로 유지
            var keepOff = new HashSet<string>();
            var old = GameObject.Find(RootName);
            if (old != null)
            {
                foreach (Transform child in old.transform)
                    if (!child.gameObject.activeSelf) keepOff.Add(child.name);
                Undo.DestroyObjectImmediate(old);
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Test Bedroom");

            // ---------- 머티리얼 ----------
            var mWall = LitMat("Wall", new Color(0.78f, 0.76f, 0.72f));
            var mFloor = LitMat("Floor", new Color(0.45f, 0.33f, 0.24f));
            var mCeiling = LitMat("Ceiling", new Color(0.85f, 0.85f, 0.83f));
            var mWood = LitMat("Wood", new Color(0.50f, 0.36f, 0.25f));
            var mDark = LitMat("DarkPlastic", new Color(0.10f, 0.10f, 0.11f));
            var mWindow = UnlitMat("WindowMoon", new Color(0.16f, 0.22f, 0.38f));
            var mPanel = UnlitMat("CeilingPanel", new Color(0.95f, 0.97f, 1f));

            // ---------- 방 ----------
            var room = Child(root, "Room");
            float w = Right - Left, d = Front - Back, cx = (Left + Right) / 2f, cz = (Back + Front) / 2f;
            Box(room, "Floor", new Vector3(cx, -Wall / 2, cz), new Vector3(w + Wall * 2, Wall, d + Wall * 2), mFloor);
            Box(room, "Ceiling", new Vector3(cx, Height + Wall / 2, cz), new Vector3(w + Wall * 2, Wall, d + Wall * 2), mCeiling);
            bool realWindow = BuildWalls(room, mWall);
            var skyRenderer = BuildNightSky(room);

            // 창문 애셋이 없을 때만 가짜 창문 (왼쪽 벽, 달빛 느낌의 Unlit 면 + 창살)
            var window = Child(room, "Window (fake)");
            if (realWindow) Object.DestroyImmediate(window);
            else
            {
                Box(window, "Glass", new Vector3(Left + 0.01f, 1.45f, 1.5f), new Vector3(0.02f, 1.0f, 0.9f), mWindow, collider: false);
                Box(window, "Bar_V", new Vector3(Left + 0.025f, 1.45f, 1.5f), new Vector3(0.02f, 1.04f, 0.04f), mDark, collider: false);
                Box(window, "Bar_H", new Vector3(Left + 0.025f, 1.45f, 1.5f), new Vector3(0.02f, 0.04f, 0.94f), mDark, collider: false);
                Box(window, "Frame_Top", new Vector3(Left + 0.025f, 1.97f, 1.5f), new Vector3(0.03f, 0.04f, 0.98f), mWood, collider: false);
                Box(window, "Frame_Bottom", new Vector3(Left + 0.025f, 0.93f, 1.5f), new Vector3(0.05f, 0.04f, 0.98f), mWood, collider: false);
            }

            // ---------- 가구 ----------
            // 상자 가구(침대/협탁/스탠드/시계)는 11/18에 뺌: 실제 가구(침대, 책상 등)를 씬 루트에 직접 배치함.
            // 파리채 받침대도 없음: 파리채는 책상 위에 직접 놓음.

            // 모기 대용 구(밝기 확인용)도 11/18에 뺌: 실제 모기 프리팹(Prefabs/Mosquito)을 씀.

            // ---------- 조명 ----------
            var lighting = Child(root, "Lighting");

            var moonGO = Child(lighting, "MoonLight");
            moonGO.transform.rotation = Quaternion.Euler(30f, 100f, 0f); // 왼쪽 창 → 방 안쪽(+X)으로
            var moon = moonGO.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.intensity = 0.1f;
            // 그림자 켬: 벽이 달빛을 막고 창문 구멍으로만 들어와 침대/바닥에 창 모양 빛이 생김
            // (창문이 없으면 닫힌 상자라 방 전체가 가려지므로 끔)
            moon.shadows = realWindow ? LightShadows.Hard : LightShadows.None;
            if (realWindow) SetShadowDistance(8f);

            var ceilGO = Child(lighting, "CeilingLight");
            ceilGO.transform.position = new Vector3(1.1f, Height - 0.15f, 1.3f);
            var ceil = ceilGO.AddComponent<Light>();
            ceil.type = LightType.Point;
            ceil.color = new Color(0.95f, 0.98f, 1f);
            ceil.range = 7f;
            ceil.intensity = 1f;
            ceil.shadows = LightShadows.None;

            // 원형 천장등 (빛이 원형으로 퍼지니 등도 원형). Cylinder 높이 = scale.y * 2 → 두께 4cm, 지름 90cm
            const float PanelDiameter = 0.9f;
            var panel = Cylinder(lighting, "CeilingPanel", new Vector3(1.1f, Height - 0.02f, 1.3f),
                new Vector3(PanelDiameter, 0.02f, PanelDiameter), mPanel, collider: false);
            panel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var controller = lighting.AddComponent<RoomLightController>();
            controller.moonLight = moon;
            controller.ceilingLight = ceil;
            controller.ceilingPanel = panel.GetComponent<Renderer>();

            var sw = lighting.AddComponent<LightSwitchInput>();
            sw.room = controller;

            // 게임 시각 5시 30분(판 끝나기 45초 전)부터 창밖에 해가 뜸 (11/18)
            var sunrise = lighting.AddComponent<SunriseController>();
            sunrise.startHour = 5.5f;
            sunrise.room = controller;
            sunrise.sky = skyRenderer;
            sunrise.roundClock = Object.FindFirstObjectByType<RoundClock>(FindObjectsInactive.Include);

            // ---------- 렌더 설정 (씬 단위) ----------
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = controller.ambientOffColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;

            // ---------- XR Origin ----------
            var origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[TestBedroom] XR Origin 프리팹을 찾지 못했습니다: {XROriginPrefabPath}");
                }
                else
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(go, "Add XR Origin");
                    origin = go.GetComponent<XROrigin>();
                }
            }

            Camera xrCam = null;
            if (origin != null)
            {
                Undo.RecordObject(origin.transform, "Place XR Origin");
                origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Undo.RecordObject(origin, "XR Origin Floor");
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                PrefabUtility.RecordPrefabInstancePropertyModifications(origin);

                // 이동 없음: 이동 / 회전 / 텔레포트 / 중력 / 등반 Provider 끄기
                foreach (var p in origin.GetComponentsInChildren<LocomotionProvider>(true))
                {
                    Undo.RecordObject(p, "Disable Locomotion");
                    p.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(p);
                }
                // 몸 캡슐이 침대에 밀려 시점이 올라가지 않게 + 실제 앉은 자리를 침대 위로 맞춤 (11/18)
                if (origin.GetComponent<StationaryPlayer>() == null)
                    Undo.AddComponent<StationaryPlayer>(origin.gameObject);

                xrCam = origin.Camera != null ? origin.Camera : origin.GetComponentInChildren<Camera>(true);
                if (xrCam != null)
                {
                    Undo.RecordObject(xrCam, "XR Camera Background");
                    xrCam.clearFlags = CameraClearFlags.SolidColor;
                    xrCam.backgroundColor = Color.black;
                    xrCam.nearClipPlane = 0.01f; // 귓가 모기 / 가까운 손이 잘리지 않게
                    PrefabUtility.RecordPrefabInstancePropertyModifications(xrCam);
                }
            }

            // XR Origin 바깥의 카메라, 기존 Directional Light 끄기
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (origin != null && cam.transform.IsChildOf(origin.transform)) continue;
                Undo.RecordObject(cam.gameObject, "Disable extra camera");
                cam.gameObject.SetActive(false);
                Debug.Log($"[TestBedroom] XR Origin 바깥 카메라 '{cam.name}' 를 비활성화했습니다.");
            }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.transform.IsChildOf(root.transform) || l.type != LightType.Directional) continue;
                Undo.RecordObject(l.gameObject, "Disable extra light");
                l.gameObject.SetActive(false);
                Debug.Log($"[TestBedroom] 기존 조명 '{l.name}' 를 비활성화했습니다.");
            }

            // ---------- 밝기 튜너 (테스트용) ----------
            var tunerGO = Child(root, "LightingTuner (TEST)");
            var tuner = tunerGO.AddComponent<LightingTuner>();
            tuner.room = controller;
            if (xrCam != null)
            {
                var labelGO = new GameObject("TunerLabel");
                Undo.RegisterCreatedObjectUndo(labelGO, "Tuner Label");
                labelGO.transform.SetParent(xrCam.transform, false);
                labelGO.transform.localPosition = new Vector3(0f, -0.28f, 1.0f);
                labelGO.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
                var tmp = labelGO.AddComponent<TextMeshPro>();
                tmp.fontSize = 0.4f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.3f, 1f, 0.4f);
                tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.3f);
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.text = "LIGHT OFF";
                tuner.label = tmp;
            }

            foreach (Transform child in root.transform)
                if (keepOff.Contains(child.name)) child.gameObject.SetActive(false);
            if (keepOff.Count > 0)
                Debug.Log($"[TestBedroom] 꺼 두었던 그룹을 꺼진 채로 유지: {string.Join(", ", keepOff)}");

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("[TestBedroom] 테스트 침실 생성 완료. 씬을 저장(Ctrl+S)하세요.");
        }

        // ================= 창밖 / 그림자 =================

        /// <summary>창밖이 검은 허공으로 보이지 않게 밤하늘 면 (그림자 X, 달빛을 막지 않음)</summary>
        static Renderer BuildNightSky(GameObject room)
        {
            var mSky = UnlitMat("NightSky", new Color(0.05f, 0.08f, 0.18f));
            var outside = Child(room, "Outside");
            float x = Left - Wall - 1.5f;
            var sky = Box(outside, "NightSky", new Vector3(x, Height * 0.5f, (Back + Front) * 0.5f),
                new Vector3(0.02f, 6f, 8f), mSky, collider: false);
            var r = sky.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            return r;
        }

        /// <summary>그림자 거리를 방 크기에 맞게 줄임 (50m → 8m: 3m 방에서 그림자가 선명, Quest 부담 감소)</summary>
        static void SetShadowDistance(float distance)
        {
            var assets = new HashSet<UniversalRenderPipelineAsset>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset a) assets.Add(a);
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset d) assets.Add(d);

            foreach (var a in assets)
            {
                if (Mathf.Approximately(a.shadowDistance, distance)) continue;
                Undo.RecordObject(a, "Shadow distance");
                Debug.Log($"[TestBedroom] {a.name} 그림자 거리 {a.shadowDistance} → {distance}m");
                a.shadowDistance = distance;
                EditorUtility.SetDirty(a);
            }
        }

        // ================= 벽 / 문 =================

        static readonly Side[] AllSides = { Side.Back, Side.Front, Side.Left, Side.Right };

        /// <summary>벽에 뚫을 사각 구멍 (벽 방향 좌표 a0~a1, 높이 y0~y1)</summary>
        struct Opening
        {
            public Side side;
            public float a0, a1, y0, y1;
            public string label;
        }

        /// <summary>
        /// 벽 4면을 만든다. 씬에 놓인 문 / 창문 프리팹 크기에 맞춰 구멍을 뚫는다.
        /// 창문을 찾으면 true 를 돌려준다 (가짜 창문을 만들지 않도록).
        /// </summary>
        static bool BuildWalls(GameObject room, Material mat)
        {
            var openings = new List<Opening>();

            var door = FindPrefabInstance(DoorPackPath, null, "문");
            if (door != null) AddOpening(openings, door, true, "문");

            var window = FindPrefabInstance(WindowPackPath, "Window", "창문");
            bool hasWindow = window != null && AddOpening(openings, window, false, "창문");
            if (hasWindow) NoGlassShadows(window);

            foreach (var side in AllSides)
            {
                var mine = openings.FindAll(o => o.side == side);
                if (mine.Count == 0)
                {
                    WallPiece(room, WallName(side), side, AlongMin(side), AlongMax(side), 0f, Height, mat);
                    continue;
                }

                var labels = string.Join(", ", mine.ConvertAll(o => o.label));
                var wall = Child(room, $"{WallName(side)} ({labels})");
                BuildWallWithOpenings(wall, side, mine, mat);
            }
            return hasWindow;
        }

        /// <summary>문/창문을 가장 가까운 벽 두께 가운데로 맞추고, 그 크기만큼 구멍 정보를 추가</summary>
        static bool AddOpening(List<Opening> list, GameObject go, bool fromFloor, string kind)
        {
            if (!TryGetBounds(go, out var b)) return false;
            var side = NearestSide(b.center);
            SnapIntoWall(go, side, ref b);

            bool alongX = AlongX(side);
            var o = new Opening
            {
                side = side,
                a0 = (alongX ? b.min.x : b.min.z) + DoorOverlap,
                a1 = (alongX ? b.max.x : b.max.z) - DoorOverlap,
                y0 = fromFloor ? 0f : Mathf.Max(0f, b.min.y + DoorOverlap),
                y1 = Mathf.Min(b.max.y - DoorOverlap, Height),
                label = kind,
            };

            if (o.a0 < AlongMin(side) || o.a1 > AlongMax(side) || o.a1 - o.a0 < 0.2f || o.y1 - o.y0 < 0.2f)
            {
                Debug.LogWarning($"[TestBedroom] {kind} '{go.name}' 이(가) {side} 벽 범위를 벗어나서 구멍을 뚫지 않았습니다. " +
                                 $"벽 안쪽으로 옮긴 뒤 다시 실행하세요. (구멍 {o.a0:0.00}~{o.a1:0.00}, 벽 {AlongMin(side):0.00}~{AlongMax(side):0.00})");
                return false;
            }

            list.Add(o);
            Debug.Log($"[TestBedroom] {side} 벽에 {kind} 구멍: 폭 {o.a1 - o.a0:0.000}m, 높이 {o.y0:0.000}~{o.y1:0.000}m ('{go.name}' 기준)");
            return true;
        }

        /// <summary>
        /// 구멍 여러 개가 있는 벽. 벽 방향으로 구멍 경계마다 세로 띠로 자르고,
        /// 띠마다 구멍이 없는 높이 구간만 벽 조각으로 채운다.
        /// </summary>
        static void BuildWallWithOpenings(GameObject parent, Side side, List<Opening> ops, Material mat)
        {
            var xs = new List<float> { AlongMin(side), AlongMax(side) };
            foreach (var o in ops) { xs.Add(o.a0); xs.Add(o.a1); }
            xs.Sort();

            int n = 0;
            for (int i = 0; i < xs.Count - 1; i++)
            {
                float a0 = xs[i], a1 = xs[i + 1];
                if (a1 - a0 < 0.001f) continue;
                float mid = (a0 + a1) * 0.5f;

                var holes = ops.FindAll(o => mid > o.a0 && mid < o.a1);
                holes.Sort((p, q) => p.y0.CompareTo(q.y0));
                float y = 0f;
                foreach (var h in holes)
                {
                    if (h.y0 - y > 0.001f) WallPiece(parent, $"Piece_{n++}", side, a0, a1, y, h.y0, mat);
                    y = Mathf.Max(y, h.y1);
                }
                if (Height - y > 0.001f) WallPiece(parent, $"Piece_{n++}", side, a0, a1, y, Height, mat);
            }
        }

        /// <summary>유리(반투명) 부분은 그림자를 드리우지 않게 → 달빛이 유리를 통과</summary>
        static void NoGlassShadows(GameObject window)
        {
            int count = 0;
            foreach (var r in window.GetComponentsInChildren<Renderer>(true))
            {
                bool transparent = false;
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.renderQueue >= (int)RenderQueue.GeometryLast) transparent = true;
                if (!transparent || r.shadowCastingMode == ShadowCastingMode.Off) continue;
                Undo.RecordObject(r, "Glass no shadow");
                r.shadowCastingMode = ShadowCastingMode.Off;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                count++;
            }
            if (count > 0) Debug.Log($"[TestBedroom] 창문 유리 {count}개의 그림자를 껐습니다 (달빛 통과).");
        }

        static GameObject WallPiece(GameObject parent, string name, Side side, float a0, float a1, float y0, float y1, Material mat)
        {
            float ac = (a0 + a1) / 2f, al = a1 - a0, yc = (y0 + y1) / 2f, yl = y1 - y0, p = PerpCenter(side);
            bool alongX = AlongX(side);
            var pos = alongX ? new Vector3(ac, yc, p) : new Vector3(p, yc, ac);
            var size = alongX ? new Vector3(al, yl, Wall) : new Vector3(Wall, yl, al);
            return Box(parent, name, pos, size, mat);
        }

        static bool AlongX(Side s) => s == Side.Back || s == Side.Front;
        static float AlongMin(Side s) => AlongX(s) ? Left : Back;
        static float AlongMax(Side s) => AlongX(s) ? Right : Front;

        /// <summary>벽 두께의 가운데 좌표 (벽에 수직인 축)</summary>
        static float PerpCenter(Side s) => s switch
        {
            Side.Back => Back - Wall / 2,
            Side.Front => Front + Wall / 2,
            Side.Left => Left - Wall / 2,
            _ => Right + Wall / 2,
        };

        static string WallName(Side s) => s switch
        {
            Side.Back => "Wall_Back (Head)",
            Side.Front => "Wall_Front",
            Side.Left => "Wall_Left",
            _ => "Wall_Right",
        };

        static Side NearestSide(Vector3 c)
        {
            Side best = Side.Right;
            float bestDist = float.MaxValue;
            foreach (var s in AllSides)
            {
                float dist = Mathf.Abs((AlongX(s) ? c.z : c.x) - PerpCenter(s));
                if (dist < bestDist) { bestDist = dist; best = s; }
            }
            return best;
        }

        /// <summary>문/창문을 벽 두께 가운데로 옮김 (벽과 나란한 방향 위치와 높이는 그대로)</summary>
        static void SnapIntoWall(GameObject go, Side side, ref Bounds b)
        {
            float delta = PerpCenter(side) - (AlongX(side) ? b.center.z : b.center.x);
            if (Mathf.Abs(delta) < 0.0005f) return;

            var move = AlongX(side) ? new Vector3(0, 0, delta) : new Vector3(delta, 0, 0);
            Undo.RecordObject(go.transform, "Snap into wall");
            go.transform.position += move;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            b.center += move;
            Debug.Log($"[TestBedroom] '{go.name}' 을(를) {side} 벽 두께 가운데로 {delta:+0.000;-0.000}m 옮겼습니다.");
        }

        /// <summary>씬에서 packPath 아래 프리팹으로 놓은 오브젝트를 찾음 (nameContains 가 있으면 프리팹 이름도 확인)</summary>
        static GameObject FindPrefabInstance(string packPath, string nameContains, string kind)
        {
            GameObject found = null;
            int count = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
                if (string.IsNullOrEmpty(path) || !path.StartsWith(packPath)) continue;
                if (nameContains != null && !Path.GetFileName(path).Contains(nameContains)) continue;
                count++;
                if (found == null) found = go;
            }

            if (count > 1)
                Debug.LogWarning($"[TestBedroom] {kind}이(가) {count}개 있습니다. '{found.name}' 하나에만 벽 구멍을 만듭니다.");
            if (found == null)
                Debug.Log($"[TestBedroom] 씬에 {kind} 프리팹이 없습니다.");
            return found;
        }

        static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        // ================= helpers =================

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static GameObject Box(GameObject parent, string name, Vector3 pos, Vector3 size, Material mat,
            bool collider = true, bool local = false)
            => Primitive(PrimitiveType.Cube, parent, name, pos, size, mat, collider, local);

        static GameObject Cylinder(GameObject parent, string name, Vector3 pos, Vector3 size, Material mat,
            bool collider = true)
            => Primitive(PrimitiveType.Cylinder, parent, name, pos, size, mat, collider, false);

        static GameObject Sphere(GameObject parent, string name, Vector3 pos, Material mat)
        {
            // 실제 모기보다 크게 (지름 3cm)
            var go = Primitive(PrimitiveType.Sphere, parent, name, pos, Vector3.one * 0.03f, mat, false, false);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject Primitive(PrimitiveType type, GameObject parent, string name, Vector3 pos, Vector3 size,
            Material mat, bool collider, bool local)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            if (local) go.transform.localPosition = pos;
            else go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = false;
            return go;
        }

        static Material LitMat(string name, Color color)
        {
            var mat = GetOrCreateMat(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material UnlitMat(string name, Color color)
        {
            var mat = GetOrCreateMat(name, "Universal Render Pipeline/Unlit");
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material GetOrCreateMat(string name, string shaderName)
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                Directory.CreateDirectory(MatDir);
                AssetDatabase.Refresh();
            }

            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }
    }
}
