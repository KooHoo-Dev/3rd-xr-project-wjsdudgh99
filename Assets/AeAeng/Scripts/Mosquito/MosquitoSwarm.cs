using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;

namespace AeAeng
{
    /// <summary>
    /// 한 판의 모기들을 관리한다: 등장, 착지 지점 배정, 불 상태, 잡은 수, 메시지.
    /// 모기 수치(기획서 초기값)도 여기 Settings 에 모아 두고 헤드셋 테스트로 조정한다.
    /// 판 진행(제한 시간, 파티)은 GameManager 가 SpawnRound / DespawnAll 과 이벤트로 처리.
    /// </summary>
    public class MosquitoSwarm : MonoBehaviour
    {
        [Serializable]
        public class Settings
        {
            [Header("비행")]
            public float flySpeed = 0.6f;
            public float acceleration = 2.5f;
            [Tooltip("불규칙한 흔들림 크기 (m/s)")]
            public float wobble = 0.35f;
            [Tooltip("평소 비행 거리: 머리에서 min ~ max (m)")]
            public Vector2 flyDistance = new Vector2(0.45f, 1.2f);
            [Tooltip("비행 높이 (m)")]
            public Vector2 flyHeight = new Vector2(0.6f, 2.1f);

            [Header("Approach (귓가 맴돌기)")]
            [Tooltip("Fly 중 이 간격(초)마다 한 번씩 귓가로 옴")]
            public Vector2 approachEvery = new Vector2(8f, 16f);
            public float approachSeconds = 4f;
            public float approachRadius = 0.28f;
            public float approachOrbitSpeed = 70f;

            [Header("착지")]
            [Tooltip("불을 켜고 이 시간(초) 동안 더 날다가 착지")]
            public float toWallDelay = 3f;
            [Tooltip("표면에서 띄우는 거리 (다리 길이)")]
            public float landOffset = 0.006f;

            [Header("눈치 판정")]
            public float alertRadius = 0.6f;
            public float alertSpeed = 1.0f;
            [Tooltip("눈치채고 날아오르기까지 (초). 이 사이에 치면 잡힘")]
            public float alertDelay = 0.25f;
            public float escapeSpeed = 2.2f;
            public float escapeSeconds = 0.6f;
            [Tooltip("불을 끄고 이 시간(초)이 지나면 숨은 모기가 다시 나옴")]
            public float respawnAfterDark = 5f;

            [Header("잡기")]
            [Tooltip("맨손 잡기 사용 (11/18 결정: 뺌 → 파리채로만 잡음)")]
            public bool handCatch = false;
            [Tooltip("이보다 가까우면 파리채로 못 잡음. 맨손 잡기를 뺐으므로 0 (어디서든 파리채)")]
            public float nearZone = 0f;
            [Tooltip("(맨손 잡기를 켤 때만) 이보다 멀면 맨손으로 못 잡음")]
            public float handReach = 0.7f;
            [Tooltip("잡힌 뒤 납작한 채로 남아 있는 시간 (초)")]
            public float caughtLinger = 1.0f;

            [Header("소리")]
            [Range(0f, 1f)] public float buzzVolume = 0.8f;
        }

        public enum Msg { Escaped, TooClose, Caught }

        [Header("참조 (비워 두면 자동으로 찾음)")]
        public Mosquito prefab;
        public RoomLightController room;
        public Transform head;

        [Header("판")]
        [Tooltip("한 판의 모기 수 (기획: 3)")]
        public int count = 3;
        [Tooltip("플레이를 누르면 바로 모기 등장 (GameManager 가 생기기 전 테스트용)")]
        public bool spawnOnStart = true;
        [Tooltip("파리채가 너무 가까울 때 안내 자막 (튜토리얼에서만)")]
        public bool showTooCloseHint = true;

        [Header("모기가 날 수 있는 방 안 영역 (월드)")]
        public Bounds roomBounds = new Bounds(new Vector3(0.9f, 1.25f, 1.05f), new Vector3(2.6f, 1.9f, 2.6f));

        public Settings settings = new Settings();

        [Header("이벤트")]
        public UnityEvent<string> onMessage;
        public UnityEvent<Mosquito> onCaught;
        public UnityEvent onAllCaught;
        [Tooltip("모기 한 마리가 눈치채고 도망감 (알림: 모기 N마리가 사라졌다!)")]
        public UnityEvent onEscaped;

        public int Total { get; private set; }
        public int Caught { get; private set; }
        public bool LightOn => room != null && room.IsOn;
        public float LightOffSeconds { get; private set; }
        public Vector3 HeadPosition => head != null ? head.position : new Vector3(0f, 1.2f, 0f);
        public IReadOnlyList<Mosquito> Active => _active;

        readonly List<Mosquito> _active = new List<Mosquito>();
        readonly List<Mosquito> _pool = new List<Mosquito>();
        LandingPoint[] _landing = Array.Empty<LandingPoint>();
        string _lastMessage;
        float _lastMessageTime;

        void Awake()
        {
            if (room == null) room = FindFirstObjectByType<RoomLightController>();
            if (head == null)
            {
                var origin = FindFirstObjectByType<XROrigin>();
                if (origin != null && origin.Camera != null) head = origin.Camera.transform;
                else if (Camera.main != null) head = Camera.main.transform;
            }
            _landing = FindObjectsByType<LandingPoint>(FindObjectsSortMode.None);
            if (_landing.Length == 0)
                Debug.LogWarning("[MosquitoSwarm] 착지 지점이 없습니다. AeAeng > Setup Mosquitoes 를 실행하세요.");
        }

        void Start()
        {
            // 씬에 미리 놓인 모기도 함께 씀 (모든 Awake 가 끝난 뒤라 Start 에서)
            foreach (var m in FindObjectsByType<Mosquito>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (_pool.Contains(m)) continue;
                m.swarm = this;
                m.gameObject.SetActive(true);
                m.Despawn();
                _pool.Add(m);
            }
            if (spawnOnStart) SpawnRound(count);
        }

        void Update()
        {
            LightOffSeconds = LightOn ? 0f : LightOffSeconds + Time.deltaTime;
        }

        // ------------------------------------------------------------------
        // 판
        // ------------------------------------------------------------------

        /// <summary>새 판: n 마리를 멀리서 등장시킴</summary>
        public void SpawnRound(int n)
        {
            DespawnAll();
            Total = n;
            Caught = 0;
            for (int i = 0; i < n; i++)
            {
                var m = GetFromPool(i);
                if (m == null) break;
                _active.Add(m);
                m.Spawn(RandomFarPoint());
            }
            Debug.Log($"[MosquitoSwarm] 모기 {_active.Count}마리 등장 (착지 지점 {_landing.Length}곳)");
        }

        /// <summary>모기와 댄싱 파티: 못 잡은 모기가 모두 머리 주변에서 맴돎</summary>
        public void Dance()
        {
            foreach (var m in _active) if (m != null) m.Dance();
        }

        public void DespawnAll()
        {
            foreach (var m in _active) if (m != null) m.Despawn();
            _active.Clear();
        }

        Mosquito GetFromPool(int i)
        {
            while (_pool.Count <= i)
            {
                if (prefab == null)
                {
                    Debug.LogWarning("[MosquitoSwarm] 모기 프리팹이 없습니다.");
                    return null;
                }
                var m = Instantiate(prefab, transform);
                m.name = $"Mosquito_{_pool.Count + 1}";
                m.swarm = this;
                _pool.Add(m);
            }
            return _pool[i];
        }

        public void NotifyCaught(Mosquito m, Mosquito.Method method)
        {
            Caught++;
            Debug.Log($"[MosquitoSwarm] 잡음! ({method}) {Caught}/{Total}");
            onCaught?.Invoke(m);
            if (Caught >= Total) onAllCaught?.Invoke();
        }

        public void Message(Msg msg)
        {
            if (msg == Msg.Escaped)
            {
                // 11/18: 문구 대신 이벤트 → 알림(토스트)이 0.6초 안의 도망을 묶어 "모기 N마리가 사라졌다!"
                Debug.Log("[MosquitoSwarm] 모기가 도망감");
                onEscaped?.Invoke();
                return;
            }
            string text = msg switch
            {
                Msg.Escaped => "모기가 사라졌습니다",
                Msg.TooClose => showTooCloseHint ? "너무 가까워요! 손으로 잡아보세요" : null,
                _ => null,
            };
            if (string.IsNullOrEmpty(text)) return;
            // 같은 자막이 연달아 쏟아지지 않게 (자막은 2초간 표시)
            if (text == _lastMessage && Time.unscaledTime - _lastMessageTime < 2.5f) return;
            _lastMessage = text;
            _lastMessageTime = Time.unscaledTime;
            Debug.Log($"[MosquitoSwarm] 자막: {text}");
            onMessage?.Invoke(text);
        }

        // ------------------------------------------------------------------
        // 위치
        // ------------------------------------------------------------------

        /// <summary>머리 주변 비행 지점 (방 안, 높이 제한)</summary>
        public Vector3 RandomFlyPoint() => RandomAroundHead(settings.flyDistance.x, settings.flyDistance.y);

        /// <summary>등장 지점: 머리에서 멀리 (소리가 멀리서 다가오게)</summary>
        public Vector3 RandomFarPoint() => RandomAroundHead(settings.flyDistance.y * 0.85f, settings.flyDistance.y * 1.1f);

        Vector3 RandomAroundHead(float minD, float maxD)
        {
            var h = HeadPosition;
            var inner = new Bounds(roomBounds.center, roomBounds.size - Vector3.one * 0.2f);
            for (int i = 0; i < 20; i++)
            {
                var dir = UnityEngine.Random.onUnitSphere;
                dir.y *= 0.6f; // 너무 위아래로 치우치지 않게
                var p = h + dir.normalized * UnityEngine.Random.Range(minD, maxD);
                p.y = Mathf.Clamp(p.y, settings.flyHeight.x, settings.flyHeight.y);
                if (inner.Contains(p)) return p;
            }
            var fallback = inner.ClosestPoint(h + UnityEngine.Random.onUnitSphere * maxD);
            fallback.y = Mathf.Clamp(fallback.y, settings.flyHeight.x, settings.flyHeight.y);
            return fallback;
        }

        /// <summary>from 에서 가까운 빈 착지 지점을 하나 맡아 둠 (가까운 3곳 중 랜덤)</summary>
        public LandingPoint ReserveLanding(Mosquito m, Vector3 from)
        {
            LandingPoint a = null, b = null, c = null;
            float da = float.MaxValue, db = float.MaxValue, dc = float.MaxValue;
            foreach (var p in _landing)
            {
                if (p == null || !p.IsFree) continue;
                float d = (p.transform.position - from).sqrMagnitude;
                if (d < da) { c = b; dc = db; b = a; db = da; a = p; da = d; }
                else if (d < db) { c = b; dc = db; b = p; db = d; }
                else if (d < dc) { c = p; dc = d; }
            }
            var pick = UnityEngine.Random.value switch { < 0.5f => a, < 0.8f => b ?? a, _ => c ?? b ?? a };
            if (pick != null) pick.occupant = m;
            return pick;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(roomBounds.center, roomBounds.size);
            var h = Application.isPlaying ? HeadPosition : new Vector3(0f, 1.2f, 0f);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(h, settings.flyDistance.y);
            Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(h, settings.nearZone);
        }
#endif
    }
}
