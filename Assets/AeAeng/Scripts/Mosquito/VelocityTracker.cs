using System.Collections.Generic;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 오브젝트의 이동 속도(m/s)를 매 프레임 계산한다.
    /// 오른손과 파리채 머리에 붙여서, 모기의 눈치 판정(가까이서 빠르게 움직이면 도망)과 파리채 타격 속도에 씀.
    /// </summary>
    public class VelocityTracker : MonoBehaviour
    {
        /// <summary>씬에서 켜져 있는 모든 트래커 (모기가 눈치 판정할 때 훑어봄)</summary>
        public static readonly List<VelocityTracker> All = new List<VelocityTracker>();

        [Tooltip("모기 눈치 판정에 쓸지 (오른손, 파리채)")]
        public bool alertsMosquitoes = true;
        [Tooltip("속도 부드럽게 하기 (0 = 그대로, 1에 가까울수록 느리게 반응)")]
        [Range(0f, 0.9f)] public float smoothing = 0.3f;

        public Vector3 Velocity { get; private set; }
        public float Speed => Velocity.magnitude;
        public Vector3 Position => transform.position;

        Vector3 _last;
        bool _has;

        void OnEnable()
        {
            All.Add(this);
            _has = false;
        }

        void OnDisable() => All.Remove(this);

        void LateUpdate()
        {
            var p = transform.position;
            float dt = Time.deltaTime;
            if (_has && dt > 1e-5f)
            {
                var v = (p - _last) / dt;
                Velocity = Vector3.Lerp(v, Velocity, smoothing);
            }
            _last = p;
            _has = true;
        }
    }
}
