using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 모기 날개 날갯짓. 애니메이션 파일 없이 코드로 날개를 흔든다.
    /// flapping = false 로 두면 날개를 등 뒤로 접는다 (벽에 앉았을 때).
    /// 날개 Transform의 피벗은 날개 뿌리, 오른쪽 날개는 +X, 왼쪽 날개는 -X 방향으로 뻗어 있어야 한다.
    /// </summary>
    public class MosquitoWingFlap : MonoBehaviour
    {
        public Transform wingL;
        public Transform wingR;

        public bool flapping = true;

        [Header("날갯짓")]
        [Tooltip("초당 날갯짓 횟수. 화면 주사율(72~90Hz)보다 충분히 낮아야 깜빡임이 덜함")]
        [Range(1f, 40f)] public float frequency = 24f;
        [Tooltip("위아래로 흔드는 각도 (도)")]
        [Range(0f, 80f)] public float amplitude = 50f;
        [Tooltip("날개 기본 위로 들린 각도 (도)")]
        [Range(-30f, 45f)] public float dihedral = 15f;
        [Tooltip("날 때 날개가 뒤로 젖혀진 각도 (도)")]
        [Range(0f, 90f)] public float sweep = 30f;

        [Header("접기")]
        [Tooltip("앉았을 때 날개가 뒤로 접힌 각도 (도)")]
        [Range(0f, 90f)] public float foldedSweep = 72f;
        [Range(-30f, 30f)] public float foldedDihedral = -4f;
        [Tooltip("펼치기/접기 속도 (1/초)")]
        public float foldSpeed = 8f;

        float _phase;
        float _open = 1f;

        void Awake()
        {
            _phase = Random.value * Mathf.PI * 2f; // 여러 마리가 동시에 같은 박자로 움직이지 않게
            _open = flapping ? 1f : 0f;
        }

        void Update()
        {
            _open = Mathf.MoveTowards(_open, flapping ? 1f : 0f, foldSpeed * Time.deltaTime);
            _phase = Mathf.Repeat(_phase + frequency * Mathf.PI * 2f * Time.deltaTime, Mathf.PI * 2f);

            float flap = dihedral + amplitude * Mathf.Sin(_phase) * _open;
            float angle = Mathf.Lerp(foldedDihedral, flap, _open);
            float yaw = Mathf.Lerp(foldedSweep, sweep, _open);
            Apply(wingR, angle, yaw, 1f);
            Apply(wingL, angle, yaw, -1f);
        }

        /// <summary>날개 자세 계산 (에디터에서 기본 자세를 잡을 때도 사용)</summary>
        public static Quaternion WingRotation(float angle, float yaw, float side) =>
            Quaternion.AngleAxis(angle * side, Vector3.forward) * Quaternion.Euler(0f, yaw * side, 0f);

        static void Apply(Transform wing, float angle, float yaw, float side)
        {
            if (wing != null) wing.localRotation = WingRotation(angle, yaw, side);
        }
    }
}
