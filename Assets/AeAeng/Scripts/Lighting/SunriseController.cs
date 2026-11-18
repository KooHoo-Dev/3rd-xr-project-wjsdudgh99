using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 게임 시각에 맞춰 창밖에 해가 뜨는 연출.
    /// 게임 시각 startHour(5시 30분, 판 끝나기 45초 전) → endHour(7시) 동안
    ///  - 창밖 하늘: 남색 밤 → 붉은 새벽 → 맑은 아침
    ///  - 창문으로 들어오는 빛: 달빛이 꺼지고, 같은 창문 쪽에서 붉은 해가 낮게 떠서 점점 높고 밝아짐 (RoomLightController.SetDawn)
    ///  - 불 끈 방의 Ambient 도 조금씩 밝아짐
    /// 판이 끝나 7시에 멈추면 아침 상태를 유지하고, 다음 판이 시작되면(시계가 1시로 돌아가면) 다시 밤.
    /// </summary>
    public class SunriseController : MonoBehaviour
    {
        public RoundClock roundClock;
        public RoomLightController room;
        [Tooltip("창밖 하늘 면 (Unlit). 비워 두면 이름 NightSky 를 찾음")]
        public Renderer sky;

        [Header("시간 (게임 시각)")]
        [Tooltip("해 뜨기 시작 게임 시각 (5.5 = 5시 30분, 판 끝나기 45초 전)")]
        [Range(0f, 23f)] public float startHour = 5.5f;
        [Range(1f, 24f)] public float endHour = 7f;

        [Header("하늘 색 (밤 → 새벽 → 아침)")]
        public Gradient skyColor = DefaultSky();

        [Header("테스트")]
        [Tooltip("0 이상이면 시계 대신 이 값으로 해 뜨기 진행도를 보여줌 (에디터 확인용, -1 = 끔)")]
        [Range(-1f, 1f)] public float previewDawn = -1f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;

        /// <summary>지금 해 뜨기 진행도 0 ~ 1</summary>
        public float Dawn { get; private set; }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (roundClock == null) roundClock = FindFirstObjectByType<RoundClock>();
            if (room == null) room = FindFirstObjectByType<RoomLightController>();
            if (sky == null)
            {
                var go = GameObject.Find("NightSky");
                if (go != null) sky = go.GetComponent<Renderer>();
            }
        }

        void Update()
        {
            Dawn = previewDawn >= 0f ? previewDawn : FromClock();
            if (room != null) room.SetDawn(Dawn);
            if (sky != null)
            {
                sky.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, skyColor.Evaluate(Dawn));
                sky.SetPropertyBlock(_mpb);
            }
        }

        float FromClock()
        {
            if (roundClock == null) return 0f;
            // 판이 시작되지 않았으면(경과 0) 밤, 7시에 멈췄으면 아침 유지
            return Mathf.Clamp01(Mathf.InverseLerp(startHour, endHour, roundClock.GameHour));
        }

        static Gradient DefaultSky()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.05f, 0.08f, 0.18f), 0f),    // 밤 (지금 NightSky 색)
                    new GradientColorKey(new Color(0.35f, 0.22f, 0.35f), 0.2f),  // 보랏빛 여명
                    new GradientColorKey(new Color(0.95f, 0.55f, 0.35f), 0.45f), // 붉은 새벽
                    new GradientColorKey(new Color(1.00f, 0.80f, 0.60f), 0.7f),  // 주황 → 밝아짐
                    new GradientColorKey(new Color(0.62f, 0.80f, 0.97f), 1f),    // 맑은 아침 하늘
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
