using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 책상 위에 메인 화면에서 고른 무기(파리채 / 신문지)만 보이게 한다.
    /// 두 무기는 같은 자리에 놓여 있고, 고르지 않은 쪽은 꺼 둔다.
    /// 메인 화면에서는 버튼을 누를 때마다 바로 바뀌어서 책상 위 무기를 미리 볼 수 있다.
    /// </summary>
    public class WeaponSelector : MonoBehaviour
    {
        public GameObject flySwatter;
        public GameObject newspaper;

        void Awake() => Apply();

        void OnEnable()
        {
            GameManager.Get().Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            if (GameManager.Instance != null) GameManager.Instance.Changed -= Apply;
        }

        void Apply()
        {
            var w = GameManager.Get().SelectedWeapon;
            if (flySwatter != null && flySwatter.activeSelf != (w == Weapon.FlySwatter))
                flySwatter.SetActive(w == Weapon.FlySwatter);
            if (newspaper != null && newspaper.activeSelf != (w == Weapon.Newspaper))
                newspaper.SetActive(w == Weapon.Newspaper);
        }
    }
}
