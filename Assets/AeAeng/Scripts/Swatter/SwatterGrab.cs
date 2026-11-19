using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AeAeng
{
    /// <summary>
    /// 파리채 잡기 / 놓기.
    /// - 잡기 판정은 XRGrabInteractable (오른손 트리거로 Select 되도록 TriggerSelectSwap 과 함께 씀)
    /// - 잡는 손에 "SwatterGrip"(주먹 가운데)이 있으면, 컨트롤러 대신 그 점에 파리채의 GripPoint 를 맞춰 손 주먹 안에 쥐어지게 함
    ///   (손 모델은 실제 손 위치에 맞춰 컨트롤러보다 뒤/아래에 있으므로)
    /// - 트리거를 놓으면 던지지 않고 returnSeconds 동안 원래 자리(책상 위)로 부드럽게 돌아감
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SwatterGrab : MonoBehaviour
    {
        [Tooltip("놓았을 때 제자리로 돌아가는 시간 (초)")]
        public float returnSeconds = 0.2f;

        [Tooltip("손 안에서 찾을 기준점 이름")]
        public string handGripName = "SwatterGrip";

        [Header("책상에 내려놓는 소리 (11/19)")]
        [Tooltip("놓은 무기가 책상 위 제자리에 닿을 때 \"탁\" (무기 자리에서 나는 3D 소리)")]
        public AudioClip dropClip;
        [Range(0f, 1f)] public float dropVolume = 0.7f;
        AudioSource _dropAudio;

        public bool IsHeld => _grab != null && _grab.isSelected;

        XRGrabInteractable _grab;
        Transform _gripPoint;     // 파리채 쪽 기준점 (손잡이)
        Transform _sceneParent;   // 원래 부모 (놓으면 되돌림)
        Vector3 _restPos;
        Quaternion _restRot;
        Coroutine _return;

        void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _gripPoint = _grab.attachTransform != null ? _grab.attachTransform : transform;
            _sceneParent = transform.parent;
            SetRestPoseHere();
        }

        void OnEnable()
        {
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }

        /// <summary>지금 위치를 "제자리"로 기억 (자리를 옮기고 싶을 때 호출)</summary>
        public void SetRestPoseHere()
        {
            _restPos = transform.position;
            _restRot = transform.rotation;
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            if (_return != null) { StopCoroutine(_return); _return = null; }

            var handGrip = FindHandGrip(args.interactorObject.transform);
            bool followHand = handGrip != null;
            // 손 기준점이 있으면 XRGrab 대신 손에 붙여서 따라가게 (헤드셋 렌더 직전 갱신까지 손과 같이 움직임)
            _grab.trackPosition = !followHand;
            _grab.trackRotation = !followHand;
            if (!followHand) return;

            transform.SetParent(handGrip, false);
            // 파리채 GripPoint 가 손 기준점과 겹치도록: 파리채 로컬 = GripPoint 의 역변환
            // (씬에서 파리채 크기를 키워도 맞도록 파리채 스케일을 반영)
            var inv = Quaternion.Inverse(_gripPoint.localRotation);
            transform.localRotation = inv;
            transform.localPosition = -(inv * Vector3.Scale(transform.localScale, _gripPoint.localPosition));
        }

        void OnReleased(SelectExitEventArgs args)
        {
            transform.SetParent(_sceneParent, true);
            _grab.trackPosition = true;
            _grab.trackRotation = true;

            if (!isActiveAndEnabled) return;
            if (_return != null) StopCoroutine(_return);
            _return = StartCoroutine(ReturnRoutine());
        }

        /// <summary>잡은 Interactor 와 같은 컨트롤러 아래에 있는 손의 SwatterGrip 을 찾음</summary>
        Transform FindHandGrip(Transform interactor)
        {
            var t = interactor;
            for (int depth = 0; depth < 3 && t != null; depth++, t = t.parent)
            {
                foreach (var c in t.GetComponentsInChildren<Transform>(true))
                    if (c.name == handGripName) return c;
            }
            return null;
        }

        IEnumerator ReturnRoutine()
        {
            // XRGrabInteractable 이 놓은 직후 같은 프레임에 위치를 정리하므로 한 프레임 뒤에 시작
            yield return null;
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, returnSeconds);
                float k = Mathf.SmoothStep(0f, 1f, t);
                transform.SetPositionAndRotation(Vector3.Lerp(fromPos, _restPos, k), Quaternion.Slerp(fromRot, _restRot, k));
                yield return null;
            }
            transform.SetPositionAndRotation(_restPos, _restRot);
            _return = null;
            PlayDrop();
        }

        void PlayDrop()
        {
            if (dropClip == null) return;
            if (_dropAudio == null)
            {
                _dropAudio = gameObject.AddComponent<AudioSource>();
                _dropAudio.playOnAwake = false;
                _dropAudio.loop = false;
                _dropAudio.spatialBlend = 1f;
                _dropAudio.rolloffMode = AudioRolloffMode.Logarithmic;
                _dropAudio.minDistance = 0.5f;
                _dropAudio.maxDistance = 6f;
                _dropAudio.dopplerLevel = 0f;
            }
            _dropAudio.PlayOneShot(dropClip, dropVolume);
        }
    }
}
