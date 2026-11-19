using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 3묶음 손맛과 마무리 (AeAeng > Setup Polish). 플레이 씬(test)에서 실행. 다시 실행해도 됨.
    ///  1) TutorialController (메인 Tutorial 버튼으로 들어왔을 때만 동작)
    ///  2) CatchEffects: 잡을 때 "딱!/퍽!" 글자 + 먼지 파티클
    ///  3) 무기 프리팹(파리채/신문지)에 SwingTrail: 휘두를 때 궤적 + 잡기/놓기 진동
    ///  4) 왼손 무선 스위치 소품 (LightSwitchRemote): 트리거만큼 버튼이 눌리고, 불 상태 LED
    /// 메인 화면 Tutorial 버튼은 AeAeng > Build Scene Flow 를 다시 실행하면 생김.
    /// 스위치 미리보기: Temp/SwitchPropPreview/
    /// </summary>
    public static class PolishSetup
    {
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";
        const string UiMatDir = "Assets/AeAeng/Materials/UI";
        const string PropMatDir = "Assets/AeAeng/Materials/Props";
        const string ParticleMatPath = UiMatDir + "/CatchParticle.mat";
        const string TrailMatPath = UiMatDir + "/SwingTrail.mat";
        const string DotTexPath = "Assets/AeAeng/Textures/UI/PillFill.png"; // 동그란 흰 점 (Setup Play Loop 가 만든 것)
        const string SwatterPrefab = "Assets/AeAeng/Prefabs/FlySwatter.prefab";
        const string NewspaperPrefab = "Assets/AeAeng/Prefabs/Newspaper.prefab";
        const string RemotePrefab = "Assets/AeAeng/Prefabs/LightSwitchRemote.prefab";
        const string LeftHandPrefab = "Assets/AeAeng/Prefabs/LeftHand.prefab";
        const string PreviewDir = "Temp/SwitchPropPreview";

        // ---- 왼손 스위치 위치 (왼손 좌표: 원점 = 손목, +Z = 손가락, +Y = 손등, 왼손 엄지 = +X) ----
        // 주먹(중지·약지·새끼 0.8) 안에 손바닥을 가로질러 쥐고, 버튼은 검지 쪽(+X) 위 끝에서 손가락 방향(+Z)을 봄
        // 1차(0.008, -0.022, 0.08), Z 90°: 버튼 쪽 끝이 새끼손가락 쪽을 향함 → -90° 로 뒤집고, 엄지 쪽으로 덜 튀어나오게 1.3cm 이동
        public static readonly Vector3 RemotePosition = new Vector3(-0.005f, -0.022f, 0.08f);
        public static readonly Vector3 RemoteEuler = new Vector3(0f, 0f, -90f);  // 긴 축(로컬 +Y, 버튼 쪽) → 손 +X (검지·엄지 쪽)
        static readonly Vector3 BodySize = new Vector3(0.032f, 0.085f, 0.016f);  // 폭 3.2 × 길이 8.5 × 두께 1.6cm

        [MenuItem("AeAeng/Setup Polish (Tutorial, Effects, Switch)")]
        public static void Setup()
        {
            EnsureFolder(UiMatDir);
            EnsureFolder(PropMatDir);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var systems = GameObject.Find("GameSystems");
            if (systems == null) { Debug.LogError("[Polish] GameSystems 가 없습니다. 플레이 씬(test)에서 Setup Play Loop 를 먼저 실행하세요."); return; }

            // 1) 튜토리얼
            var tut = systems.GetComponentInChildren<TutorialController>(true);
            if (tut == null) tut = Undo.AddComponent<TutorialController>(new GameObject("Tutorial").SetParentReturn(systems.transform));

            // 2) 잡기 이펙트
            var fx = systems.GetComponentInChildren<CatchEffects>(true);
            if (fx == null) fx = Undo.AddComponent<CatchEffects>(new GameObject("CatchEffects").SetParentReturn(systems.transform));
            fx.font = font;
            fx.particleMaterial = ParticleMaterial();
            EditorUtility.SetDirty(fx);

            // 3) 무기 궤적 + 진동
            int weapons = 0;
            foreach (var path in new[] { SwatterPrefab, NewspaperPrefab })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;
                EnsureSwingTrail(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                weapons++;
            }

            // 4) 왼손 스위치
            var remote = BuildRemotePrefab();
            string attached = AttachRemote(remote);
            string preview = RenderPreview(remote);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(systems.scene);
            Debug.Log($"[Polish] 튜토리얼, 잡기 이펙트, 무기 궤적({weapons}개), 왼손 스위치({attached}) 설정 완료. " +
                      $"메인 Tutorial 버튼은 Build Scene Flow 를 다시 실행하세요. 스위치 미리보기 {preview}");
        }

        static GameObject SetParentReturn(this GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            return go;
        }

        // ------------------------------------------------------------------
        // 무기 궤적
        // ------------------------------------------------------------------

        /// <summary>무기 루트에 SwingTrail (궤적 재질 연결). FlySwatterBuilder / NewspaperBuilder 에서도 호출</summary>
        public static void EnsureSwingTrail(GameObject weaponRoot)
        {
            var trail = weaponRoot.GetComponent<SwingTrail>();
            if (trail == null) trail = weaponRoot.AddComponent<SwingTrail>();
            EnsureFolder(UiMatDir);
            trail.trailMaterial = TrailMaterial();
        }

        // ------------------------------------------------------------------
        // 재질 (URP Particles/Unlit 반투명) — 씬/프리팹에서 참조해야 빌드에 셰이더가 들어감
        // ------------------------------------------------------------------

        static Material ParticleMaterial()
        {
            var mat = UrpParticleMat(ParticleMatPath, "CatchParticle");
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(DotTexPath);
            if (dot != null) mat.SetTexture("_BaseMap", dot);
            return mat;
        }

        static Material TrailMaterial() => UrpParticleMat(TrailMatPath, "SwingTrail");

        static Material UrpParticleMat(string path, string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader) mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);                 // Transparent
            mat.SetFloat("_Blend", 0f);                   // Alpha
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        // 왼손 무선 스위치
        // ------------------------------------------------------------------

        static GameObject BuildRemotePrefab()
        {
            var bodyMat = LitMat("RemoteBody", new Color(0.86f, 0.86f, 0.83f), 0.45f);
            var buttonMat = LitMat("RemoteButton", new Color(0.55f, 0.58f, 0.62f), 0.3f);
            var ledMat = UnlitMat("RemoteLED", new Color(0.35f, 1f, 0.45f));

            var root = new GameObject("LightSwitchRemote");
            var prop = root.AddComponent<SwitchProp>();

            var body = Prim(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, BodySize, bodyMat);
            // 버튼: 위쪽 끝, 앞면(+Z)에 동그란 버튼
            var button = Prim(PrimitiveType.Cylinder, "Button", root.transform,
                new Vector3(0f, BodySize.y * 0.28f, BodySize.z * 0.5f + 0.002f), new Vector3(0.018f, 0.002f, 0.018f), buttonMat);
            button.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 원기둥 축을 +Z 로 (로컬 -Y 로 눌림 → 앞면 안쪽)
            var led = Prim(PrimitiveType.Sphere, "LED", root.transform,
                new Vector3(0f, BodySize.y * 0.43f, BodySize.z * 0.5f + 0.0005f), Vector3.one * 0.004f, ledMat);

            prop.button = button.transform;
            prop.led = led.GetComponent<Renderer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RemotePrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static string AttachRemote(GameObject prefab)
        {
            HandPose left = null;
            foreach (var h in Object.FindObjectsByType<HandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h.side == HandPose.Side.Left) left = h;
            if (left == null) return "왼손 없음";

            foreach (var old in left.GetComponentsInChildren<SwitchProp>(true))
                Undo.DestroyObjectImmediate(old.gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, left.transform);
            inst.transform.localPosition = RemotePosition;
            inst.transform.localRotation = Quaternion.Euler(RemoteEuler);
            Undo.RegisterCreatedObjectUndo(inst, "Attach Remote");
            return "왼손에 붙임";
        }

        static string RenderPreview(GameObject remotePrefab)
        {
            var handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeftHandPrefab);
            if (handPrefab == null) return "skipped";
            var pru = new PreviewRenderUtility();
            var grey = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            grey.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.75f));
            try
            {
                Directory.CreateDirectory(PreviewDir);
                var cam = pru.camera;
                cam.nearClipPlane = 0.005f; cam.farClipPlane = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.45f, 0.5f, 0.58f);
                pru.lights[0].intensity = 1.1f;
                pru.lights[0].transform.rotation = Quaternion.Euler(50f, 30f, 0f);
                pru.lights[1].intensity = 0.6f;
                pru.ambientColor = new Color(0.4f, 0.4f, 0.42f);

                var hand = pru.InstantiatePrefabInScene(handPrefab);
                foreach (var smr in hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.sharedMaterials = Enumerable.Repeat(grey, smr.sharedMaterials.Length).ToArray();
                hand.GetComponent<HandPose>().ApplyImmediate(0f, 0f); // 왼손 기본 자세 (스위치 쥔 손, 검지 살짝)
                var remote = pru.InstantiatePrefabInScene(remotePrefab);
                remote.transform.SetParent(hand.transform, false);
                remote.transform.localPosition = RemotePosition;
                remote.transform.localRotation = Quaternion.Euler(RemoteEuler);

                var c = hand.transform.TransformPoint(RemotePosition);
                Shot(pru, "1_back", c + new Vector3(0f, 0.3f, 0f), c, Vector3.forward);
                Shot(pru, "2_palm", c + new Vector3(0f, -0.3f, 0f), c, Vector3.forward);
                Shot(pru, "3_thumb", c + new Vector3(0.3f, 0f, 0f), c, Vector3.up);
                Shot(pru, "4_front", c + new Vector3(0f, 0f, 0.3f), c, Vector3.up);
                return Path.GetFullPath(PreviewDir);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Polish] preview failed: {e}");
                return "failed";
            }
            finally
            {
                Object.DestroyImmediate(grey);
                pru.Cleanup();
            }
        }

        static void Shot(PreviewRenderUtility pru, string name, Vector3 pos, Vector3 target, Vector3 up)
        {
            pru.BeginStaticPreview(new Rect(0, 0, 768, 768));
            pru.camera.fieldOfView = 30f;
            pru.camera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, up));
            pru.Render(true, true);
            var tex = pru.EndStaticPreview();
            File.WriteAllBytes(Path.Combine(PreviewDir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static Material LitMat(string name, Color color, float smooth)
        {
            var path = $"{PropMatDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material UnlitMat(string name, Color color)
        {
            var path = $"{PropMatDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
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
