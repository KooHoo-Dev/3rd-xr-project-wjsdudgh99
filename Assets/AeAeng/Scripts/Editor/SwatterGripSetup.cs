using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 오른손 프리팹에 파리채를 쥐는 기준점(SwatterGrip)을 만든다. (AeAeng > Setup Swatter Grip)
    /// 파리채를 잡으면 SwatterGrab 이 파리채의 GripPoint 를 이 점에 맞춰 손에 붙인다.
    /// 손 좌표: 원점 = 손목, +Z = 손가락, +Y = 손등, 오른손 엄지 = -X.
    /// 미리보기: Temp/SwatterGripPreview/ (쥔 손 + 파리채, 회색)
    /// </summary>
    public static class SwatterGripSetup
    {
        public const string GripName = "SwatterGrip";
        const string RightHandPath = "Assets/AeAeng/Prefabs/RightHand.prefab";
        const string SwatterPath = "Assets/AeAeng/Prefabs/FlySwatter.prefab";
        const string PreviewDir = "Temp/SwatterGripPreview";

        /// <summary>주먹 안 손잡이 중심 (손목 기준, m)</summary>
        // 1차(-0.005, -0.03, 0.075): 손잡이가 말린 손가락 바깥(손바닥 쪽)으로 지나감 → 손등 쪽으로 1.2cm, 손목 쪽으로 0.3cm
        // 2차(-0.005, -0.018, 0.072), 35°: 손잡이가 손바닥 가운데를 가로지름 → 실제처럼 손가락 뿌리 주름 쪽으로, 덜 비스듬하게
        public static readonly Vector3 GripPosition = new Vector3(-0.005f, -0.02f, 0.085f);
        /// <summary>파리채 머리가 엄지 쪽(-X)에서 손가락 쪽(+Z)으로 기운 각도. 0 = 손바닥을 가로질러 엄지 쪽으로 곧게</summary>
        public const float HeadTilt = 25f;

        [MenuItem("AeAeng/Setup Swatter Grip (Right Hand)")]
        public static void Setup()
        {
            var root = PrefabUtility.LoadPrefabContents(RightHandPath);
            if (root == null)
            {
                Debug.LogError($"[SwatterGrip] {RightHandPath} 이 없습니다. AeAeng > Build Hands 를 먼저 실행하세요.");
                return;
            }
            ApplyGrip(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, RightHandPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();

            string preview = RenderPreview();
            Debug.Log($"[SwatterGrip] 오른손에 {GripName} 설정 (위치 {GripPosition}, 기울기 {HeadTilt}°), preview {preview}");
        }

        /// <summary>손 루트 아래에 SwatterGrip 을 만들거나 위치를 갱신 (HandBuilder 에서도 호출)</summary>
        public static Transform ApplyGrip(Transform handRoot)
        {
            var grip = handRoot.Find(GripName);
            if (grip == null)
            {
                grip = new GameObject(GripName).transform;
                grip.SetParent(handRoot, false);
            }
            float r = HeadTilt * Mathf.Deg2Rad;
            // forward = 파리채 머리 방향 (엄지 쪽 → 손가락 쪽으로 기움)
            // up = 파리채 면이 바라보는 방향 (머리 방향에 수직, 손가락/주먹 앞쪽) → 앞으로 내려치면 면으로 때림
            var forward = new Vector3(-Mathf.Cos(r), 0f, Mathf.Sin(r));
            var up = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            grip.localPosition = GripPosition;
            grip.localRotation = Quaternion.LookRotation(forward, up);
            grip.localScale = Vector3.one;
            return grip;
        }

        // ------------------------------------------------------------------
        // 미리보기
        // ------------------------------------------------------------------

        static string RenderPreview()
        {
            var handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RightHandPath);
            var swatterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwatterPath);
            if (handPrefab == null || swatterPrefab == null) return "skipped";

            var pru = new PreviewRenderUtility();
            var grey = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            grey.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.75f));
            try
            {
                Directory.CreateDirectory(PreviewDir);
                var cam = pru.camera;
                cam.nearClipPlane = 0.005f;
                cam.farClipPlane = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.45f, 0.5f, 0.58f);
                pru.lights[0].intensity = 1.1f;
                pru.lights[0].transform.rotation = Quaternion.Euler(50f, 30f, 0f);
                pru.lights[1].intensity = 0.6f;
                pru.ambientColor = new Color(0.4f, 0.4f, 0.42f);

                var hand = pru.InstantiatePrefabInScene(handPrefab);
                foreach (var smr in hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.sharedMaterials = Enumerable.Repeat(grey, smr.sharedMaterials.Length).ToArray();
                hand.GetComponent<HandPose>().ApplyImmediate(1f, 1f); // 트리거를 끝까지 = 주먹

                var grip = hand.transform.Find(GripName);
                var swatter = pru.InstantiatePrefabInScene(swatterPrefab);
                var gp = swatter.transform.Find("GripPoint");
                var rot = grip.rotation * Quaternion.Inverse(gp.localRotation);
                swatter.transform.SetPositionAndRotation(grip.position - rot * gp.localPosition, rot);

                var c = grip.position;
                Shot(pru, "1_back", c + new Vector3(0f, 0.32f, 0f), c, Vector3.forward);     // 손등 위에서
                Shot(pru, "2_palm", c + new Vector3(0f, -0.32f, 0f), c, Vector3.forward);    // 손바닥 아래에서
                Shot(pru, "3_thumb", c + new Vector3(-0.32f, 0f, 0f), c, Vector3.up);        // 엄지 쪽에서
                Shot(pru, "4_pinky", c + new Vector3(0.32f, 0f, 0f), c, Vector3.up);         // 새끼 쪽에서
                Shot(pru, "5_wide", c + new Vector3(0.35f, 0.45f, -0.55f), c + new Vector3(-0.12f, 0f, 0.1f), Vector3.up);
                return Path.GetFullPath(PreviewDir);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SwatterGrip] preview failed: {e}");
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
    }
}
