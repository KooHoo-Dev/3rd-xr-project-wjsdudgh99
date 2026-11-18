using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace AeAeng
{
    /// <summary>
    /// 지정한 손의 Near-Far Interactor 가 그립 대신 **트리거**로 잡도록 Select / Activate 입력을 서로 바꾼다.
    /// 기획: 오른손 트리거를 누르고 있는 동안 파리채를 잡고, 떼면 놓는다.
    /// XR Origin 루트에 붙인다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TriggerSelectSwap : MonoBehaviour
    {
        public InteractorHandedness hand = InteractorHandedness.Right;

        void Awake()
        {
            int count = 0;
            foreach (var it in GetComponentsInChildren<NearFarInteractor>(true))
            {
                if (it.handedness != hand) continue;
                var select = it.selectInput;
                it.selectInput = it.activateInput;   // 트리거 → 잡기
                it.activateInput = select;           // 그립 → Activate (지금은 안 씀)
                it.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State; // 누르고 있는 동안만
                count++;
            }
            if (count == 0)
                Debug.LogWarning($"[TriggerSelectSwap] {hand} 손 Near-Far Interactor 를 찾지 못했습니다.");
        }
    }
}
