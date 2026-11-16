using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 메뉴 AeAeng > Build Test Bedroom : 현재 열린 씬에 대충 만든 침실 + 방 불 + 달빛 + XR Origin 을 만든다.
    /// 다시 실행하면 기존 TestBedroom 을 지우고 새로 만든다.
    ///
    /// 좌표 기준 (XR Origin = 원점, 플레이어는 +Z 를 바라봄)
    ///   뒤(머리 쪽) 벽 z = -0.45 / 왼쪽 벽 x = -0.6 / 오른쪽 x = 3.4 / 앞 z = 3.6 / 천장 y = 2.4
    /// </summary>
    public static class TestBedroomBuilder
    {
        const string RootName = "TestBedroom";
        const string MatDir = "Assets/AeAeng/Materials/TestBedroom";
        const string XROriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        const float Left = -0.6f, Right = 3.4f, Back = -0.45f, Front = 3.6f, Height = 2.4f, Wall = 0.1f;

        [MenuItem("AeAeng/Build Test Bedroom")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Test Bedroom");

            // ---------- 머티리얼 ----------
            var mWall = LitMat("Wall", new Color(0.78f, 0.76f, 0.72f));
            var mFloor = LitMat("Floor", new Color(0.45f, 0.33f, 0.24f));
            var mCeiling = LitMat("Ceiling", new Color(0.85f, 0.85f, 0.83f));
            var mWood = LitMat("Wood", new Color(0.50f, 0.36f, 0.25f));
            var mMattress = LitMat("Mattress", new Color(0.88f, 0.88f, 0.86f));
            var mBlanket = LitMat("Blanket", new Color(0.35f, 0.45f, 0.65f));
            var mPillow = LitMat("Pillow", new Color(0.92f, 0.92f, 0.90f));
            var mDark = LitMat("DarkPlastic", new Color(0.10f, 0.10f, 0.11f));
            var mLampShade = LitMat("LampShade", new Color(0.90f, 0.85f, 0.70f));
            var mMosquito = LitMat("MosquitoDummy", new Color(0.12f, 0.12f, 0.12f));
            var mWindow = UnlitMat("WindowMoon", new Color(0.16f, 0.22f, 0.38f));
            var mClock = UnlitMat("ClockDigits", new Color(0.85f, 0.08f, 0.05f));
            var mPanel = UnlitMat("CeilingPanel", new Color(0.95f, 0.97f, 1f));

            // ---------- 방 ----------
            var room = Child(root, "Room");
            float w = Right - Left, d = Front - Back, cx = (Left + Right) / 2f, cz = (Back + Front) / 2f;
            Box(room, "Floor", new Vector3(cx, -Wall / 2, cz), new Vector3(w + Wall * 2, Wall, d + Wall * 2), mFloor);
            Box(room, "Ceiling", new Vector3(cx, Height + Wall / 2, cz), new Vector3(w + Wall * 2, Wall, d + Wall * 2), mCeiling);
            Box(room, "Wall_Back (Head)", new Vector3(cx, Height / 2, Back - Wall / 2), new Vector3(w, Height, Wall), mWall);
            Box(room, "Wall_Front", new Vector3(cx, Height / 2, Front + Wall / 2), new Vector3(w, Height, Wall), mWall);
            Box(room, "Wall_Left", new Vector3(Left - Wall / 2, Height / 2, cz), new Vector3(Wall, Height, d), mWall);
            Box(room, "Wall_Right", new Vector3(Right + Wall / 2, Height / 2, cz), new Vector3(Wall, Height, d), mWall);

            // 창문 (왼쪽 벽, 달빛 느낌의 Unlit 면 + 창살)
            var window = Child(room, "Window");
            Box(window, "Glass", new Vector3(Left + 0.01f, 1.45f, 1.5f), new Vector3(0.02f, 1.0f, 0.9f), mWindow, collider: false);
            Box(window, "Bar_V", new Vector3(Left + 0.025f, 1.45f, 1.5f), new Vector3(0.02f, 1.04f, 0.04f), mDark, collider: false);
            Box(window, "Bar_H", new Vector3(Left + 0.025f, 1.45f, 1.5f), new Vector3(0.02f, 0.04f, 0.94f), mDark, collider: false);
            Box(window, "Frame_Top", new Vector3(Left + 0.025f, 1.97f, 1.5f), new Vector3(0.03f, 0.04f, 0.98f), mWood, collider: false);
            Box(window, "Frame_Bottom", new Vector3(Left + 0.025f, 0.93f, 1.5f), new Vector3(0.05f, 0.04f, 0.98f), mWood, collider: false);

            // ---------- 가구 ----------
            var furniture = Child(root, "Furniture");
            var bed = Child(furniture, "Bed");
            Box(bed, "Frame", new Vector3(-0.05f, 0.15f, 0.575f), new Vector3(1.1f, 0.3f, 2.05f), mWood);
            Box(bed, "Mattress", new Vector3(-0.05f, 0.4f, 0.575f), new Vector3(1.05f, 0.2f, 2.0f), mMattress);
            Box(bed, "Blanket", new Vector3(-0.05f, 0.53f, 0.95f), new Vector3(1.08f, 0.06f, 1.25f), mBlanket);
            Box(bed, "Pillow", new Vector3(-0.05f, 0.56f, -0.2f), new Vector3(0.6f, 0.12f, 0.35f), mPillow);
            Box(bed, "Headboard", new Vector3(-0.05f, 0.5f, Back + 0.03f), new Vector3(1.1f, 1.0f, 0.06f), mWood);

            var nightstand = Child(furniture, "Nightstand");
            Box(nightstand, "Body", new Vector3(0.8f, 0.25f, 0.0f), new Vector3(0.45f, 0.5f, 0.4f), mWood);
            // 시계 화면은 침대 쪽(-X)을 바라봄
            var clock = Box(nightstand, "DigitalClock", new Vector3(0.72f, 0.545f, 0.08f), new Vector3(0.06f, 0.09f, 0.18f), mDark);
            Box(clock, "Digits", new Vector3(-0.51f, 0f, 0f), new Vector3(0.02f, 0.6f, 0.8f), mClock, collider: false, local: true);
            Box(nightstand, "Lamp_Base", new Vector3(0.9f, 0.51f, -0.08f), new Vector3(0.1f, 0.02f, 0.1f), mDark);
            Box(nightstand, "Lamp_Stem", new Vector3(0.9f, 0.62f, -0.08f), new Vector3(0.02f, 0.2f, 0.02f), mDark);
            Cylinder(nightstand, "Lamp_Shade", new Vector3(0.9f, 0.76f, -0.08f), new Vector3(0.18f, 0.07f, 0.18f), mLampShade);

            var stand = Child(furniture, "SwatterStand (placeholder)");
            Box(stand, "Base", new Vector3(0.75f, 0.01f, 0.45f), new Vector3(0.2f, 0.02f, 0.2f), mDark);
            Box(stand, "Post", new Vector3(0.75f, 0.45f, 0.45f), new Vector3(0.03f, 0.9f, 0.03f), mDark);

            // ---------- 모기 대용 (밝기 확인용 작은 구) ----------
            var mosq = Child(root, "MosquitoDummies");
            Sphere(mosq, "Dummy_LeftWall_Low", new Vector3(Left + 0.015f, 1.15f, 0.45f), mMosquito);
            Sphere(mosq, "Dummy_LeftWall_High", new Vector3(Left + 0.015f, 1.85f, 0.9f), mMosquito);
            Sphere(mosq, "Dummy_Blanket", new Vector3(0.15f, 0.575f, 1.15f), mMosquito);
            Sphere(mosq, "Dummy_LampShade", new Vector3(0.9f, 0.76f, 0.025f), mMosquito);
            Sphere(mosq, "Dummy_Ceiling", new Vector3(0.4f, Height - 0.015f, 0.8f), mMosquito);

            // ---------- 조명 ----------
            var lighting = Child(root, "Lighting");

            var moonGO = Child(lighting, "MoonLight");
            moonGO.transform.rotation = Quaternion.Euler(30f, 100f, 0f); // 왼쪽 창 → 방 안쪽(+X)으로
            var moon = moonGO.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.intensity = 0.1f;
            moon.shadows = LightShadows.None; // 닫힌 상자라 그림자를 켜면 방 전체가 가려짐

            var ceilGO = Child(lighting, "CeilingLight");
            ceilGO.transform.position = new Vector3(1.1f, Height - 0.15f, 1.3f);
            var ceil = ceilGO.AddComponent<Light>();
            ceil.type = LightType.Point;
            ceil.color = new Color(0.95f, 0.98f, 1f);
            ceil.range = 7f;
            ceil.intensity = 1f;
            ceil.shadows = LightShadows.None;

            var panel = Box(lighting, "CeilingPanel", new Vector3(1.1f, Height - 0.02f, 1.3f), new Vector3(1.2f, 0.04f, 0.3f), mPanel, collider: false);
            panel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var controller = lighting.AddComponent<RoomLightController>();
            controller.moonLight = moon;
            controller.ceilingLight = ceil;
            controller.ceilingPanel = panel.GetComponent<Renderer>();

            var sw = lighting.AddComponent<LightSwitchInput>();
            sw.room = controller;

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

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("[TestBedroom] 테스트 침실 생성 완료. 씬을 저장(Ctrl+S)하세요.");
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

        static GameObject Cylinder(GameObject parent, string name, Vector3 pos, Vector3 size, Material mat)
            => Primitive(PrimitiveType.Cylinder, parent, name, pos, size, mat, true, false);

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
