using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 파리채 메쉬를 코드로 생성한다. (AeAeng > Build Fly Swatter)
    /// - 머리: 납작한 판(두께 2mm)에 # 모양 격자 구멍이 실제로 뚫려 있음
    /// - 손잡이: 납작한 막대 + 끝에 거는 고리 구멍
    /// 결과물: Models/FlySwatter_Mesh.asset, Materials/FlySwatter.mat, Prefabs/FlySwatter.prefab
    /// 좌표: 원점 = 손잡이 끝, +Y = 머리 쪽, 판의 면은 ±Z를 바라봄. 단위 m.
    /// </summary>
    public static class FlySwatterBuilder
    {
        const string ModelDir = "Assets/AeAeng/Models";
        const string MeshPath = ModelDir + "/FlySwatter_Mesh.asset";
        const string MatDir = "Assets/AeAeng/Materials";
        const string MatPath = MatDir + "/FlySwatter.mat";
        const string PrefabDir = "Assets/AeAeng/Prefabs";
        const string PrefabPath = PrefabDir + "/FlySwatter.prefab";
        const string PreviewDir = "Temp/FlySwatterPreview";

        // ---- 치수 (m) ----
        const float HandleHalf = 0.0055f;     // 손잡이 폭 11mm
        const float LoopR = 0.0075f;          // 손잡이 끝 고리 반지름
        const float LoopHoleR = 0.0035f;      // 고리 구멍 반지름
        const float LoopY = LoopR;            // 고리 중심 (바닥이 y=0)
        const float HeadThick = 0.002f;       // 머리 판 두께
        const float HandleThick = 0.005f;     // 손잡이 두께
        const float HoleSize = 0.0036f;       // 격자 구멍 한 변
        const float Pitch = 0.0055f;          // 구멍 간격 (살 두께 = Pitch - HoleSize)
        const float Rim = 0.0055f;            // 테두리 폭
        const float HoleMinY = 0.325f;        // 이보다 아래(목 부분)에는 구멍 없음
        const float OutlineStartY = 0.25f;    // 이 위부터 머리 외곽선 곡선 사용
        static readonly Color SwatterColor = new Color(0.55f, 0.84f, 0.84f);

        struct Row { public float y0, y1; public int n; } // 구멍 x = i * Pitch (i = -n..n)

        static List<Vector2> _outline; // 오른쪽 절반, y 내림차순
        static float _topY;

        [MenuItem("AeAeng/Build Fly Swatter")]
        public static void Build()
        {
            BuildOutline();
            var rows = BuildRows();

            var md = new MeshData();
            Generate(md, rows, out int holeCount);

            EnsureFolder(ModelDir);
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "FlySwatter" };
            mesh.Clear();
            mesh.indexFormat = md.v.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(md.v);
            mesh.SetNormals(md.n);
            mesh.SetUVs(0, md.uv);
            mesh.SetTriangles(md.t, 0);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, MeshPath);
            else EditorUtility.SetDirty(mesh);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FlySwatter" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            mat.SetColor("_BaseColor", SwatterColor);
            mat.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(mat);

            // ---- 프리팹 ----
            var go = new GameObject("FlySwatter");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            // 2~5mm 얇은 판이라 그림자를 켜면 자기 그림자 줄무늬(acne)가 생김 → 끔
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            float headBottom = HoleMinY - 0.02f;
            var head = go.AddComponent<BoxCollider>();
            head.center = new Vector3(0f, (headBottom + _topY) * 0.5f, 0f);
            head.size = new Vector3(0.12f, _topY - headBottom, 0.01f);
            var handle = go.AddComponent<BoxCollider>();
            handle.center = new Vector3(0f, headBottom * 0.5f, 0f);
            handle.size = new Vector3(HandleHalf * 2.6f, headBottom, 0.008f);

            var grip = new GameObject("GripPoint").transform;
            grip.SetParent(go.transform, false);
            grip.localPosition = new Vector3(0f, 0.06f, 0f);
            grip.localRotation = Quaternion.Euler(-90f, 0f, 0f); // forward = 머리 쪽

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();

            bool placed = PlaceInSceneIfMissing(prefab);
            string preview = RenderPreviews(prefab);

            Debug.Log($"[FlySwatterBuilder] verts {md.v.Count}, tris {md.t.Count / 3}, holes {holeCount}, " +
                      $"length {_topY * 100f:0.0}cm, placedInScene {placed}, preview {preview}");
        }

        // ------------------------------------------------------------------
        // 외곽선
        // ------------------------------------------------------------------

        static void BuildOutline()
        {
            // 오른쪽 절반의 거친 다각형 → 좌우 대칭으로 이어 붙인 뒤 Chaikin으로 둥글게
            var right = new List<Vector2>
            {
                new Vector2(0f, 0.450f),        // 머리 위 가운데
                new Vector2(0.032f, 0.4485f),   // 위쪽 살짝 볼록
                new Vector2(0.060f, 0.443f),    // 위 모서리 (머리 폭 12cm)
                new Vector2(0.045f, 0.345f),    // 아래로 좁아지는 옆면
                new Vector2(0.009f, 0.298f),    // 목으로 모이는 곳
                new Vector2(HandleHalf, 0.272f),
                new Vector2(HandleHalf, OutlineStartY),
            };
            var pts = new List<Vector2>();
            for (int i = right.Count - 1; i >= 1; i--) pts.Add(new Vector2(-right[i].x, right[i].y));
            pts.AddRange(right);
            for (int it = 0; it < 5; it++) pts = Chaikin(pts);

            _outline = new List<Vector2>();
            foreach (var p in pts)
                if (p.x > 0f) _outline.Add(p);
            _topY = _outline[0].y;
        }

        static List<Vector2> Chaikin(List<Vector2> p)
        {
            var r = new List<Vector2> { p[0] };
            for (int i = 0; i < p.Count - 1; i++)
            {
                r.Add(Vector2.Lerp(p[i], p[i + 1], 0.25f));
                r.Add(Vector2.Lerp(p[i], p[i + 1], 0.75f));
            }
            r.Add(p[p.Count - 1]);
            return r;
        }

        /// <summary>높이 y에서 몸체의 반폭</summary>
        static float HalfWidth(float y)
        {
            if (y >= OutlineStartY)
            {
                if (y >= _outline[0].y) return _outline[0].x;
                for (int i = 0; i < _outline.Count - 1; i++)
                {
                    var a = _outline[i];
                    var b = _outline[i + 1];
                    if (y <= a.y && y >= b.y)
                    {
                        float d = a.y - b.y;
                        return d < 1e-7f ? Mathf.Max(a.x, b.x) : Mathf.Lerp(b.x, a.x, (y - b.y) / d);
                    }
                }
                return HandleHalf;
            }

            float dy = y - LoopY;
            float circle = Mathf.Sqrt(Mathf.Max(0f, LoopR * LoopR - dy * dy));
            return y < LoopY ? circle : Mathf.Max(HandleHalf, circle);
        }

        static float HalfThick(float y) =>
            0.5f * Mathf.SmoothStep(HandleThick, HeadThick, Mathf.InverseLerp(0.275f, 0.32f, y));

        static float LoopHoleHalf(float y)
        {
            float dy = y - LoopY;
            return Mathf.Sqrt(Mathf.Max(0f, LoopHoleR * LoopHoleR - dy * dy));
        }

        static bool Inside(float x, float y) => y >= 0f && y <= _topY && Mathf.Abs(x) <= HalfWidth(y);

        static List<Row> BuildRows()
        {
            var rows = new List<Row>();
            float h = HoleSize * 0.5f;
            for (int j = 0; ; j++)
            {
                float yc = _topY - Rim - h - j * Pitch;
                if (yc - h < HoleMinY) break;
                int n = -1;
                for (int i = 0; i < 40; i++)
                {
                    float x = i * Pitch + h + Rim;
                    if (Inside(x, yc - h - Rim) && Inside(x, yc + h + Rim)) n = i;
                    else break;
                }
                if (n >= 0) rows.Add(new Row { y0 = yc - h, y1 = yc + h, n = n });
            }
            return rows;
        }

        // ------------------------------------------------------------------
        // 메쉬 생성: 몸체를 가로 띠(slice)로 잘라, 띠마다 구멍을 피해서 판을 만든다
        // ------------------------------------------------------------------

        static void Generate(MeshData md, List<Row> rows, out int holeCount)
        {
            const float Eps = 2e-5f;
            float h = HoleSize * 0.5f;

            float rowsBottom = float.MaxValue, rowsTop = float.MinValue;
            foreach (var r in rows) { rowsBottom = Mathf.Min(rowsBottom, r.y0); rowsTop = Mathf.Max(rowsTop, r.y1); }

            var cuts = new List<float> { 0f, _topY, 0.03f, LoopY - LoopHoleR, LoopY + LoopHoleR };
            for (int i = 1; i < 60; i++) cuts.Add(i * 0.0005f);                 // 고리 부분 0.5mm
            for (float y = 0.245f; y < _topY; y += 0.001f)                       // 목·테두리 1mm
                if (y < rowsBottom - Eps || y > rowsTop + Eps) cuts.Add(y);
            foreach (var r in rows) { cuts.Add(r.y0); cuts.Add(r.y1); }
            cuts.Sort();
            var ys = new List<float>();
            foreach (float c in cuts)
                if (c >= 0f && c <= _topY && (ys.Count == 0 || c - ys[ys.Count - 1] > Eps)) ys.Add(c);

            var solids = new List<(float la, float lb, float ra, float rb)>();
            var splits = new List<float>();
            var bounds = new List<(float a, float b)>();

            for (int k = 0; k < ys.Count - 1; k++)
            {
                float ya = ys[k], yb = ys[k + 1], ym = (ya + yb) * 0.5f;
                float xa = HalfWidth(ya), xb = HalfWidth(yb);
                float ta = HalfThick(ya), tb = HalfThick(yb);

                solids.Clear();
                splits.Clear();

                int rowIdx = rows.FindIndex(r => ym > r.y0 && ym < r.y1);
                if (rowIdx >= 0)
                {
                    var row = rows[rowIdx];
                    float pa = -xa, pb = -xb;
                    for (int i = -row.n; i <= row.n; i++)
                    {
                        float hl = i * Pitch - h;
                        solids.Add((pa, pb, hl, hl));
                        pa = pb = i * Pitch + h;
                    }
                    solids.Add((pa, pb, xa, xb));
                }
                else if (ym > LoopY - LoopHoleR && ym < LoopY + LoopHoleR)
                {
                    float ca = LoopHoleHalf(ya), cb = LoopHoleHalf(yb);
                    solids.Add((-xa, -xb, -ca, -cb));
                    solids.Add((ca, cb, xa, xb));
                }
                else
                {
                    solids.Add((-xa, -xb, xa, xb));
                    // 위아래로 붙은 구멍 줄의 꼭짓점과 맞춰서 면을 나눔 (T자 이음새 줄이기)
                    foreach (var r in rows)
                        if (Mathf.Abs(r.y1 - ya) < Eps || Mathf.Abs(r.y0 - yb) < Eps)
                            for (int i = -r.n; i <= r.n; i++) { splits.Add(i * Pitch - h); splits.Add(i * Pitch + h); }
                    if (Mathf.Abs(ya - (LoopY + LoopHoleR)) < Eps || Mathf.Abs(yb - (LoopY - LoopHoleR)) < Eps)
                        splits.Add(0f);
                    splits.Sort();
                }

                foreach (var s in solids)
                {
                    bounds.Clear();
                    bounds.Add((s.la, s.lb));
                    float lo = Mathf.Max(s.la, s.lb) + Eps, hi = Mathf.Min(s.ra, s.rb) - Eps;
                    foreach (float x in splits)
                        if (x > lo && x < hi && x - bounds[bounds.Count - 1].a > Eps) bounds.Add((x, x));
                    bounds.Add((s.ra, s.rb));

                    for (int i = 0; i < bounds.Count - 1; i++)
                    {
                        var L = bounds[i];
                        var R = bounds[i + 1];
                        md.Quad(new Vector3(L.a, ya, ta), new Vector3(R.a, ya, ta),
                                new Vector3(R.b, yb, tb), new Vector3(L.b, yb, tb), Vector3.forward);
                        md.Quad(new Vector3(L.a, ya, -ta), new Vector3(R.a, ya, -ta),
                                new Vector3(R.b, yb, -tb), new Vector3(L.b, yb, -tb), Vector3.back);
                    }

                    md.Quad(new Vector3(s.la, ya, -ta), new Vector3(s.la, ya, ta),
                            new Vector3(s.lb, yb, tb), new Vector3(s.lb, yb, -tb), Vector3.left);
                    md.Quad(new Vector3(s.ra, ya, -ta), new Vector3(s.ra, ya, ta),
                            new Vector3(s.rb, yb, tb), new Vector3(s.rb, yb, -tb), Vector3.right);
                }
            }

            // 격자 구멍의 위/아래 벽
            holeCount = 0;
            float t = HeadThick * 0.5f;
            foreach (var r in rows)
            {
                for (int i = -r.n; i <= r.n; i++)
                {
                    float x0 = i * Pitch - h, x1 = i * Pitch + h;
                    md.Quad(new Vector3(x0, r.y1, -t), new Vector3(x1, r.y1, -t),
                            new Vector3(x1, r.y1, t), new Vector3(x0, r.y1, t), Vector3.down);
                    md.Quad(new Vector3(x0, r.y0, -t), new Vector3(x1, r.y0, -t),
                            new Vector3(x1, r.y0, t), new Vector3(x0, r.y0, t), Vector3.up);
                    holeCount++;
                }
            }

            // 맨 위 마감
            float xt = HalfWidth(_topY), tt = HalfThick(_topY);
            if (xt > 1e-6f)
                md.Quad(new Vector3(-xt, _topY, -tt), new Vector3(xt, _topY, -tt),
                        new Vector3(xt, _topY, tt), new Vector3(-xt, _topY, tt), Vector3.up);
        }

        class MeshData
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            /// <summary>사각형 하나 추가. hint 쪽을 바라보도록 감는 방향을 맞춘다 (평면 노멀).</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint)
            {
                Vector3 nrm = Vector3.Cross(c - a, d - b);
                if (nrm.sqrMagnitude < 1e-16f) return;
                if (Vector3.Dot(nrm, hint) < 0f) { (b, d) = (d, b); nrm = -nrm; }
                // Vector3.Normalize()는 길이 < 1e-5면 0벡터를 돌려줌 → mm 단위 작은 면이 깨지므로 직접 나눈다
                nrm /= nrm.magnitude;
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) n.Add(nrm);
                uv.Add(new Vector2(a.x, a.y)); uv.Add(new Vector2(b.x, b.y));
                uv.Add(new Vector2(c.x, c.y)); uv.Add(new Vector2(d.x, d.y));
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
        }

        // ------------------------------------------------------------------
        // 씬 배치 / 미리보기
        // ------------------------------------------------------------------

        static bool PlaceInSceneIfMissing(GameObject prefab)
        {
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = tr.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab) return false;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // 파리채 받침대(placeholder) 꼭대기에 세워 두고, 면이 플레이어 쪽을 보게
            inst.transform.SetPositionAndRotation(new Vector3(0.75f, 0.92f, 0.45f), Quaternion.Euler(0f, -121f, 0f));
            Undo.RegisterCreatedObjectUndo(inst, "Place Fly Swatter");
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
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 10f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.45f, 0.47f, 0.52f);
                pru.lights[0].intensity = 1.1f;
                pru.lights[0].transform.rotation = Quaternion.Euler(35f, 30f, 0f);
                pru.lights[1].intensity = 0.5f;
                pru.ambientColor = new Color(0.35f, 0.35f, 0.38f);
                pru.InstantiatePrefabInScene(prefab);

                Shot(pru, "front", 30f, new Vector3(0f, 0.225f, -0.95f), new Vector3(0f, 0.225f, 0f));
                Shot(pru, "head", 30f, new Vector3(0.05f, 0.40f, -0.30f), new Vector3(0f, 0.385f, 0f));
                Shot(pru, "angle", 30f, new Vector3(0.45f, 0.45f, -0.55f), new Vector3(0f, 0.25f, 0f));
                Shot(pru, "loop", 30f, new Vector3(0.02f, 0.02f, -0.06f), new Vector3(0f, 0.01f, 0f));
                return Path.GetFullPath(PreviewDir);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FlySwatterBuilder] preview failed: {e}");
                return "failed";
            }
            finally
            {
                pru.Cleanup();
            }
        }

        static void Shot(PreviewRenderUtility pru, string name, float fov, Vector3 pos, Vector3 target)
        {
            pru.BeginStaticPreview(new Rect(0, 0, 1024, 1024));
            pru.camera.fieldOfView = fov;
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
