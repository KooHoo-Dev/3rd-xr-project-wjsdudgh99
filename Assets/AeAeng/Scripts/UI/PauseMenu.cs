using UnityEngine;
using UnityEngine.InputSystem;

namespace AeAeng
{
    /// <summary>
    /// 메인 복귀 확인 창 (기획서 4.2). 왼손 트리거를 1초 이상 누르거나(LightSwitchInput.onLongPress),
    /// 왼쪽 컨트롤러 메뉴 버튼(≡)을 누르면 열림 (메뉴 버튼을 다시 누르면 닫힘).
    ///  열리면: 게임 멈춤(timeScale 0), 모든 소리 멈춤, 레이 보이기(버튼 누르기용), 스위치로 불 전환 막기
    ///  아니오: 닫고 게임 재개 / 네: 결과 화면으로
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Tooltip("창 (평소에는 꺼져 있음)")]
        public GameObject panel;
        public RayVisibility rays;
        public LightSwitchInput lightSwitch;

        public bool IsOpen { get; private set; }
        /// <summary>어느 씬이든 확인 창이 열려 있는지 (포커스 복구 때 소리를 켜지 않도록)</summary>
        public static bool AnyOpen { get; private set; }

        InputAction _menuButton;

        void OnDestroy()
        {
            if (IsOpen) AnyOpen = false;
        }

        // 왼쪽 컨트롤러 메뉴 버튼(≡)으로도 열고 닫기 (11/18 추가: 길게 누르기는 처음 하는 사람이 알기 어려움)
        void OnEnable()
        {
            _menuButton = new InputAction("PauseMenu", InputActionType.Button);
            _menuButton.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            _menuButton.AddBinding("<Keyboard>/escape"); // 에디터 테스트
            _menuButton.Enable();
        }

        void OnDisable()
        {
            _menuButton?.Disable();
            _menuButton?.Dispose();
        }

        void Update()
        {
            if (_menuButton == null || !_menuButton.WasPressedThisFrame()) return;
            if (IsOpen) OnNo();
            else Open();
        }

        void Awake()
        {
            if (rays == null) rays = FindFirstObjectByType<RayVisibility>();
            if (lightSwitch == null) lightSwitch = FindFirstObjectByType<LightSwitchInput>();
            if (panel != null) panel.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen || panel == null) return;
            IsOpen = true;
            AnyOpen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (lightSwitch != null) lightSwitch.enabled = false;
            if (rays != null) rays.SetVisible(true);
            panel.SetActive(true);
            var lockComp = panel.GetComponent<HeadLock>();
            if (lockComp != null) lockComp.SnapToFront();
            Debug.Log("[PauseMenu] 메인 복귀 확인 창 열림");
        }

        public void OnNo()
        {
            if (!IsOpen) return;
            IsOpen = false;
            AnyOpen = false;
            panel.SetActive(false);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (rays != null) rays.SetVisible(false);
            if (lightSwitch != null) lightSwitch.enabled = true;
        }

        public void OnYes()
        {
            if (!IsOpen) return;
            IsOpen = false;
            AnyOpen = false;
            panel.SetActive(false);
            // 튜토리얼 중이면 기록이 없으니 결과 대신 메인으로 (timeScale / 소리는 GameFlow 가 되돌림)
            if (GameManager.Get().TutorialMode) GameFlow.LoadMenu();
            else GameFlow.LoadResult();
        }
    }
}
