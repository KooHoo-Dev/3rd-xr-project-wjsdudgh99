using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 가로로 긴 LED 디지털 벽시계를 코드로 생성한다. (AeAeng > Build Wall Clock)
    /// 검은 둥근 사각 본체 + 빨간 7-세그먼트 숫자(HH:MM, 월/일) + AM/PM, 요일 글씨 + 아래로 퍼지는 따뜻한 빛.
    /// 결과물: Models/WallClock_Meshes.asset, Materials/WallClock/*.mat, Prefabs/WallClock.prefab
    /// 좌표: 원점 = 본체 뒷면 가운데(벽에 닿는 면). 화면은 -Z를 바라봄 (회전 0이면 앞 벽에 걸려 방 안쪽을 봄).
    /// </summary>
    public static class WallClockBuilder
    {
        const string ModelDir = "Assets/AeAeng/Models";
        const string MeshPath = ModelDir + "/WallClock_Meshes.asset";
        const string MatDir = "Assets/AeAeng/Materials/WallClock";
        const string PrefabDir = "Assets/AeAeng/Prefabs";
        const string PrefabPath = PrefabDir + "/WallClock.prefab";
        const string PreviewDir = "Temp/WallClockPreview";

        // 씬 배치: 앞 벽(책장 있는 벽) 안쪽 면 z = 2.55, 책장(x -0.5~0.5) 오른쪽
        static readonly Vector3 ScenePosition = new Vector3(1.2f, 1.75f, 2.55f);

        // 본체 (m)
        const float BodyW = 0.48f, BodyH = 0.15f, BodyD = 0.028f;
        const float CornerR = 0.014f;
        const int CornerSeg = 4;
        const float DisplayZ = -BodyD - 0.0006f; // 앞면보다 0.6mm 앞 (z-fighting 방지)

        // 숫자 기울기 (LED 시계처럼 살짝 이탤릭)
        static readonly float Slant = Mathf.Tan(8f * Mathf.Deg2Rad);

        // 큰 숫자 HH:MM
        const float BigW = 0.05f, BigH = 0.10f, BigT = 0.011f;
        static readonly float[] BigX = { -0.175f, -0.115f, -0.025f, 0.035f };
        const float BigY = -0.05f;
        const float ColonX = -0.045f, ColonDy = 0.022f, ColonSize = 0.0055f;

        // 작은 숫자 MM/DD
        const float SmallW = 0.019f, SmallH = 0.04f, SmallT = 0.0055f;
        static readonly float[] SmallX = { 0.11f, 0.133f, 0.172f, 0.195f };
        const float SmallY = -0.05f;

        static readonly Color BodyColor = new Color(0.035f, 0.035f, 0.04f);
        static readonly Color FaceColor = new Color(0.012f, 0.012f, 0.014f);
        static readonly Color LedOnColor = new Color(1f, 0.09f, 0.05f);
        static readonly Color LedOffColor = new Color(0.03f, 0.009f, 0.009f);
        static readonly Color LabelDimColor = new Color(0.75f, 0.07f, 0.04f);
        static readonly Color GlowColor = new Color(1f, 0.72f, 0.42f);

        static readonly string[] SegNames = { "a", "b", "c", "d", "e", "f", "g" };

        [MenuItem("AeAeng/Build Wall Clock")]
        public static void Build()
        {
            EnsureFolder(ModelDir);
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);

            // ---- 메쉬 ----
            var meshes = new MeshStore(MeshPath);
            var bodyMesh = meshes.Get("WallClock_Body", BuildBody);
            var bigSegs = SegNames.Select((n, i) => meshes.Get($"Seg_Big_{n}", m => BuildSegment(m, i, BigW, BigH, BigT))).ToArray();
            var smallSegs = SegNames.Select((n, i) => meshes.Get($"Seg_Small_{n}", m => BuildSegment(m, i, SmallW, SmallH, SmallT))).ToArray();
            var dotMesh = meshes.Get("Colon_Dot", BuildDot);
            var slashMesh = meshes.Get("Slash", BuildSlash);
            meshes.Save();

            // ---- 재질 ----
            var mBody = LitMat("WallClock_Body", BodyColor, 0.3f);
            var mFace = LitMat("WallClock_Face", FaceColor, 0.9f);
            var mOn = UnlitMat("WallClock_LED_On", LedOnColor);
            // 꺼진 칸은 Lit: 앞면과 같은 빛을 받아서 어떤 조명에서도 앞면보다 살짝 붉게만 보임
            var mOff = LitMat("WallClock_LED_Off", LedOffColor, 0.9f);

            // ---- 프리팹 ----
            var root = new GameObject("WallClock");
            var clock = root.AddComponent<LedClock>();
            clock.onMaterial = mOn;
            clock.offMaterial = mOff;

            var body = Child(root.transform, "Body", Vector3.zero);
            body.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            var bodyMr = body.AddComponent<MeshRenderer>();
            bodyMr.sharedMaterials = new[] { mFace, mBody };
            NoShadows(bodyMr);

            var display = Child(root.transform, "Display", new Vector3(0f, 0f, DisplayZ)).transform;

            clock.hourDigits = new[]
            {
                MakeDigit(display, "Hour_1", new Vector2(BigX[0], BigY), bigSegs, mOff),
                MakeDigit(display, "Hour_2", new Vector2(BigX[1], BigY), bigSegs, mOff),
            };
            clock.minuteDigits = new[]
            {
                MakeDigit(display, "Minute_1", new Vector2(BigX[2], BigY), bigSegs, mOff),
                MakeDigit(display, "Minute_2", new Vector2(BigX[3], BigY), bigSegs, mOff),
            };
            clock.monthDigits = new[]
            {
                MakeDigit(display, "Month_1", new Vector2(SmallX[0], SmallY), smallSegs, mOff),
                MakeDigit(display, "Month_2", new Vector2(SmallX[1], SmallY), smallSegs, mOff),
            };
            clock.dayDigits = new[]
            {
                MakeDigit(display, "Day_1", new Vector2(SmallX[2], SmallY), smallSegs, mOff),
                MakeDigit(display, "Day_2", new Vector2(SmallX[3], SmallY), smallSegs, mOff),
            };

            // 콜론 (기울기만큼 위 점은 오른쪽으로)
            float cy = BigY + BigH / 2f;
            clock.colonDots = new[]
            {
                MakeMeshObject(display, "Colon_Top", new Vector3(ColonX + (BigH / 2f + ColonDy) * Slant, cy + ColonDy, 0f), dotMesh, mOn),
                MakeMeshObject(display, "Colon_Bottom", new Vector3(ColonX + (BigH / 2f - ColonDy) * Slant, cy - ColonDy, 0f), dotMesh, mOn),
            };
            MakeMeshObject(display, "Slash", Vector3.zero, slashMesh, mOn);

            // 글씨 (TextMeshPro, 화면과 같은 -Z 방향)
            clock.ampmLabel = Label(display, "AM_PM", "AM", new Vector2(-0.205f, 0.036f), new Vector2(0.03f, 0.016f), LedOnColor, TextAlignmentOptions.Center);
            clock.weekdayLabel = Label(display, "Weekday", "TUE", new Vector2(0.167f, 0.033f), new Vector2(0.105f, 0.034f), LedOnColor, TextAlignmentOptions.Center);
            Label(display, "Label_Month", "MONTH", new Vector2(0.133f, 0.006f), new Vector2(0.042f, 0.008f), LabelDimColor, TextAlignmentOptions.Center);
            Label(display, "Label_Date", "DATE", new Vector2(0.197f, 0.006f), new Vector2(0.034f, 0.008f), LabelDimColor, TextAlignmentOptions.Center);

            // 아래로 퍼지는 따뜻한 빛 (끄려면 Underglow 오브젝트를 비활성화)
            var glowGo = Child(root.transform, "Underglow", new Vector3(0f, -BodyH / 2f - 0.004f, -BodyD * 0.5f));
            glowGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 아래(-Y)를 향함
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Spot;
            glow.spotAngle = 150f;
            glow.innerSpotAngle = 60f;
            glow.range = 0.6f;
            glow.intensity = 0.25f;
            glow.color = GlowColor;
            glow.shadows = LightShadows.None;

            // 미리보기 값: 게임 시작 시각 새벽 2:00
            clock.useRealTime = true;
            clock.Show(new DateTime(2026, 11, 17, 2, 0, 0));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            bool placed = PlaceInSceneIfMissing(prefab);
            string preview = RenderPreviews(prefab);

            int tris = bodyMesh.triangles.Length / 3 + 4 * bigSegs.Sum(m => m.triangles.Length / 3)
                       + 4 * smallSegs.Sum(m => m.triangles.Length / 3) + 2 * dotMesh.triangles.Length / 3;
            Debug.Log($"[WallClockBuilder] tris {tris}, size {BodyW * 100f:0}x{BodyH * 100f:0}x{BodyD * 100f:0.0}cm, " +
                      $"placedInScene {placed}, preview {preview}");
        }

        /// <summary>씬에 게임 시간(RoundClock)을 추가한다. 이미 있으면 선택만 한다.</summary>
        [MenuItem("AeAeng/Add Round Clock (Game Time)")]
        public static void AddRoundClock()
        {
            var existing = Object.FindFirstObjectByType<RoundClock>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[WallClockBuilder] RoundClock이 이미 씬에 있습니다.");
                return;
            }
            var go = new GameObject("RoundClock");
            go.AddComponent<RoundClock>();
            Undo.RegisterCreatedObjectUndo(go, "Add Round Clock");
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log("[WallClockBuilder] RoundClock 추가. 씬을 저장(Ctrl+S)하세요.");
        }

        // ------------------------------------------------------------------
        // 메쉬
        // ------------------------------------------------------------------

        /// <summary>둥근 사각 본체. 서브메쉬 0 = 앞면(유광), 1 = 옆면 + 뒷면.</summary>
        static void BuildBody(Mesh mesh)
        {
            var outline = new List<Vector2>();
            var normals2 = new List<Vector2>();
            float hx = BodyW / 2f - CornerR, hy = BodyH / 2f - CornerR;
            var centers = new[] { new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy), new Vector2(-hx, -hy) };
            for (int c = 0; c < 4; c++)
            {
                float a0 = -90f + 90f * c;
                for (int s = 0; s <= CornerSeg; s++)
                {
                    float a = (a0 + 90f * s / CornerSeg) * Mathf.Deg2Rad;
                    var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    outline.Add(centers[c] + n * CornerR);
                    normals2.Add(n);
                }
            }
            int count = outline.Count; // 반시계 방향

            var v = new List<Vector3>();
            var nrm = new List<Vector3>();
            var front = new List<int>();
            var rest = new List<int>();

            // 앞면 (z = -BodyD, -Z를 봄)
            int fc = v.Count;
            v.Add(new Vector3(0f, 0f, -BodyD)); nrm.Add(Vector3.back);
            for (int i = 0; i < count; i++) { v.Add(new Vector3(outline[i].x, outline[i].y, -BodyD)); nrm.Add(Vector3.back); }
            for (int i = 0; i < count; i++) front.AddRange(new[] { fc, fc + 1 + (i + 1) % count, fc + 1 + i });

            // 뒷면 (z = 0, +Z를 봄)
            int bc = v.Count;
            v.Add(Vector3.zero); nrm.Add(Vector3.forward);
            for (int i = 0; i < count; i++) { v.Add(new Vector3(outline[i].x, outline[i].y, 0f)); nrm.Add(Vector3.forward); }
            for (int i = 0; i < count; i++) rest.AddRange(new[] { bc, bc + 1 + i, bc + 1 + (i + 1) % count });

            // 옆면 (둥근 모서리는 부드러운 노멀)
            int sf = v.Count;
            for (int i = 0; i < count; i++)
            {
                var n = new Vector3(normals2[i].x, normals2[i].y, 0f);
                v.Add(new Vector3(outline[i].x, outline[i].y, -BodyD)); nrm.Add(n);
                v.Add(new Vector3(outline[i].x, outline[i].y, 0f)); nrm.Add(n);
            }
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                int f0 = sf + i * 2, b0 = f0 + 1, f1 = sf + j * 2, b1 = f1 + 1;
                rest.AddRange(new[] { f0, f1, b1, f0, b1, b0 });
            }

            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetNormals(nrm);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(rest, 1);
            mesh.RecalculateBounds();
        }

        /// <summary>7-세그먼트 한 칸. 원점 = 숫자 칸의 왼쪽 아래. 양 끝이 뾰족한 육각형.</summary>
        static void BuildSegment(Mesh mesh, int index, float w, float h, float t)
        {
            float ht = t / 2f;
            Vector2 p0, p1;
            switch (index)
            {
                case 0: p0 = new Vector2(ht, h - ht); p1 = new Vector2(w - ht, h - ht); break;     // a
                case 1: p0 = new Vector2(w - ht, h / 2f); p1 = new Vector2(w - ht, h - ht); break; // b
                case 2: p0 = new Vector2(w - ht, ht); p1 = new Vector2(w - ht, h / 2f); break;     // c
                case 3: p0 = new Vector2(ht, ht); p1 = new Vector2(w - ht, ht); break;             // d
                case 4: p0 = new Vector2(ht, ht); p1 = new Vector2(ht, h / 2f); break;             // e
                case 5: p0 = new Vector2(ht, h / 2f); p1 = new Vector2(ht, h - ht); break;         // f
                default: p0 = new Vector2(ht, h / 2f); p1 = new Vector2(w - ht, h / 2f); break;    // g
            }
            var poly = Hexagon(p0, p1, ht, t * 0.18f);
            for (int i = 0; i < poly.Count; i++) poly[i] = Italic(poly[i], 0f);
            FlatPolygon(mesh, poly);
        }

        static void BuildDot(Mesh mesh)
        {
            float s = ColonSize;
            var poly = new List<Vector2> { new Vector2(-s, -s), new Vector2(s, -s), new Vector2(s, s), new Vector2(-s, s) };
            for (int i = 0; i < poly.Count; i++) poly[i] = Italic(poly[i], 0f);
            FlatPolygon(mesh, poly);
        }

        /// <summary>월/일 사이의 / (Display 기준 좌표로 만듦)</summary>
        static void BuildSlash(Mesh mesh)
        {
            float x0 = SmallX[1] + SmallW + 0.004f;
            var a = new Vector2(x0, SmallY + 0.002f);
            var b = new Vector2(x0 + 0.011f, SmallY + SmallH - 0.002f);
            FlatPolygon(mesh, Hexagon(a, b, 0.0022f, 0f));
        }

        static Vector2 Italic(Vector2 p, float baseY) => new Vector2(p.x + (p.y - baseY) * Slant, p.y);

        /// <summary>p0→p1 선분을 두께 2*ht의 뾰족한 육각형으로. gap만큼 양 끝을 줄임.</summary>
        static List<Vector2> Hexagon(Vector2 p0, Vector2 p1, float ht, float gap)
        {
            var dir = (p1 - p0).normalized;
            var n = new Vector2(-dir.y, dir.x);
            var q0 = p0 + dir * gap;
            var q1 = p1 - dir * gap;
            return new List<Vector2>
            {
                q0, q0 + dir * ht + n * ht, q1 - dir * ht + n * ht,
                q1, q1 - dir * ht - n * ht, q0 + dir * ht - n * ht,
            };
        }

        /// <summary>z = 0 평면의 볼록 다각형. 앞면이 -Z를 보도록 감는다.</summary>
        static void FlatPolygon(Mesh mesh, List<Vector2> poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                area += a.x * b.y - b.x * a.y;
            }
            bool ccw = area > 0f;

            var c = Vector2.zero;
            foreach (var p in poly) c += p;
            c /= poly.Count;

            var v = new List<Vector3> { c };
            foreach (var p in poly) v.Add(p);
            var tri = new List<int>();
            for (int i = 0; i < poly.Count; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % poly.Count;
                // -Z에서 볼 때 시계 방향이어야 앞면
                if (ccw) tri.AddRange(new[] { 0, b, a });
                else tri.AddRange(new[] { 0, a, b });
            }

            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetNormals(Enumerable.Repeat(Vector3.back, v.Count).ToList());
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>메쉬 여러 개를 .asset 파일 하나에 모아 저장. 다시 실행하면 같은 메쉬를 덮어써서 참조가 유지됨.</summary>
        class MeshStore
        {
            readonly string _path;
            readonly Dictionary<string, Mesh> _existing = new Dictionary<string, Mesh>();
            Object _main;

            public MeshStore(string path)
            {
                _path = path;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Mesh m) _existing[m.name] = m;
                _main = AssetDatabase.LoadMainAssetAtPath(path);
            }

            public Mesh Get(string name, Action<Mesh> fill)
            {
                if (_existing.TryGetValue(name, out var mesh))
                {
                    fill(mesh);
                    EditorUtility.SetDirty(mesh);
                    return mesh;
                }
                mesh = new Mesh { name = name };
                fill(mesh);
                if (_main == null)
                {
                    AssetDatabase.CreateAsset(mesh, _path);
                    _main = mesh;
                }
                else AssetDatabase.AddObjectToAsset(mesh, _main);
                _existing[name] = mesh;
                return mesh;
            }

            public void Save()
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(_path);
            }
        }

        // ------------------------------------------------------------------
        // 오브젝트
        // ------------------------------------------------------------------

        static GameObject Child(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        static LedClock.Digit MakeDigit(Transform parent, string name, Vector2 origin, Mesh[] segMeshes, Material mat)
        {
            var go = Child(parent, name, new Vector3(origin.x, origin.y, 0f));
            var d = new LedClock.Digit();
            for (int i = 0; i < 7; i++)
                d.segments[i] = MakeMeshObject(go.transform, SegNames[i], Vector3.zero, segMeshes[i], mat);
            return d;
        }

        static MeshRenderer MakeMeshObject(Transform parent, string name, Vector3 localPos, Mesh mesh, Material mat)
        {
            var go = Child(parent, name, localPos);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            NoShadows(mr);
            return mr;
        }

        static TextMeshPro Label(Transform parent, string name, string text, Vector2 center, Vector2 size, Color color,
                                 TextAlignmentOptions align)
        {
            var go = Child(parent, name, new Vector3(center.x, center.y, 0f));
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.001f;
            tmp.fontSizeMax = 10f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.margin = Vector4.zero;
            tmp.color = color;
            tmp.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) NoShadows(mr);
            return tmp;
        }

        static void NoShadows(MeshRenderer mr)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ------------------------------------------------------------------
        // 재질
        // ------------------------------------------------------------------

        static Material GetOrCreateMat(string name, string shaderName)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
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
            var mat = GetOrCreateMat(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>LED는 조명과 상관없이 같은 밝기로 보여야 하므로 Unlit.</summary>
        static Material UnlitMat(string name, Color color)
        {
            var mat = GetOrCreateMat(name, "Universal Render Pipeline/Unlit");
            mat.SetColor("_BaseColor", color);
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
            inst.transform.SetPositionAndRotation(ScenePosition, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(inst, "Place Wall Clock");
            EditorSceneManager.MarkSceneDirty(inst.scene);
            Selection.activeGameObject = inst;
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
                cam.backgroundColor = new Color(0.62f, 0.62f, 0.60f);
                pru.lights[0].intensity = 1.0f;
                pru.lights[0].transform.rotation = Quaternion.Euler(30f, 20f, 0f);
                pru.lights[1].intensity = 0.5f;
                pru.ambientColor = new Color(0.35f, 0.35f, 0.37f);
                pru.InstantiatePrefabInScene(prefab);

                var c = new Vector3(0f, 0f, -BodyD);
                Shot(pru, "front", new Vector3(0f, 0f, -0.95f), c);
                Shot(pru, "front34", new Vector3(0.45f, 0.12f, -0.75f), c);
                Shot(pru, "side", new Vector3(0.6f, 0.05f, -0.15f), c);
                return Path.GetFullPath(PreviewDir);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WallClockBuilder] preview failed: {e}");
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
            pru.camera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target - pos, Vector3.up));
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
