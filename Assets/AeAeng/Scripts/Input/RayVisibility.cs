using System.Linq;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// XR Origin의 컨트롤러 레이와 표시점을 보이거나 숨긴다.
    /// - LineVisual: Near-Far Interactor의 레이 선
    /// - Poke Point Affordances: Poke Interactor가 컨트롤러 앞에 띄우는 회색 점
    /// 기획: 레이는 메뉴 / 결과 화면 UI에서만 쓰고, 게임 중에는 숨김.
    /// XR Origin 루트에 붙인다. 메뉴 화면에서는 SetVisible(true).
    /// </summary>
    public class RayVisibility : MonoBehaviour
    {
        public bool showRays;

        [Tooltip("이 이름의 오브젝트를 모두 켜고 끈다")]
        public string[] visualNames = { "LineVisual", "Poke Point Affordances" };

        GameObject[] _lines;

        void Awake()
        {
            _lines = GetComponentsInChildren<Transform>(true)
                .Where(t => visualNames.Contains(t.name))
                .Select(t => t.gameObject)
                .ToArray();
            Apply();
        }

        public void SetVisible(bool visible)
        {
            showRays = visible;
            Apply();
        }

        void OnValidate()
        {
            if (Application.isPlaying && _lines != null) Apply();
        }

        void Apply()
        {
            foreach (var line in _lines)
                if (line != null) line.SetActive(showRays);
        }
    }
}
