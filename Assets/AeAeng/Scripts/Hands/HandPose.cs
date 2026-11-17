using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AeAeng
{
    /// <summary>
    /// 컨트롤러 입력으로 손가락을 접는다 (애니메이션 파일 없이 뼈 회전만).
    /// - 손가락마다 입력 소스(고정 / 트리거 / 그립)와 접힘 범위(min~max)를 정한다.
    /// - 접는 축은 Awake에서 손 모양(손목, 검지/새끼 뿌리 위치)으로 계산하므로 FBX 뼈 축 규칙에 의존하지 않는다.
    /// 기본값: 왼손 = 스위치를 쥔 상태(중지·약지·새끼 고정) + 트리거로 검지,
    ///         오른손 = 중지·약지·새끼가 살짝 말린 채 그립으로 더 쥐고, 트리거로 주먹.
    /// 에디터 테스트 키: 왼손 트리거 L, 오른손 트리거 K, 오른손 그립 J.
    /// </summary>
    public class HandPose : MonoBehaviour
    {
        public enum Side { Left, Right }
        public enum Source { Fixed, Trigger, Grip, TriggerOrGrip }

        [Serializable]
        public class Finger
        {
            [Tooltip("뿌리 → 끝 순서. 마지막 다음 위치는 tip")]
            public Transform[] bones;
            public Transform tip;
            public Source source = Source.Trigger;
            [Range(0f, 1f)] public float min;
            [Range(0f, 1f)] public float max = 1f;
            [Tooltip("완전히 접었을 때(1) 각 마디의 회전 각도")]
            public float[] maxAngles = { 70f, 90f, 60f };

            [NonSerialized] public Quaternion[] rest;
            [NonSerialized] public Vector3[] axes;
            [NonSerialized] public float current = -1f;
        }

        public Side side = Side.Right;
        public Transform wrist;
        public Finger thumb = new Finger();
        public Finger index = new Finger();
        public Finger middle = new Finger();
        public Finger ring = new Finger();
        public Finger little = new Finger();

        [Tooltip("손가락이 따라가는 속도 (클수록 빠름)")]
        public float followSpeed = 20f;
        [Tooltip("손가락이 거꾸로 접히면 -1")]
        public float curlSign = 1f;
        public bool keyboardForTesting = true;

        public float Trigger { get; private set; }
        public float Grip { get; private set; }

        InputAction _trigger, _grip;
        bool _initialized;

        Finger[] Fingers => new[] { thumb, index, middle, ring, little };

        void Awake() => Init();

        void OnEnable()
        {
            string hand = side == Side.Left ? "{LeftHand}" : "{RightHand}";
            _trigger = new InputAction($"{side}HandTrigger", InputActionType.Value, expectedControlType: "Axis");
            _trigger.AddBinding($"<XRController>{hand}/trigger");
            _grip = new InputAction($"{side}HandGrip", InputActionType.Value, expectedControlType: "Axis");
            _grip.AddBinding($"<XRController>{hand}/grip");
            if (keyboardForTesting)
            {
                _trigger.AddBinding(side == Side.Left ? "<Keyboard>/l" : "<Keyboard>/k");
                if (side == Side.Right) _grip.AddBinding("<Keyboard>/j");
            }
            _trigger.Enable();
            _grip.Enable();
        }

        void OnDisable()
        {
            _trigger?.Dispose();
            _grip?.Dispose();
            _trigger = _grip = null;
        }

        /// <summary>뼈의 기본 자세와 접는 축을 저장한다. 에디터 미리보기에서도 부를 수 있다.</summary>
        public void Init()
        {
            if (_initialized) return;
            if (wrist == null || index.bones == null || index.bones.Length == 0 ||
                little.bones == null || little.bones.Length == 0)
            {
                Debug.LogWarning($"[HandPose] {name}: 뼈가 연결되지 않았습니다.", this);
                return;
            }

            // 손등 방향: 오른손은 cross(검지 - 손목, 새끼 - 손목), 왼손은 반대
            var a = index.bones[0].position - wrist.position;
            var b = little.bones[0].position - wrist.position;
            var back = Vector3.Cross(a, b).normalized * (side == Side.Right ? 1f : -1f);

            foreach (var f in Fingers)
            {
                if (f.bones == null) continue;
                int n = f.bones.Length;
                f.rest = new Quaternion[n];
                f.axes = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    var bone = f.bones[i];
                    if (bone == null) continue;
                    f.rest[i] = bone.localRotation;
                    var next = i + 1 < n ? f.bones[i + 1] : f.tip;
                    var dir = next != null ? (next.position - bone.position).normalized : bone.forward;
                    // AngleAxis(+θ, cross(v, t))는 v를 t 쪽으로 돌린다 → 손가락을 손바닥(-back) 쪽으로
                    var axisWorld = Vector3.Cross(dir, -back).normalized * curlSign;
                    f.axes[i] = Quaternion.Inverse(bone.rotation) * axisWorld;
                }
                f.current = -1f;
            }
            _initialized = true;
        }

        void Update()
        {
            if (!_initialized) return;
            Trigger = _trigger != null ? _trigger.ReadValue<float>() : 0f;
            Grip = _grip != null ? _grip.ReadValue<float>() : 0f;

            float k = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            foreach (var f in Fingers)
            {
                float target = Target(f, Trigger, Grip);
                f.current = f.current < 0f ? target : Mathf.Lerp(f.current, target, k);
                ApplyFinger(f, f.current);
            }
        }

        /// <summary>입력값으로 즉시 자세를 만든다 (에디터 미리보기용).</summary>
        public void ApplyImmediate(float trigger, float grip)
        {
            Init();
            if (!_initialized) return;
            foreach (var f in Fingers)
            {
                f.current = Target(f, trigger, grip);
                ApplyFinger(f, f.current);
            }
        }

        /// <summary>뼈를 처음 자세로 되돌린다.</summary>
        public void ResetPose()
        {
            if (!_initialized) return;
            foreach (var f in Fingers)
            {
                if (f.bones == null || f.rest == null) continue;
                for (int i = 0; i < f.bones.Length; i++)
                    if (f.bones[i] != null) f.bones[i].localRotation = f.rest[i];
            }
        }

        static float Target(Finger f, float trigger, float grip)
        {
            float s = f.source switch
            {
                Source.Trigger => trigger,
                Source.Grip => grip,
                // 그립만 쥐면 80%까지 (컨트롤러를 쥔 느낌), 트리거를 당기면 끝까지 (주먹)
                Source.TriggerOrGrip => Mathf.Max(trigger, grip * 0.8f),
                _ => 0f,
            };
            return Mathf.Lerp(f.min, f.max, s);
        }

        static void ApplyFinger(Finger f, float curl)
        {
            if (f.bones == null || f.rest == null) return;
            for (int i = 0; i < f.bones.Length; i++)
            {
                var bone = f.bones[i];
                if (bone == null) continue;
                float maxAngle = f.maxAngles != null && i < f.maxAngles.Length ? f.maxAngles[i] : 60f;
                bone.localRotation = f.rest[i] * Quaternion.AngleAxis(curl * maxAngle, f.axes[i]);
            }
        }
    }
}
