using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 모기가 앉을 수 있는 자리. forward = 표면 바깥쪽(노멀). 한 자리에는 한 마리만 앉음.
    /// AeAeng > Setup Mosquitoes 가 플레이어 머리에서 손이 닿는 범위의 벽/가구 표면에 자동으로 만든다.
    /// </summary>
    public class LandingPoint : MonoBehaviour
    {
        [HideInInspector] public Mosquito occupant;

        public bool IsFree => occupant == null;
        public Vector3 Normal => transform.forward;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = IsFree ? new Color(0.2f, 1f, 0.4f, 0.8f) : new Color(1f, 0.3f, 0.2f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.012f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.05f);
        }
#endif
    }
}
