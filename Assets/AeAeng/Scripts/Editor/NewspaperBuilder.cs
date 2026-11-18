using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 돌돌 만 신문지 (11/18 중간 피드백). AeAeng > Build Newspaper
    ///  1) 메쉬: 바깥 한 겹이 살짝 튀어나온 나선 단면의 원통 + 양 끝에 돌돌 말린 무늬. 신문 글씨/나선 텍스처는 코드로 그림
    ///  2) 프리팹: 파리채와 같은 잡기(SwatterGrab) / 치기(SwatterHit). 판정은 좁고(지름 6cm × 길이 28cm), 더 빨리 휘둘러야(2.0m/s) 잡힘.
    ///     대신 휘두르는 소리가 더 낮고 묵직하며, 잡을 때 진동이 더 셈.
    ///  3) 파리채 프리팹에도 휘두르는 소리 / 잡을 때 진동을 넣음
    ///  4) 씬: 파리채와 같은 자리에 신문지를 놓고, WeaponSelector 로 메인 화면에서 고른 것만 보이게
    /// 좌표: 원점 = 손잡이 끝, +Y = 치는 쪽 (파리채와 같음). 단위 m.
    /// 메인 화면의 무기 선택 버튼은 AeAeng > Build Scene Flow 를 다시 실행하면 생김.
    /// </summary>
    public static class NewspaperBuilder
    {
        const string ModelDir = "Assets/AeAeng/Models";
        const string MeshPath = ModelDir + "/Newspaper_Mesh.asset";
        const string TexDir = "Assets/AeAeng/Textures/Newspaper";
        const string MatDir = "Assets/AeAeng/Materials/Newspaper";
        const string PrefabDir = "Assets/AeAeng/Prefabs";
        const string PrefabPath = PrefabDir + "/Newspaper.prefab";
        const string SwatterPrefabPath = PrefabDir + "/FlySwatter.prefab";
        const string WhipClipPath = "Assets/AeAeng/Sound/Whip/universfield-whip-snap-242215.mp3";
        const string PreviewDir = "Temp/NewspaperPreview";

        // ---- 치수 (m) ----
        // 11/18 헤드셋 테스트: 막 휘둘러도 잘 잡혀서 파리채보다 쉬움 → 42cm/지름 4.4cm 에서 32cm/지름 3.6cm 로 줄임
        const float Length = 0.32f;
        const float Radius = 0.018f;
        const float InnerRatio = 0.93f;   // 나선 단면: 한 바퀴 돌며 반지름이 이 비율 → 1.0 (바깥 한 겹의 끝이 살짝 튀어나옴)
        const int Segments = 18;

        // ---- 판정 / 손맛 ----
        static readonly Vector3 NewspaperHalfExtents = new Vector3(0.025f, 0.08f, 0.025f); // 지름 5cm × 길이 16cm (전: 6cm × 28cm)
        const float NewspaperHeadY = 0.22f; // 판정 0.14~0.30m: 손잡이 쪽은 안 잡히고 끝쪽 절반만
        const float NewspaperMinSpeed = 3.0f; // 11/18: 2.0 은 그냥 움직여도 넘어서 올림
        const float SwatterMinSpeed = 3.5f;   // 11/18: 1.5 → 2.5 → 3.5 (채 머리가 손에서 38cm 라 신문지(22cm)보다 같은 손목 움직임에 더 빠름)
        // 공중(날고 있는) 모기가 판정에 걸렸을 때 잡힐 확률. 앉은 모기는 항상 잡힘
        const float SwatterAirChance = 0.2f;   // 11/18: 크고 넓은 파리채가 공중은 더 어렵게 (신문지와 바꿈)
        const float NewspaperAirChance = 0.35f; // 작고 좁아서 맞히기 어려운 대신 맞으면 더 잘 잡힘

        [MenuItem("AeAeng/Build Newspaper")]
        public static void Build()
        {
            EnsureFolder(ModelDir);
            EnsureFolder(TexDir);
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);

            var mesh = BuildMesh();
            var paperTex = SaveTexture("Newsprint", DrawNewsprint(), TextureWrapMode.Repeat);
            var endTex = SaveTexture("RolledEnd", DrawRolledEnd(), TextureWrapMode.Clamp);
            var paperMat = LitMat("Newspaper_Paper", paperTex, 0.08f);
            var endMat = LitMat("Newspaper_End", endTex, 0.05f);
            var whip = PrepareWhipClip();

            var prefab = BuildPrefab(mesh, paperMat, endMat, whip);
            string swatter = SetupSwatterFeel(whip);
            string scene = SetupScene(prefab);
            string preview = RenderPreviews(prefab);

            Debug.Log($"[NewspaperBuilder] tris {mesh.triangles.Length / 3}, length {Length * 100f:0}cm, " +
                      $"swatter {swatter}, scene {scene}, preview {preview}\n" +
                      "메인 화면 무기 선택 버튼은 AeAeng > Build Scene Flow 를 다시 실행하면 생깁니다.");
        }

        // ------------------------------------------------------------------
        // 메쉬
        // ------------------------------------------------------------------

        static Vector3 Section(float theta, float y)
        {
            float r = Radius * Mathf.Lerp(InnerRatio, 1f, theta / (Mathf.PI * 2f));
            return new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
        }

        static Mesh BuildMesh()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var side = new List<int>();
            var ends = new List<int>();

            // 옆면: u = 길이 방향, v = 둘레 방향 (신문 글씨 줄이 길이 방향으로 감)
            int ring = Segments + 1;
            for (int k = 0; k < 2; k++)
            {
                float y = k * Length;
                for (int i = 0; i < ring; i++)
                {
                    float th = Mathf.PI * 2f * i / Segments;
                    v.Add(Section(th, y));
                    n.Add(new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th)));
                    uv.Add(new Vector2(y / Length, (float)i / Segments));
                }
            }
            for (int i = 0; i < Segments; i++)
                AddQuad(v, side, i, i + 1, ring + i + 1, ring + i, Outward(v, i, i + 1));

            // 바깥 한 겹이 끝나는 단 (θ = 0 자리의 작은 세로 면, 둘레 방향 +Z 를 봄)
            int s0 = v.Count;
            v.Add(Section(Mathf.PI * 2f, 0f)); v.Add(Section(0f, 0f));
            v.Add(Section(0f, Length)); v.Add(Section(Mathf.PI * 2f, Length));
            for (int i = 0; i < 4; i++) { n.Add(Vector3.forward); uv.Add(new Vector2(i >= 2 ? 1f : 0f, 0.999f)); }
            AddQuad(v, side, s0, s0 + 1, s0 + 2, s0 + 3, Vector3.forward);

            // 양 끝: 단면 모양 그대로 부채꼴, 돌돌 말린 무늬 텍스처를 평면으로 붙임
            for (int k = 0; k < 2; k++)
            {
                float y = k * Length;
                var normal = k == 0 ? Vector3.down : Vector3.up;
                int c = v.Count;
                v.Add(new Vector3(0f, y, 0f)); n.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= Segments; i++)
                {
                    var p = Section(Mathf.PI * 2f * i / Segments, y);
                    v.Add(p); n.Add(normal);
                    uv.Add(new Vector2(p.x / (2f * Radius) + 0.5f, p.z / (2f * Radius) + 0.5f));
                }
                for (int i = 0; i < Segments; i++)
                    AddTri(v, ends, c, c + 1 + i, c + 2 + i, normal);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "Newspaper" };
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(side, 0);
            mesh.SetTriangles(ends, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (isNew) AssetDatabase.CreateAsset(mesh, MeshPath);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Vector3 Outward(List<Vector3> v, int a, int b)
        {
            var mid = (v[a] + v[b]) * 0.5f;
            mid.y = 0f;
            return mid.normalized;
        }

        /// <summary>a-b-c-d 사각형. 앞면이 want 방향을 보도록 감는 방향을 맞춘다 (Unity: cross(b-a, c-a) 쪽이 앞면)</summary>
        static void AddQuad(List<Vector3> v, List<int> t, int a, int b, int c, int d, Vector3 want)
        {
            AddTri(v, t, a, b, c, want);
            AddTri(v, t, a, c, d, want);
        }

        static void AddTri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 want)
        {
            var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(face, want) >= 0f) t.AddRange(new[] { a, b, c });
            else t.AddRange(new[] { a, c, b });
        }

        // ------------------------------------------------------------------
        // 텍스처 (코드로 그림)
        // ------------------------------------------------------------------

        static readonly Color Paper = new Color(0.87f, 0.86f, 0.81f);
        static readonly Color Ink = new Color(0.22f, 0.22f, 0.24f);

        /// <summary>신문 지면: 가로(u) = 길이 방향. 칼럼 3개, 제목 굵은 줄, 사진 칸, 본문 줄</summary>
        static Texture2D DrawNewsprint()
        {
            const int W = 512, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
            var px = new Color[W * H];
            var rnd = new System.Random(1118);
            for (int i = 0; i < px.Length; i++)
            {
                float noise = (float)rnd.NextDouble() * 0.03f - 0.015f;
                px[i] = new Color(Paper.r + noise, Paper.g + noise, Paper.b + noise, 1f);
            }

            void Rect(int x0, int y0, int x1, int y1, Color c, float a)
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(H, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(W, x1); x++)
                    px[y * W + x] = Color.Lerp(px[y * W + x], c, a);
            }

            int[] colX = { 14, 180, 346 };
            const int colW = 152;
            for (int ci = 0; ci < colX.Length; ci++)
            {
                int x0 = colX[ci];
                int y = H - 14;
                // 칼럼마다 제목 하나
                // 칼럼마다 제목: 굵은 글자 덩어리 2~4개
                int headW = colW - rnd.Next(0, 40);
                for (int x = x0; x < x0 + headW;)
                {
                    int w = Mathf.Min(rnd.Next(18, 46), x0 + headW - x);
                    Rect(x, y - 11, x + w, y, Ink, 0.85f);
                    x += w + 6;
                }
                y -= 20;
                if (ci == 1)
                {
                    // 가운데 칼럼에 사진 칸
                    Rect(x0, y - 70, x0 + colW, y, new Color(0.45f, 0.45f, 0.47f), 0.85f);
                    y -= 80;
                }
                while (y > 10)
                {
                    int lines = rnd.Next(4, 9);
                    for (int l = 0; l < lines && y > 10; l++)
                    {
                        // 본문 한 줄 = 짧은 단어 덩어리들 (줄이 통째로 이어지면 바코드처럼 보임)
                        int len = l == lines - 1 ? rnd.Next(30, colW - 20) : colW - rnd.Next(0, 6);
                        for (int x = x0 + (l == 0 ? 8 : 0); x < x0 + len;)
                        {
                            int w = Mathf.Min(rnd.Next(4, 15), x0 + len - x);
                            Rect(x, y - 2, x + w, y, Ink, 0.5f);
                            x += w + rnd.Next(2, 4);
                        }
                        y -= 5;
                    }
                    y -= 5; // 문단 사이
                }
            }
            // 칼럼 사이 세로 선
            Rect(172, 8, 173, H - 8, Ink, 0.35f);
            Rect(338, 8, 339, H - 8, Ink, 0.35f);

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>끝 단면: 돌돌 말린 종이 나선</summary>
        static Texture2D DrawRolledEnd()
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            var px = new Color[S * S];
            var face = new Color(0.80f, 0.79f, 0.74f);
            var line = new Color(0.42f, 0.41f, 0.39f);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;       // 0 ~ 1 (가장자리)
                float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f);     // -0.5 ~ 0.5
                Color c;
                if (r < 0.12f) c = new Color(0.12f, 0.12f, 0.12f);   // 가운데 빈 구멍
                else
                {
                    float phase = r * 9f - a;                        // 9겹 나선
                    float f = phase - Mathf.Floor(phase);
                    c = f < 0.16f ? line : face;
                    c *= Mathf.Lerp(0.85f, 1.0f, r);                 // 안쪽은 살짝 어둡게
                    c.a = 1f;
                }
                px[y * S + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static Texture2D SaveTexture(string name, Texture2D tex, TextureWrapMode wrap)
        {
            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.wrapMode = wrap;
            imp.anisoLevel = 2;
            imp.maxTextureSize = 512;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material LitMat(string name, Texture2D tex, float smoothness)
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
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        // 소리
        // ------------------------------------------------------------------

        /// <summary>휘두르는 소리는 손 위치에서 3D로 내므로 모노로</summary>
        static AudioClip PrepareWhipClip()
        {
            var imp = AssetImporter.GetAtPath(WhipClipPath) as AudioImporter;
            if (imp == null)
            {
                Debug.LogWarning($"[NewspaperBuilder] 휘두르는 소리 {WhipClipPath} 가 없습니다. 소리 없이 만듭니다.");
                return null;
            }
            if (!imp.forceToMono)
            {
                imp.forceToMono = true;
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad; // 짧은 효과음: 바로 재생 + time 지정 가능
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(WhipClipPath);
        }

        static AudioSource EnsureSwingAudio(Transform head, AudioClip clip, float pitch, float volume)
        {
            var a = head.GetComponent<AudioSource>();
            if (a == null) a = head.gameObject.AddComponent<AudioSource>();
            a.clip = clip;
            a.playOnAwake = false;
            a.loop = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 0.3f;
            a.maxDistance = 5f;
            a.pitch = pitch;
            a.volume = volume;
            return a;
        }

        // ------------------------------------------------------------------
        // 프리팹
        // ------------------------------------------------------------------

        static GameObject BuildPrefab(Mesh mesh, Material paper, Material end, AudioClip whip)
        {
            var go = new GameObject("Newspaper");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { paper, end };

            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 1; // Y
            col.radius = Radius;
            col.height = Length;
            col.center = new Vector3(0f, Length * 0.5f, 0f);

            // 파리채와 같은 규칙: 손잡이 끝에서 7cm, forward = 치는 쪽 → 오른손 SwatterGrip 에 그대로 맞음
            var grip = new GameObject("GripPoint").transform;
            grip.SetParent(go.transform, false);
            grip.localPosition = new Vector3(0f, 0.07f, 0f);
            grip.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var grab = go.AddComponent<XRGrabInteractable>();
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = false;
            grab.trackScale = false;
            grab.attachTransform = grip;
            grab.selectMode = InteractableSelectMode.Single;
            go.AddComponent<SwatterGrab>();

            // 치기 판정: 위쪽 2/3 (손 위 7cm ~ 끝), 좁고 길게
            var head = new GameObject("HeadCenter").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0f, NewspaperHeadY, 0f);
            head.localRotation = Quaternion.identity;
            var tracker = head.gameObject.AddComponent<VelocityTracker>();
            tracker.alertsMosquitoes = true;

            var hit = go.AddComponent<SwatterHit>();
            hit.head = tracker;
            hit.grab = go.GetComponent<SwatterGrab>();
            hit.halfExtents = NewspaperHalfExtents;
            hit.minSpeed = NewspaperMinSpeed;
            hit.swingSpeed = NewspaperMinSpeed; // 잡힐 수 있는 속도부터 소리
            hit.swingCooldown = 0.4f;
            hit.airCatchChance = NewspaperAirChance; // 신문지는 앉은 모기를 "탁!" 치는 무기
            hit.hitHapticAmplitude = 1.0f;   // "세게": 파리채(0.6)보다 강하고 길게
            hit.hitHapticDuration = 0.14f;
            if (whip != null) hit.swingAudio = EnsureSwingAudio(head, whip, pitch: 0.75f, volume: 1.0f); // 낮고 묵직한 "휙"

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>파리채 프리팹에도 휘두르는 소리 / 잡을 때 진동 (신문지보다 가볍고 높은 소리)</summary>
        static string SetupSwatterFeel(AudioClip whip)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SwatterPrefabPath) == null) return "파리채 프리팹 없음";
            var root = PrefabUtility.LoadPrefabContents(SwatterPrefabPath);
            try
            {
                var hit = root.GetComponent<SwatterHit>();
                var head = root.transform.Find("HeadCenter");
                if (hit == null || head == null) return "SwatterHit 없음 (AeAeng > Setup Mosquitoes 먼저)";
                hit.minSpeed = SwatterMinSpeed;
                hit.swingSpeed = SwatterMinSpeed; // 잡힐 수 있는 속도부터 소리
                hit.swingCooldown = 0.35f;
                hit.airCatchChance = SwatterAirChance;
                hit.hitHapticAmplitude = 0.6f;
                hit.hitHapticDuration = 0.08f;
                if (whip != null) hit.swingAudio = EnsureSwingAudio(head, whip, pitch: 1.15f, volume: 0.75f);
                PrefabUtility.SaveAsPrefabAsset(root, SwatterPrefabPath);
                return "휘두르는 소리 + 진동 설정";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------
        // 씬: 파리채 자리에 신문지 + WeaponSelector
        // ------------------------------------------------------------------

        static string SetupScene(GameObject newspaperPrefab)
        {
            var swatterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwatterPrefabPath);
            GameObject swatter = null, paper = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src == swatterPrefab) swatter = go;
                else if (src == newspaperPrefab) paper = go;
            }
            if (swatter == null) return "씬에 파리채가 없어서 신문지를 놓지 않음 (AeAeng > Build Fly Swatter 먼저)";

            bool placed = false;
            if (paper == null)
            {
                paper = (GameObject)PrefabUtility.InstantiatePrefab(newspaperPrefab);
                // 파리채와 같은 자리, 같은 방향(치는 쪽이 같은 쪽). 책상 위에 눕도록 반지름만큼 올림
                var st = swatter.transform;
                paper.transform.SetPositionAndRotation(st.position + Vector3.up * (Radius - 0.002f), st.rotation);
                Undo.RegisterCreatedObjectUndo(paper, "Place Newspaper");
                placed = true;
            }

            var sel = Object.FindFirstObjectByType<WeaponSelector>(FindObjectsInactive.Include);
            if (sel == null)
            {
                var go = new GameObject("WeaponSelector");
                Undo.RegisterCreatedObjectUndo(go, "Add Weapon Selector");
                sel = go.AddComponent<WeaponSelector>();
            }
            Undo.RecordObject(sel, "Weapon Selector refs");
            sel.flySwatter = swatter;
            sel.newspaper = paper;
            // 에디터에서는 파리채를 보이게 두고, 실행하면 고른 무기만 켬
            if (!swatter.activeSelf) { Undo.RecordObject(swatter, "Show swatter"); swatter.SetActive(true); }
            if (paper.activeSelf) { Undo.RecordObject(paper, "Hide newspaper"); paper.SetActive(false); }

            EditorSceneManager.MarkSceneDirty(swatter.scene);
            return placed ? "신문지 배치 + WeaponSelector" : "WeaponSelector 연결 (신문지는 이미 있음)";
        }

        // ------------------------------------------------------------------
        // 미리보기
        // ------------------------------------------------------------------

        static string RenderPreviews(GameObject prefab)
        {
            var pru = new PreviewRenderUtility();
            try
            {
                Directory.CreateDirectory(PreviewDir);
                var cam = pru.camera;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 10f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.45f, 0.47f, 0.52f);
                pru.lights[0].intensity = 1.1f;
                pru.lights[0].transform.rotation = Quaternion.Euler(35f, 30f, 0f);
                pru.lights[1].intensity = 0.5f;
                pru.ambientColor = new Color(0.35f, 0.35f, 0.38f);
                pru.InstantiatePrefabInScene(prefab);

                Shot(pru, "side", new Vector3(0.75f, 0.21f, -0.35f), new Vector3(0f, 0.21f, 0f));
                Shot(pru, "end", new Vector3(0.05f, 0.52f, -0.08f), new Vector3(0f, Length, 0f));
                Shot(pru, "close", new Vector3(0.12f, 0.30f, -0.12f), new Vector3(0f, 0.30f, 0f));
                return Path.GetFullPath(PreviewDir);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NewspaperBuilder] preview failed: {e}");
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
            pru.camera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos));
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
