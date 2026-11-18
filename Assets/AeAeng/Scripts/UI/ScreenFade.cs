using System.Collections;
using UnityEngine;

namespace AeAeng
{
    /// <summary>
    /// 화면 페이드. XR 카메라 자식인 작은 구(안쪽에서 보임)에 AeAeng/ScreenFade 재질로 검은색을 덮는다.
    /// 알파 0이면 렌더러를 꺼서 비용 없음. VR 에서 카메라를 강제로 움직이지 않는 멀미 없는 전환.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ScreenFade : MonoBehaviour
    {
        public Color color = Color.black;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        Renderer _renderer;
        MaterialPropertyBlock _mpb;

        public float Alpha { get; private set; }

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            SetAlpha(Alpha);
        }

        public void SetAlpha(float a)
        {
            Alpha = Mathf.Clamp01(a);
            if (_renderer == null) return;
            _renderer.enabled = Alpha > 0.001f;
            _renderer.GetPropertyBlock(_mpb);
            var c = color;
            c.a = Alpha;
            _mpb.SetColor(ColorId, c);
            _renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>seconds 동안 알파를 target 까지 (부드럽게)</summary>
        public IEnumerator FadeTo(float target, float seconds)
        {
            float from = Alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, seconds);
                SetAlpha(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            SetAlpha(target);
        }
    }
}
