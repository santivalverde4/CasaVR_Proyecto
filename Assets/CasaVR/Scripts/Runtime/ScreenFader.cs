using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CasaVR
{
    /// <summary>Fundido a negro delante de la cámara (sirve en VR y en escritorio).</summary>
    public class ScreenFader : MonoBehaviour
    {
        public Camera targetCamera;
        public float defaultDuration = 0.2f;
        CanvasGroup m_Group;

        void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponentInParent<Camera>();
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = targetCamera;
            canvas.planeDistance = Mathf.Max(0.06f, (targetCamera != null ? targetCamera.nearClipPlane : 0.03f) + 0.02f);
            canvas.sortingOrder = 32000;
            m_Group = gameObject.AddComponent<CanvasGroup>();
            m_Group.alpha = 0f;
            m_Group.blocksRaycasts = false;
            m_Group.interactable = false;
            var img = new GameObject("Negro", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-200, -200); rt.offsetMax = new Vector2(200, 200);
            var image = img.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }

        public IEnumerator Fade(float to, float duration = -1f)
        {
            if (duration < 0f) duration = defaultDuration;
            float from = m_Group.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                m_Group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            m_Group.alpha = to;
        }
    }
}
