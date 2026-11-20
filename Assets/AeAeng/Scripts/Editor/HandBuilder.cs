using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 컨트롤러 대신 보일 손 프리팹을 만들고 XR Origin의 컨트롤러 아래에 붙인다. (AeAeng > Build Hands)
    /// 손 모델: XR Hands 패키지 샘플(HandVisualizer)의 LeftHand.fbx / RightHand.fbx를 Models/Hands로 복사한 것.
    /// 재질: AeAeng/GhostHand (가장자리만 푸르게 빛나는 유령 손, 가산 블렌딩).
    /// 프리팹 좌표: 원점 = 손목, +Z = 손가락 방향, +Y = 손등.
    /// </summary>
    public static class HandBuilder
    {
        const string ModelDir = "Assets/AeAeng/Models/Hands";
        const string MatPath = "Assets/AeAeng/Materials/Hands/GhostHand.mat";
        const string PrefabDir = "Assets/AeAeng/Prefabs";
        const string PreviewDir = "Temp/HandPreview";

        // 컨트롤러(조준 자세) 기준 손목 위치/각도. 헤드셋에서 보고 씬의 LeftHand/RightHand Transform을 직접 조정해도 됨.
        // 다시 실행해도 이미 붙어 있는 손의 위치는 바꾸지 않는다.
        static readonly Vector3 WristOffset = new Vector3(0f, -0.035f, -0.11f);
        // 11/17 헤드셋 확인: 20°는 손가락이 위를 향함 → 45°로. 정확한 값은 HandOffsetTuner(11/20 삭제)로 맞춘 뒤 반영
        const float WristPitch = 45f;

        static readonly Color RimColor = new Color(0.25f, 0.69f, 0.80f);
        static readonly Color FillColor = new Color(0.02f, 0.05f, 0.06f);

        [MenuItem("AeAeng/Build Hands")]
        public static void Build()
        {
            EnsureFolder(Path.GetDirectoryName(MatPath).Replace('\\', '/'));
            EnsureFolder(PrefabDir);

            var mat = GhostMaterial();
            var left = BuildHand(HandPose.Side.Left, mat);
            var right = BuildHand(HandPose.Side.Right, mat);
            AssetDatabase.SaveAssets();
            if (left == null || right == null) return;

            string attached = AttachToControllers(left, right);
            string preview = RenderPreviews(left, right);
            Debug.Log($"[HandBuilder] hands built. {attached}, preview {preview}");
        }

        // ------------------------------------------------------------------
        // 프리팹
        // ------------------------------------------------------------------

        static GameObject BuildHand(HandPose.Side side, Material mat)
        {
            string sideName = side == HandPose.Side.Left ? "Left" : "Right";
            string p = side == HandPose.Side.Left ? "L_" : "R_";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{sideName}Hand.fbx");
            if (fbx == null)
            {
                Debug.LogError($"[HandBuilder] {ModelDir}/{sideName}Hand.fbx 가 없습니다.");
                return null;
            }

            var root = new GameObject($"{sideName}Hand");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);

            Transform Find(string bone)
            {
                var t = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == p + bone);
                if (t == null) Debug.LogError($"[HandBuilder] 뼈 {p}{bone} 를 찾지 못했습니다.");
                return t;
            }

            var wrist = Find("Wrist");
            var indexProx = Find("IndexProximal");
            var middleProx = Find("MiddleProximal");
            var littleProx = Find("LittleProximal");
            if (wrist == null || indexProx == null || middleProx == null || littleProx == null)
            {
                Object.DestroyImmediate(root);
                return null;
            }

            // 손목을 원점에, 손가락 = +Z, 손등 = +Y가 되도록 모델을 돌린다
            var fingers = (middleProx.position - wrist.position).normalized;
            var back = Vector3.Cross(indexProx.position - wrist.position, littleProx.position - wrist.position).normalized
                       * (side == HandPose.Side.Right ? 1f : -1f);
            var frame = Quaternion.LookRotation(fingers, back);
            model.transform.localRotation = Quaternion.Inverse(frame);
            model.transform.localPosition = -(Quaternion.Inverse(frame) * wrist.position);

            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.sharedMaterials = Enumerable.Repeat(mat, smr.sharedMaterials.Length).ToArray();
                smr.shadowCastingMode = ShadowCastingMode.Off;
                smr.receiveShadows = false;
                smr.lightProbeUsage = LightProbeUsage.Off;
                smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                smr.updateWhenOffscreen = false;
            }

            var pose = root.AddComponent<HandPose>();
            pose.side = side;
            pose.wrist = wrist;
            pose.thumb = MakeFinger(Find, "Thumb", new[] { "Metacarpal", "Proximal", "Distal" }, new[] { 20f, 35f, 45f });
            pose.index = MakeFinger(Find, "Index", new[] { "Proximal", "Intermediate", "Distal" }, new[] { 70f, 90f, 60f });
            pose.middle = MakeFinger(Find, "Middle", new[] { "Proximal", "Intermediate", "Distal" }, new[] { 70f, 90f, 60f });
            pose.ring = MakeFinger(Find, "Ring", new[] { "Proximal", "Intermediate", "Distal" }, new[] { 70f, 90f, 60f });
            pose.little = MakeFinger(Find, "Little", new[] { "Proximal", "Intermediate", "Distal" }, new[] { 70f, 90f, 60f });

            if (side == HandPose.Side.Left)
            {
                // 왼손: 무선 스위치를 늘 쥔 상태. 검지만 트리거로 스위치 버튼을 누름
                Drive(pose.thumb, HandPose.Source.Fixed, 0.35f, 0.35f);
                Drive(pose.index, HandPose.Source.Trigger, 0.15f, 0.65f);
                Drive(pose.middle, HandPose.Source.Fixed, 0.8f, 0.8f);
                Drive(pose.ring, HandPose.Source.Fixed, 0.8f, 0.8f);
                Drive(pose.little, HandPose.Source.Fixed, 0.8f, 0.8f);
            }
            else
            {
                // 오른손: 중지·약지·새끼는 살짝 말려 있고(컨트롤러를 쥔 느낌) 그립으로 더 쥠, 트리거로 주먹 (맨손 잡기 / 파리채 쥐기)
                Drive(pose.thumb, HandPose.Source.Trigger, 0.1f, 0.7f);
                Drive(pose.index, HandPose.Source.Trigger, 0.1f, 0.95f);
                Drive(pose.middle, HandPose.Source.TriggerOrGrip, 0.4f, 1f);
                Drive(pose.ring, HandPose.Source.TriggerOrGrip, 0.45f, 1f);
                Drive(pose.little, HandPose.Source.TriggerOrGrip, 0.5f, 1f);
                SwatterGripSetup.ApplyGrip(root.transform); // 파리채를 쥐는 기준점
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{sideName}Hand.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static HandPose.Finger MakeFinger(Func<string, Transform> find, string finger, string[] joints, float[] angles)
        {
            return new HandPose.Finger
            {
                bones = joints.Select(j => find(finger + j)).ToArray(),
                tip = find(finger + "Tip"),
                maxAngles = angles,
            };
        }

        static void Drive(HandPose.Finger f, HandPose.Source source, float min, float max)
        {
            f.source = source;
            f.min = min;
            f.max = max;
        }

        static Material GhostMaterial()
        {
            var shader = Shader.Find("AeAeng/GhostHand");
            if (shader == null) Debug.LogError("[HandBuilder] AeAeng/GhostHand 셰이더를 찾지 못했습니다.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "GhostHand" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.SetColor("_RimColor", RimColor);
            mat.SetColor("_FillColor", FillColor);
            mat.SetFloat("_RimPower", 2f);
            mat.SetFloat("_RimScale", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        // XR Origin에 붙이기
        // ------------------------------------------------------------------

        static string AttachToControllers(GameObject leftPrefab, GameObject rightPrefab)
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var leftCtrl = all.FirstOrDefault(t => t.name == "Left Controller");
            var rightCtrl = all.FirstOrDefault(t => t.name == "Right Controller");
            if (leftCtrl == null || rightCtrl == null) return "XR Origin의 Left/Right Controller를 찾지 못해 씬에 붙이지 않음";

            int placed = Attach(leftCtrl, leftPrefab, -1f) + Attach(rightCtrl, rightPrefab, 1f);
            int hidden = HideControllerModel(leftCtrl) + HideControllerModel(rightCtrl);

            // 게임 중 레이 숨김 (XR Origin 루트)
            var origin = leftCtrl.root;
            bool rays = false;
            if (origin.GetComponent<RayVisibility>() == null)
            {
                Undo.AddComponent<RayVisibility>(origin.gameObject);
                rays = true;
            }

            // 11/20: 헤드셋 안 손 위치 튜너(HandOffsetTuner, 테스트용)는 값을 다 맞춰서 스크립트째 삭제함

            EditorSceneManager.MarkSceneDirty(leftCtrl.gameObject.scene);
            return $"attached {placed}, controller models hidden {hidden}, rayVisibility added {rays}";
        }

        /// <summary>씬에 붙은 손의 위치/각도를 HandBuilder 기본값으로 되돌린다.</summary>
        [MenuItem("AeAeng/Reset Hand Position")]
        public static void ResetHandPosition()
        {
            int n = 0;
            foreach (var h in Object.FindObjectsByType<HandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                float mirror = h.side == HandPose.Side.Right ? 1f : -1f;
                Undo.RecordObject(h.transform, "Reset Hand Position");
                SetDefaultPose(h.transform, mirror);
                n++;
            }
            if (n > 0) EditorSceneManager.MarkSceneDirty(SceneManagerActive());
            Debug.Log($"[HandBuilder] 손 {n}개를 기본 위치로 (pitch {WristPitch}, offset {WristOffset}). 씬을 저장하세요.");
        }

        static UnityEngine.SceneManagement.Scene SceneManagerActive() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        static void SetDefaultPose(Transform t, float mirror)
        {
            // 오른손: 손등이 +X(바깥), 왼손: 손등이 -X(바깥). 손가락은 앞쪽에서 WristPitch만큼 아래로
            t.localPosition = new Vector3(WristOffset.x * mirror, WristOffset.y, WristOffset.z);
            t.localRotation = Quaternion.Euler(WristPitch, 0f, -90f * mirror);
        }

        static int Attach(Transform controller, GameObject prefab, float mirror)
        {
            foreach (Transform child in controller)
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == prefab) return 0;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, controller);
            SetDefaultPose(inst.transform, mirror);
            Undo.RegisterCreatedObjectUndo(inst, "Attach Hand");
            return 1;
        }

        /// <summary>Starter Assets의 컨트롤러 모델(XR Controller Left/Right)을 끈다.</summary>
        static int HideControllerModel(Transform controller)
        {
            int n = 0;
            foreach (Transform child in controller)
            {
                // 씬의 XR Origin에서는 "Left/Right Controller Visual"로 이름이 바뀌어 있음
                bool isModel = child.name.Contains("XR Controller") || child.name.Contains("Controller Visual");
                if (!isModel || !child.gameObject.activeSelf) continue;
                Undo.RecordObject(child.gameObject, "Hide Controller Model");
                child.gameObject.SetActive(false);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------
        // 미리보기 (회색 Lit 재질로 펼친 손 / 쥔 손)
        // ------------------------------------------------------------------

        static string RenderPreviews(GameObject left, GameObject right)
        {
            var pru = new PreviewRenderUtility();
            var grey = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            grey.SetColor("_BaseColor", new Color(0.8f, 0.78f, 0.75f));
            try
            {
                Directory.CreateDirectory(PreviewDir);
                var cam = pru.camera;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.45f, 0.5f, 0.58f);
                pru.lights[0].intensity = 1.1f;
                pru.lights[0].transform.rotation = Quaternion.Euler(50f, 30f, 0f);
                pru.lights[1].intensity = 0.6f;
                pru.ambientColor = new Color(0.4f, 0.4f, 0.42f);

                var c = new Vector3(0f, 0f, 0.09f);
                foreach (var (prefab, tag) in new[] { (left, "left"), (right, "right") })
                foreach (var (t, poseTag) in new[] { (0f, "open"), (1f, "closed") })
                {
                    // 스킨 메쉬는 처음 그릴 때의 자세로 남으므로 자세마다 새로 만든다
                    var inst = pru.InstantiatePrefabInScene(prefab);
                    foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        smr.sharedMaterials = Enumerable.Repeat(grey, smr.sharedMaterials.Length).ToArray();
                    inst.GetComponent<HandPose>().ApplyImmediate(t, t);
                    Shot(pru, $"{tag}_{poseTag}_top", c + new Vector3(0f, 0.45f, 0f), c, Vector3.forward);
                    Shot(pru, $"{tag}_{poseTag}_side", c + new Vector3(0.45f, 0f, 0f), c, Vector3.up);
                    Object.DestroyImmediate(inst);
                }
                return Path.GetFullPath(PreviewDir);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HandBuilder] preview failed: {e}");
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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
