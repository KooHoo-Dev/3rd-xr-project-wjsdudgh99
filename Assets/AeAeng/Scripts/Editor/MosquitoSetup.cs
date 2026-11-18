using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AeAeng.EditorTools
{
    /// <summary>
    /// 모기 게임 준비 (AeAeng > Setup Mosquitoes). 다시 실행해도 됨.
    ///  1) 착지 지점 자동 생성: 침대 자리(머리)에서 손이 닿는 범위(0.3~0.9m)의 벽/가구 표면에 LandingPoint 를 뿌림
    ///     (높이 0.6~2.0m, 등 뒤 머리 쪽 벽 제외, 0.18m 간격). 가구를 옮겼으면 다시 실행.
    ///  2) 모기 프리팹: Mosquito + 3D AudioSource("애앵~" 루프), 판정 콜라이더 넉넉하게
    ///  3) 파리채 프리팹: 채 머리 속도 추적 + SwatterHit
    ///  4) 오른손: 속도 추적(눈치 판정) + HandCatch(맨손 잡기)
    ///  5) 씬에 MosquitoSwarm (한 판 3마리)
    /// </summary>
    public static class MosquitoSetup
    {
        const string MosquitoPrefabPath = "Assets/AeAeng/Prefabs/Mosquito.prefab";
        const string SwatterPrefabPath = "Assets/AeAeng/Prefabs/FlySwatter.prefab";
        const string BuzzClipPath = "Assets/AeAeng/Sound/freesound_community-single-mosquito-buzz-69360.mp3";
        const string LandingRootName = "MosquitoLandingPoints";

        // 착지 지점 조건 (기획서 + 11/18 결정: 손 닿는 범위)
        const float SeatEyeHeight = 1.2f;
        const float MinReach = 0.3f, MaxReach = 0.9f;
        const float MinHeight = 0.6f, MaxHeight = 2.0f;
        const float Spacing = 0.18f;
        // 40개로 막으면 위쪽 방향부터 뽑혀서 아래쪽(책상, 침대 주변)이 잘림 → 넉넉하게
        const int MaxPoints = 80;

        [MenuItem("AeAeng/Setup Mosquitoes")]
        public static void Setup()
        {
            EditPrefab(MosquitoPrefabPath, EnsureMosquito);
            EditPrefab(SwatterPrefabPath, EnsureSwatterHit);

            int points = BuildLandingPoints();
            string hand = SetupRightHand();
            bool swarm = EnsureSwarm();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[MosquitoSetup] 착지 지점 {points}곳, 오른손 {hand}, MosquitoSwarm 추가 {swarm}. 씬을 저장하세요.");
        }

        static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { Debug.LogError($"[MosquitoSetup] {path} 이 없습니다."); return; }
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ------------------------------------------------------------------
        // 프리팹
        // ------------------------------------------------------------------

        /// <summary>모기 프리팹 루트에 Mosquito / AudioSource 설정 (MosquitoBuilder 에서도 호출)</summary>
        public static void EnsureMosquito(GameObject root)
        {
            var audio = root.GetComponent<AudioSource>(); if (audio == null) audio = root.AddComponent<AudioSource>();
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BuzzClipPath);
            audio.loop = true;
            audio.playOnAwake = false;
            audio.volume = 0.8f;
            audio.spatialBlend = 1f;               // 3D
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 0.12f;             // 귓가에서 크게
            audio.maxDistance = 4f;
            audio.dopplerLevel = 0f;
            audio.spread = 0f;
            if (audio.clip == null) Debug.LogWarning($"[MosquitoSetup] 모기 소리 {BuzzClipPath} 를 찾지 못했습니다.");

            if (root.GetComponent<Mosquito>() == null) root.AddComponent<Mosquito>();

            // 판정 콜라이더: 눈에 보이는 몸(3cm)보다 넉넉하게
            var col = root.GetComponent<SphereCollider>(); if (col == null) col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = Vector3.zero;
            col.radius = 0.035f;
        }

        /// <summary>파리채 프리팹에 채 머리 속도 추적 + SwatterHit (FlySwatterBuilder 에서도 호출)</summary>
        public static void EnsureSwatterHit(GameObject root)
        {
            var headT = root.transform.Find("HeadCenter");
            if (headT == null)
            {
                headT = new GameObject("HeadCenter").transform;
                headT.SetParent(root.transform, false);
            }
            headT.localPosition = new Vector3(0f, 0.38f, 0f); // 채 머리 가운데 (원점 = 손잡이 끝)
            headT.localRotation = Quaternion.identity;
            var tracker = headT.GetComponent<VelocityTracker>(); if (tracker == null) tracker = headT.gameObject.AddComponent<VelocityTracker>();
            tracker.alertsMosquitoes = true;

            var hit = root.GetComponent<SwatterHit>(); if (hit == null) hit = root.AddComponent<SwatterHit>();
            hit.head = tracker;
            hit.grab = root.GetComponent<SwatterGrab>();
        }

        // ------------------------------------------------------------------
        // 오른손 / 씬
        // ------------------------------------------------------------------

        static string SetupRightHand()
        {
            HandPose right = null;
            foreach (var h in Object.FindObjectsByType<HandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (h.side == HandPose.Side.Right) right = h;
            if (right == null) return "없음 (AeAeng > Build Hands 먼저)";

            var go = right.gameObject;
            if (go.GetComponent<VelocityTracker>() == null) Undo.AddComponent<VelocityTracker>(go);
            var c = go.GetComponent<HandCatch>(); if (c == null) c = Undo.AddComponent<HandCatch>(go);
            c.palm = go.transform.Find(SwatterGripSetup.GripName);
            if (c.palm == null) Debug.LogWarning("[MosquitoSetup] 오른손에 SwatterGrip 이 없습니다. AeAeng > Setup Swatter Grip 을 실행하세요.");
            return "속도 추적 + 맨손 잡기";
        }

        static bool EnsureSwarm()
        {
            var swarm = Object.FindFirstObjectByType<MosquitoSwarm>(FindObjectsInactive.Include);
            bool added = false;
            if (swarm == null)
            {
                var go = new GameObject("MosquitoSwarm");
                Undo.RegisterCreatedObjectUndo(go, "Add MosquitoSwarm");
                swarm = go.AddComponent<MosquitoSwarm>();
                added = true;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MosquitoPrefabPath);
            swarm.prefab = prefab != null ? prefab.GetComponent<Mosquito>() : null;
            EditorUtility.SetDirty(swarm);
            return added;
        }

        // ------------------------------------------------------------------
        // 착지 지점
        // ------------------------------------------------------------------

        static int BuildLandingPoints()
        {
            var old = GameObject.Find(LandingRootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            // 머리 위치 = 침대 자리 (StationaryPlayer.seatPosition), 앉은 눈높이
            var seat = Vector3.zero;
            float yaw = 0f;
            var player = Object.FindFirstObjectByType<StationaryPlayer>(FindObjectsInactive.Include);
            if (player != null) { seat = player.seatPosition; yaw = player.seatYaw; }
            var head = new Vector3(seat.x, SeatEyeHeight, seat.z);
            var forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            var root = new GameObject(LandingRootName);
            Undo.RegisterCreatedObjectUndo(root, "Landing Points");

            // 가구 애셋의 콜라이더는 대충 씌운 상자라(램프, 책상 등) 표면에서 떠 있는 지점이 생김
            // → 착지 지점을 찾는 동안만 실제 모양(MeshCollider)으로 바꿔서 쏘고, 끝나면 원래대로
            var temp = new List<MeshCollider>();
            var disabled = new List<Collider>();
            UseExactShapes(temp, disabled);
            try
            {
                return BuildPoints(root, head, forward);
            }
            finally
            {
                foreach (var mc in temp) if (mc != null) Object.DestroyImmediate(mc);
                foreach (var c in disabled) if (c != null) c.enabled = true;
                Physics.SyncTransforms();
            }
        }

        /// <summary>방 구조(TestBedroom: 벽/바닥은 원래 정확한 상자)를 뺀 나머지 물체에 잠깐 MeshCollider 를 붙이고 기존 대충 콜라이더를 끔</summary>
        static void UseExactShapes(List<MeshCollider> temp, List<Collider> disabled)
        {
            var roomRoot = GameObject.Find("TestBedroom");
            bool InRoom(Transform t) => roomRoot != null && t.IsChildOf(roomRoot.transform);

            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!c.enabled || c.isTrigger || c is MeshCollider || InRoom(c.transform) || IsPlayerStuff(c)) continue;
                c.enabled = false;
                disabled.Add(c);
            }
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null || InRoom(mf.transform)) continue;
                if (mf.GetComponent<MeshCollider>() != null) continue;
                if (mf.GetComponentInParent<XROrigin>() != null || mf.GetComponentInParent<SwatterGrab>() != null ||
                    mf.GetComponentInParent<Mosquito>() != null || mf.GetComponentInParent<HandPose>() != null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
            }
            Physics.SyncTransforms();
        }

        static int BuildPoints(GameObject root, Vector3 head, Vector3 forward)
        {
            var picked = new List<Vector3>();
            const int samples = 900;
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < samples && picked.Count < MaxPoints; i++)
            {
                // 피보나치 구로 고르게 방향 뽑기
                float y = 1f - (i + 0.5f) / samples * 2f;
                float r = Mathf.Sqrt(1f - y * y);
                float th = golden * i;
                var dir = new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);

                if (!Physics.Raycast(head, dir, out var hit, MaxReach, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.distance < MinReach) continue;
                var p = hit.point;
                if (p.y < MinHeight || p.y > MaxHeight) continue;
                if (Vector3.Dot(p - head, forward) < -0.2f) continue;      // 등 뒤(머리 쪽 벽)는 착지 안 함
                if (hit.normal.y < -0.3f) continue;                          // 아래를 보는 면(밑면) 제외
                if (IsPlayerStuff(hit.collider)) continue;
                bool tooClose = false;
                foreach (var q in picked)
                    if ((q - p).sqrMagnitude < Spacing * Spacing) { tooClose = true; break; }
                if (tooClose) continue;

                picked.Add(p);
                AddPoint(root, $"LP_{picked.Count:00}", p, hit.normal);
            }

            int wallCount = picked.Count;
            int bedCount = BuildBedPoints(root, head, forward, picked);
            Debug.Log($"[MosquitoSetup] 착지 지점: 벽/가구 {wallCount}곳 + 침대 위 {bedCount}곳");
            return picked.Count;
        }

        static void AddPoint(GameObject root, string name, Vector3 p, Vector3 normal)
        {
            var lp = new GameObject(name);
            lp.transform.SetParent(root.transform, false);
            lp.transform.SetPositionAndRotation(p + normal * 0.002f, Quaternion.LookRotation(normal));
            lp.AddComponent<LandingPoint>();
        }

        // ------------------------------------------------------------------
        // 침대 위 착지 지점 (이불/베개 위)
        // 침대 애셋(JaneArtWork)에는 콜라이더가 없어서, 잠깐 MeshCollider 를 붙여 위에서 아래로 쏴 보고 다시 뗀다.
        // 침대 윗면은 0.6m 보다 낮으므로 높이 조건을 따로 둠. 플레이어가 앉은 자리 바로 주변은 제외.
        // ------------------------------------------------------------------

        const string BedAssetPath = "Assets/JaneArtWork/";
        const int MaxBedPoints = 8;
        const float BedGridStep = 0.1f;
        const float BedMinHeight = 0.3f;
        const float SeatClearRadius = 0.35f;   // 앉은 자리(엉덩이/다리) 주변은 착지 안 함
        const float BedReach = 0.95f;          // 앉아서 몸을 숙여 닿는 거리 (머리에서)

        static int BuildBedPoints(GameObject root, Vector3 head, Vector3 forward, List<Vector3> picked)
        {
            // 침대 부품 (UseExactShapes 가 이미 MeshCollider 를 붙여 둠)
            var bedRoots = new List<Transform>();
            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                var path = src != null ? AssetDatabase.GetAssetPath(src) : null;
                if (string.IsNullOrEmpty(path) || !path.StartsWith(BedAssetPath)) continue;
                bedRoots.Add(t);
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                    else bounds.Encapsulate(r.bounds);
                }
            }
            if (!hasBounds) return 0;
            bool IsBed(Transform x) => bedRoots.Exists(b => x.IsChildOf(b));

            int added = 0;
            {
                var candidates = new List<(Vector3 p, Vector3 n, float d)>();
                for (float x = bounds.min.x; x <= bounds.max.x; x += BedGridStep)
                for (float z = bounds.min.z; z <= bounds.max.z; z += BedGridStep)
                {
                    var from = new Vector3(x, bounds.max.y + 0.2f, z);
                    if (!Physics.Raycast(from, Vector3.down, out var hit, bounds.size.y + 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!IsBed(hit.collider.transform)) continue;   // 침대 표면만
                    var p = hit.point;
                    if (hit.normal.y < 0.6f || p.y < BedMinHeight) continue;            // 위를 보는 면
                    var flat = new Vector3(p.x - head.x, 0f, p.z - head.z);
                    if (flat.magnitude < SeatClearRadius) continue;                      // 앉은 자리 제외
                    float d = Vector3.Distance(p, head);
                    if (d > BedReach) continue;
                    if (Vector3.Dot(flat, forward) < -0.15f) continue;                   // 등 뒤(베개 쪽 너무 뒤) 제외
                    candidates.Add((p, hit.normal, d));
                }

                // 가까운 곳부터, 간격을 두고 MaxBedPoints 개까지
                candidates.Sort((a, b) => a.d.CompareTo(b.d));
                foreach (var c in candidates)
                {
                    if (added >= MaxBedPoints) break;
                    bool tooClose = false;
                    foreach (var q in picked)
                        if ((q - c.p).sqrMagnitude < Spacing * Spacing) { tooClose = true; break; }
                    if (tooClose) continue;
                    picked.Add(c.p);
                    added++;
                    AddPoint(root, $"LP_Bed_{added:00}", c.p, c.n);
                }
            }
            return added;
        }

        static bool IsPlayerStuff(Collider c) =>
            c.GetComponentInParent<XROrigin>() != null ||
            c.GetComponentInParent<SwatterGrab>() != null ||
            c.GetComponentInParent<Mosquito>() != null ||
            c.GetComponentInParent<HandPose>() != null;
    }
}
