using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 귀여운 로우폴리 모기를 코드로 생성한다. (AeAeng > Build Mosquito)
    /// 큰 머리와 큰 눈, 줄무늬 배, 긴 침, 가는 다리 6개, 반투명 날개 2장 (MosquitoWingFlap으로 날갯짓).
    /// 결과물: Models/Mosquito_*.asset, Materials/Mosquito/*.mat, Prefabs/Mosquito.prefab
    /// 좌표: +Z = 머리 쪽, +Y = 등 쪽. 모델 단위로 만든 뒤 ModelScale을 곱함 (전체 길이 약 3cm).
    /// </summary>
    public static class MosquitoBuilder
    {
        const string ModelDir = "Assets/AeAeng/Models";
        const string MatDir = "Assets/AeAeng/Materials/Mosquito";
        const string PrefabDir = "Assets/AeAeng/Prefabs";
        const string PrefabPath = PrefabDir + "/Mosquito.prefab";
        const string PreviewDir = "Temp/MosquitoPreview";

        const float ModelScale = 0.0075f; // 모델 1단위 = 7.5mm

        // 서브메쉬(재질) 번호
        const int SubBody = 0, SubStripe = 1, SubEyeWhite = 2, SubPupil = 3, SubCount = 4;

        static readonly Color BodyColor = new Color(0.30f, 0.32f, 0.40f);
        static readonly Color StripeColor = new Color(0.92f, 0.92f, 0.95f);
        static readonly Color EyeWhiteColor = new Color(1f, 1f, 1f);
        static readonly Color PupilColor = new Color(0.04f, 0.04f, 0.06f);
        static readonly Color WingColor = new Color(0.85f, 0.93f, 1.00f, 0.55f);

        static readonly Vector3 WingRoot = new Vector3(0.24f, 0.30f, 0.05f);

        [MenuItem("AeAeng/Build Mosquito")]
        public static void Build()
        {
            EnsureFolder(ModelDir);
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);

            // ---- 메쉬 ----
            var body = new MeshBuilder(SubCount);
            BuildBody(body);
            var bodyMesh = SaveMesh(body, "Mosquito_Body");

            var wingR = new MeshBuilder(1);
            BuildWing(wingR, 1f);
            var wingRMesh = SaveMesh(wingR, "Mosquito_WingR");

            var wingL = new MeshBuilder(1);
            BuildWing(wingL, -1f);
            var wingLMesh = SaveMesh(wingL, "Mosquito_WingL");

            // ---- 재질 ----
            var mats = new[]
            {
                LitMat("Mosquito_Body", BodyColor, 0.25f),
                LitMat("Mosquito_Stripe", StripeColor, 0.25f),
                LitMat("Mosquito_EyeWhite", EyeWhiteColor, 0.6f),
                LitMat("Mosquito_Pupil", PupilColor, 0.8f),
            };
            var wingMat = WingMat("Mosquito_Wing", WingColor);

            // ---- 프리팹 ----
            var root = new GameObject("Mosquito");

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(root.transform, false);
            bodyGo.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            var bodyMr = bodyGo.AddComponent<MeshRenderer>();
            bodyMr.sharedMaterials = mats;
            NoShadows(bodyMr);

            var wr = MakeWing(root.transform, "Wing_R", wingRMesh, wingMat, 1f);
            var wl = MakeWing(root.transform, "Wing_L", wingLMesh, wingMat, -1f);

            var flap = root.AddComponent<MosquitoWingFlap>();
            flap.wingR = wr;
            flap.wingL = wl;
            wr.localRotation = MosquitoWingFlap.WingRotation(flap.dihedral, flap.sweep, 1f);
            wl.localRotation = MosquitoWingFlap.WingRotation(flap.dihedral, flap.sweep, -1f);

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0f, -0.2f) * ModelScale;
            col.radius = 1.6f * ModelScale; // 약 1.2cm (파리채/손 판정용)

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            bool placed = PlaceInSceneIfMissing(prefab);
            string preview = RenderPreviews(prefab);

            int tris = (body.TriCount + wingR.TriCount + wingL.TriCount);
            Debug.Log($"[MosquitoBuilder] tris {tris} (body {body.TriCount}), " +
                      $"length {bodyMesh.bounds.size.z * 100f:0.0}cm, placedInScene {placed}, preview {preview}");
        }

        // ------------------------------------------------------------------
        // 몸
        // ------------------------------------------------------------------

        static void BuildBody(MeshBuilder m)
        {
            // 가슴(몸통 가운데)
            m.Ellipsoid(SubBody, Vector3.zero, new Vector3(0.42f, 0.40f, 0.48f), Quaternion.identity, 8, 6);

            // 머리 (크게 = 귀엽게)
            var head = new Vector3(0f, 0.08f, 0.68f);
            m.Ellipsoid(SubBody, head, Vector3.one * 0.40f, Quaternion.identity, 10, 7);

            // 눈: 흰자 + 검은 눈동자 + 반짝이
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = new Vector3(0.20f * s, 0.18f, 0.95f);
                m.Ellipsoid(SubEyeWhite, eye, Vector3.one * 0.20f, Quaternion.identity, 12, 8);
                var look = new Vector3(0.25f * s, 0.10f, 1f).normalized;
                var pupil = eye + look * 0.12f;
                m.Ellipsoid(SubPupil, pupil, Vector3.one * 0.10f, Quaternion.identity, 10, 7);
                var sparkle = pupil + look * 0.07f + new Vector3(0.03f * s, 0.05f, 0f);
                m.Ellipsoid(SubEyeWhite, sparkle, Vector3.one * 0.035f, Quaternion.identity, 6, 4);
            }

            // 침 (앞으로 길게, 살짝 아래로)
            m.Tube(SubPupil, new Vector3(0f, -0.05f, 1.0f), new Vector3(0f, -0.45f, 1.85f), 0.045f, 0.012f, 6, true, true);

            // 더듬이 + 끝 동그라미
            for (int s = -1; s <= 1; s += 2)
            {
                var a = new Vector3(0.10f * s, 0.40f, 0.85f);
                var b = new Vector3(0.32f * s, 0.80f, 1.10f);
                m.Tube(SubBody, a, b, 0.025f, 0.015f, 5, false, false);
                m.Ellipsoid(SubBody, b, Vector3.one * 0.05f, Quaternion.identity, 6, 4);
            }

            // 배: 마디 5개, 진한색/흰색 번갈아 (줄무늬)
            var dir = new Vector3(0f, -0.15f, -1f).normalized;
            var start = new Vector3(0f, -0.02f, -0.35f);
            float[] radius = { 0.24f, 0.26f, 0.25f, 0.21f, 0.15f };
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            for (int i = 0; i < radius.Length; i++)
            {
                var c = start + dir * (0.28f + i * 0.29f);
                int sub = i % 2 == 0 ? SubBody : SubStripe;
                float len = sub == SubStripe ? 0.21f : 0.24f;
                float r = sub == SubStripe ? radius[i] * 1.02f : radius[i];
                m.Ellipsoid(sub, c, new Vector3(r, r * 0.95f, len), rot, 8, 6);
            }

            // 다리 6개: 엉덩이 → 무릎(위로 꺾임) → 발(아래 바깥쪽)
            float[] hipZ = { 0.22f, 0.0f, -0.22f };
            float[] spread = { 0.40f, 0.05f, -0.40f };
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    var hip = new Vector3(0.20f * s, -0.28f, hipZ[i]);
                    var knee = new Vector3(0.75f * s, 0.12f, hipZ[i] + spread[i]);
                    var foot = new Vector3(1.05f * s, -0.78f, hipZ[i] + spread[i] * 2f);
                    m.Tube(SubBody, hip, knee, 0.032f, 0.026f, 5, false, false);
                    m.Ellipsoid(SubBody, knee, Vector3.one * 0.034f, Quaternion.identity, 5, 3);
                    m.Tube(SubBody, knee, foot, 0.026f, 0.014f, 5, false, true);
                }
            }
        }

        /// <summary>날개 하나 (뿌리 = 원점, side=+1이면 +X로 뻗음). 납작한 나뭇잎 모양.</summary>
        static void BuildWing(MeshBuilder m, float side)
        {
            const int N = 10;
            const float L = 1.6f, W = 0.48f;
            var lead = new List<Vector3>();
            var trail = new List<Vector3>();
            for (int k = 0; k <= N; k++)
            {
                float t = (float)k / N;
                float h = W * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.04f, 1f, t)), 0.75f);
                float x = L * t * side;
                lead.Add(new Vector3(x, 0f, h * 0.35f));
                trail.Add(new Vector3(x, 0f, -h * 0.65f));
            }
            for (int k = 0; k < N; k++)
            {
                m.Tri(0, lead[k], lead[k + 1], trail[k + 1], Vector3.up);
                m.Tri(0, lead[k], trail[k + 1], trail[k], Vector3.up);
            }
        }

        static Transform MakeWing(Transform parent, string name, Mesh mesh, Material mat, float side)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(WingRoot.x * side, WingRoot.y, WingRoot.z) * ModelScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            NoShadows(mr);
            return go.transform;
        }

        static void NoShadows(MeshRenderer mr)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ------------------------------------------------------------------
        // 메쉬 빌더 (각진 면 = 로우폴리 느낌, 면마다 정점 따로)
        // ------------------------------------------------------------------

        class MeshBuilder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<int>[] sub;
            public int TriCount { get; private set; }

            public MeshBuilder(int subCount)
            {
                sub = new List<int>[subCount];
                for (int i = 0; i < subCount; i++) sub[i] = new List<int>();
            }

            /// <summary>삼각형 하나. hint 쪽을 바라보게 감는 방향을 맞춘다.</summary>
            public void Tri(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 hint)
            {
                Vector3 nrm = Vector3.Cross(b - a, c - a);
                float mag = nrm.magnitude;
                if (mag < 1e-9f) return; // 극점 등에서 생기는 넓이 0 삼각형
                if (Vector3.Dot(nrm, hint) < 0f) { (b, c) = (c, b); nrm = -nrm; }
                nrm /= mag; // (Vector3.Normalize는 작은 벡터를 0으로 만들어서 직접 나눔)
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                n.Add(nrm); n.Add(nrm); n.Add(nrm);
                sub[s].Add(i); sub[s].Add(i + 1); sub[s].Add(i + 2);
                TriCount++;
            }

            public void Ellipsoid(int s, Vector3 center, Vector3 radii, Quaternion rot, int lon, int lat)
            {
                Vector3 P(int i, int j)
                {
                    float th = Mathf.PI * j / lat;
                    float ph = Mathf.PI * 2f * i / lon;
                    var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    return center + rot * Vector3.Scale(u, radii);
                }

                for (int j = 0; j < lat; j++)
                {
                    for (int i = 0; i < lon; i++)
                    {
                        Vector3 a = P(i, j), b = P(i + 1, j), c = P(i + 1, j + 1), d = P(i, j + 1);
                        Vector3 hint = (a + b + c + d) * 0.25f - center;
                        Tri(s, a, b, c, hint);
                        Tri(s, a, c, d, hint);
                    }
                }
            }

            public void Tube(int s, Vector3 p0, Vector3 p1, float r0, float r1, int sides, bool cap0, bool cap1)
            {
                Vector3 axis = p1 - p0;
                Vector3 dir = axis / axis.magnitude;
                Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right);
                u /= u.magnitude;
                Vector3 w = Vector3.Cross(dir, u);

                Vector3 Ring(Vector3 c, float r, int k)
                {
                    float a = Mathf.PI * 2f * k / sides;
                    return c + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * r;
                }

                for (int k = 0; k < sides; k++)
                {
                    Vector3 a = Ring(p0, r0, k), b = Ring(p0, r0, k + 1);
                    Vector3 c = Ring(p1, r1, k + 1), d = Ring(p1, r1, k);
                    Vector3 mid = (a + b + c + d) * 0.25f;
                    Vector3 hint = mid - (p0 + dir * Vector3.Dot(mid - p0, dir));
                    Tri(s, a, b, c, hint);
                    Tri(s, a, c, d, hint);
                    if (cap0) Tri(s, p0, a, b, -dir);
                    if (cap1) Tri(s, p1, d, c, dir);
                }
            }

            public Mesh ToMesh(Mesh mesh, float scale)
            {
                mesh.Clear();
                var verts = new List<Vector3>(v.Count);
                foreach (var p in v) verts.Add(p * scale);
                mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(verts);
                mesh.SetNormals(n);
                mesh.subMeshCount = sub.Length;
                for (int i = 0; i < sub.Length; i++) mesh.SetTriangles(sub[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        static Mesh SaveMesh(MeshBuilder mb, string name)
        {
            string path = $"{ModelDir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = name };
            mb.ToMesh(mesh, ModelScale);
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------
        // 재질
        // ------------------------------------------------------------------

        static Material GetOrCreateMat(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        static Material LitMat(string name, Color color, float smoothness)
        {
            var mat = GetOrCreateMat(name);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material WingMat(string name, Color color)
        {
            var mat = GetOrCreateMat(name);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.7f);
            // 반투명 + 양면
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        // 씬 배치 / 미리보기
        // ------------------------------------------------------------------

        static bool PlaceInSceneIfMissing(GameObject prefab)
        {
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = tr.gameObject;
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(go) &&
                    PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab) return false;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // 침대에 앉은 플레이어 앞쪽 공중, 플레이어를 바라보게 (확인용)
            inst.transform.SetPositionAndRotation(new Vector3(0.25f, 1.0f, 0.5f), Quaternion.Euler(0f, -153f, 0f));
            Undo.RegisterCreatedObjectUndo(inst, "Place Mosquito");
            EditorSceneManager.MarkSceneDirty(inst.scene);
            return true;
        }

        static string RenderPreviews(GameObject prefab)
        {
            var pru = new PreviewRenderUtility();
            try
            {
                Directory.CreateDirectory(PreviewDir);
                var cam = pru.camera;
                cam.nearClipPlane = 0.001f;
                cam.farClipPlane = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.62f, 0.66f, 0.72f);
                pru.lights[0].intensity = 1.1f;
                pru.lights[0].transform.rotation = Quaternion.Euler(40f, 150f, 0f);
                pru.lights[1].intensity = 0.6f;
                pru.ambientColor = new Color(0.4f, 0.4f, 0.43f);
                pru.InstantiatePrefabInScene(prefab);

                var c = new Vector3(0f, 0f, 0f);
                Shot(pru, "front34", new Vector3(0.045f, 0.025f, 0.06f), c);
                Shot(pru, "side", new Vector3(0.085f, 0.005f, 0f), c);
                Shot(pru, "top", new Vector3(0f, 0.085f, -0.005f), c);
                Shot(pru, "face", new Vector3(0.008f, 0.006f, 0.045f), new Vector3(0f, 0f, 0.006f));
                return Path.GetFullPath(PreviewDir);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MosquitoBuilder] preview failed: {e}");
                return "failed";
            }
            finally
            {
                pru.Cleanup();
            }
        }

        static void Shot(PreviewRenderUtility pru, string name, Vector3 pos, Vector3 target)
        {
            pru.BeginStaticPreview(new Rect(0, 0, 1024, 1024));
            pru.camera.fieldOfView = 30f;
            var fwd = target - pos;
            var up = Mathf.Abs(fwd.normalized.y) > 0.95f ? Vector3.forward : Vector3.up;
            pru.camera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));
            pru.Render(true, false);
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
