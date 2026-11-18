using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CasaVR
{
    /// <summary>
    /// Modo escritorio (sin visor): mira al objeto y pulsa E o clic para interactuar.
    /// Muestra una cruz, el texto de la acción y la ayuda de teclas.
    /// </summary>
    public class DesktopInteractor : MonoBehaviour
    {
        public CasaVRPlayer player;
        public Camera cam;
        public InputActionReference interactAction;
        public TeleportMenu menu;
        public float maxDistance = 3.5f;

        Canvas m_Hud;
        Text m_Prompt;
        ICasaInteractable m_Current;

        void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();
            BuildHud();
        }

        void OnEnable()
        {
            if (interactAction != null && interactAction.action != null) interactAction.action.Enable();
        }

        void Update()
        {
            bool active = player != null && player.DesktopMode;
            if (m_Hud != null && m_Hud.gameObject.activeSelf != active) m_Hud.gameObject.SetActive(active);
            if (!active) return;
            bool menuOpen = menu != null && menu.IsOpen;
            var mouse = Mouse.current;
            if (!menuOpen && Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                player.SetCursorLocked(true);
                return;
            }
            m_Current = null;
            if (!menuOpen && cam != null &&
                Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                m_Current = hit.collider.GetComponentInParent<ICasaInteractable>();
            }
            if (m_Prompt != null) m_Prompt.text = m_Current != null ? "[E / clic]  " + m_Current.Prompt : "";
            if (m_Current != null && interactAction != null && interactAction.action != null && interactAction.action.WasPressedThisFrame())
                m_Current.Interact();
        }

        void BuildHud()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("HUD_Escritorio", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            m_Hud = go.AddComponent<Canvas>();
            m_Hud.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Hud.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            Text Make(string n, string txt, int size, TextAnchor a, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
            {
                var t = new GameObject(n, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                t.transform.SetParent(go.transform, false);
                t.font = font; t.text = txt; t.fontSize = size; t.alignment = a; t.color = Color.white;
                t.raycastTarget = false;
                var sh = t.gameObject.AddComponent<Shadow>(); sh.effectDistance = new Vector2(2, -2);
                var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
                return t;
            }
            Make("Mira", "+", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            m_Prompt = Make("Accion", "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(900, 60));
            Make("Ayuda", "WASD: caminar   Mouse: mirar   E / clic: interactuar   Shift: correr   Tab / M: menú de salas   Esc: soltar el cursor",
                 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(560, -30), new Vector2(1080, 40));
            m_Hud.gameObject.SetActive(false);
        }
    }
}
