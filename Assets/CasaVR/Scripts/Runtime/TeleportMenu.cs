using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CasaVR
{
    [System.Serializable]
    public class RoomEntry
    {
        public string id;
        public string displayName;
        [Tooltip("0 = Exterior, 1 = Piso 1, 2 = Piso 2")]
        public int floor;
        public Transform spawn;
        public Transform look;
    }

    [System.Serializable]
    public class MenuIcon
    {
        public string key;
        public Sprite sprite;
    }

    /// <summary>
    /// Menú flotante de teletransporte, disponible en cualquier momento:
    /// VR = botón Menú (control izquierdo) o Y / B; simulador XR = M; escritorio = Tab o M.
    /// Muestra las tres categorías a la vez (Piso 1 · Piso 2 · Exterior), cada destino es un botón grande
    /// con ícono; se elige apuntando con el rayo y el gatillo. La columna del piso donde estás se resalta.
    /// Abajo: Modo día/noche y Cerrar.
    /// </summary>
    public class TeleportMenu : MonoBehaviour
    {
        public List<RoomEntry> rooms = new List<RoomEntry>();
        public InputActionReference[] xrMenuActions;
        public InputActionReference desktopMenuAction;
        public CasaVRPlayer player;
        public ScreenFader fader;
        public DayNightController dayNight;
        [Tooltip("Distancia preferida del menú a los ojos (m)")]
        public float distance = 1.25f;
        [Tooltip("Ancho angular del menú (grados): se mantiene igual aunque el menú se acerque por una pared")]
        public float angularWidth = 54f;
        [Tooltip("Altura (m) a partir de la cual se considera piso 2 (igual que CasaVRPlayer)")]
        public float floor2Height = 2.5f;

        [Header("Apariencia (sprites generados en Textures/UI)")]
        public Sprite rounded;
        public Sprite roundedOutline;
        public Sprite glow;
        public List<MenuIcon> icons = new List<MenuIcon>();

        // ---- diseño (unidades del canvas)
        const float k_W = 1640f, k_H = 1010f;
        const float k_ColW = 506f, k_ColGap = 31f, k_ColTop = -180f, k_ColH = 690f;
        const float k_HeaderH = 96f, k_BtnH = 68f, k_BtnGap = 10f, k_Pad = 14f;

        static readonly int[] k_ColumnFloors = { 1, 2, 0 };
        static readonly string[] k_FloorTitle = { "Exterior", "Piso 1", "Piso 2" };

        static readonly Color k_PanelBg = new Color(0.045f, 0.065f, 0.11f, 0.95f);
        static readonly Color k_PanelLine = new Color(0.35f, 0.55f, 0.95f, 0.45f);
        static readonly Color k_Column = new Color(0.085f, 0.12f, 0.19f, 0.96f);
        static readonly Color k_ColumnLine = new Color(0.30f, 0.45f, 0.75f, 0.22f);
        static readonly Color k_Header = new Color(0.12f, 0.18f, 0.29f, 1f);
        static readonly Color k_Accent = new Color(0.16f, 0.47f, 0.96f, 1f);
        static readonly Color k_AccentGlow = new Color(0.25f, 0.55f, 1f, 0.55f);
        static readonly Color k_BtnNormal = new Color(0.13f, 0.19f, 0.31f, 1f);
        static readonly Color k_BtnHover = new Color(0.19f, 0.33f, 0.58f, 1f);
        static readonly Color k_BtnPressed = new Color(0.20f, 0.50f, 0.98f, 1f);
        static readonly Color k_Text = new Color(0.95f, 0.97f, 1f, 1f);
        static readonly Color k_TextMuted = new Color(0.66f, 0.72f, 0.82f, 1f);

        Canvas m_Canvas;
        Font m_Font;
        Material m_OnTop;
        readonly List<Image> m_HeaderImages = new List<Image>();
        readonly List<Image> m_HeaderGlows = new List<Image>();
        readonly List<Image> m_ColumnLines = new List<Image>();
        Text m_Subtitle;
        Text m_DayNightLabel;
        Image m_DayNightIcon;
        int m_Floor = 1;
        bool m_Busy;

        public bool IsOpen => m_Canvas != null && m_Canvas.gameObject.activeSelf;
        /// <summary>Categoría resaltada (piso donde está el jugador): 1, 2 o 0 = Exterior.</summary>
        public int CurrentFloorTab => m_Floor;
        public Canvas MenuCanvas => m_Canvas;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<CasaVRPlayer>();
            BuildUI();
            SetVisible(false);
        }

        void OnEnable()
        {
            if (xrMenuActions != null)
                foreach (var a in xrMenuActions)
                    if (a != null && a.action != null) a.action.Enable();
            if (desktopMenuAction != null && desktopMenuAction.action != null) desktopMenuAction.action.Enable();
        }

        void Update()
        {
            if (m_Busy) return;
            bool pressed = false;
            if (player != null && player.DesktopMode)
            {
                if (desktopMenuAction != null && desktopMenuAction.action != null && desktopMenuAction.action.WasPressedThisFrame())
                    pressed = true;
            }
            else
            {
                if (xrMenuActions != null)
                    foreach (var a in xrMenuActions)
                        if (a != null && a.action != null && a.action.WasPressedThisFrame()) pressed = true;
                // con el simulador XR en la computadora: tecla M (Tab la usa el simulador)
                var kb = Keyboard.current;
                if (kb != null && kb.mKey.wasPressedThisFrame) pressed = true;
            }
            if (pressed) Toggle();
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            Transform head = player != null && player.head != null ? player.head : (Camera.main != null ? Camera.main.transform : transform);
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(head.up, Vector3.up);
            fwd.Normalize();

            // Si hay una pared delante, se busca la dirección libre más cercana a la mirada; si no hay
            // ninguna, el menú se acerca. El tamaño angular se mantiene, así que siempre se ve igual.
            float d = PickPlacement(head.position, ref fwd);
            float width = 2f * d * Mathf.Tan(angularWidth * 0.5f * Mathf.Deg2Rad);
            m_Canvas.transform.localScale = Vector3.one * (width / k_W);
            Vector3 pos = head.position + fwd * d + Vector3.down * (0.08f * d);
            m_Canvas.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - head.position, Vector3.up));

            if (player != null)
                m_Floor = player.transform.position.y > floor2Height ? 2 : (IsInsideHouse() ? 1 : 0);
            ShowFloor(m_Floor);
            RefreshLabels();
            SetVisible(true);
            if (player != null && player.DesktopMode)
            {
                player.InputLocked = true;
                player.SetCursorLocked(false);
            }
        }

        float PickPlacement(Vector3 eye, ref Vector3 fwd)
        {
            float[] yaws = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };
            float bestFree = -1f; Vector3 bestDir = fwd;
            foreach (float yaw in yaws)
            {
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * fwd;
                float free = Physics.Raycast(eye, dir, out RaycastHit hit, distance + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    ? hit.distance : distance + 0.5f;
                if (free >= 0.95f)
                {
                    fwd = dir;
                    return Mathf.Min(distance, free - 0.15f);
                }
                if (free > bestFree) { bestFree = free; bestDir = dir; }
            }
            fwd = bestDir;
            return Mathf.Clamp(bestFree - 0.12f, 0.4f, distance);
        }

        public void Close()
        {
            SetVisible(false);
            if (player != null)
            {
                player.InputLocked = false;
                if (player.DesktopMode) player.SetCursorLocked(true);
            }
        }

        bool IsInsideHouse()
        {
            // ¿Hay techo encima? (dentro de la casa o del porche)
            return player != null && Physics.Raycast(player.transform.position + Vector3.up * 1.0f, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore);
        }

        public RoomEntry Find(string id) => rooms.Find(r => r.id == id);

        public void GoTo(string id)
        {
            var e = Find(id);
            if (e != null) GoTo(e);
        }

        public void GoTo(RoomEntry entry)
        {
            if (!m_Busy && entry != null && entry.spawn != null) StartCoroutine(GoRoutine(entry));
        }

        IEnumerator GoRoutine(RoomEntry e)
        {
            m_Busy = true;
            if (fader != null) yield return fader.Fade(1f, 0.18f);
            if (player != null) player.TeleportTo(e.spawn, e.look);
            Close();
            yield return null;
            if (fader != null) yield return fader.Fade(0f, 0.25f);
            m_Busy = false;
        }

        void SetVisible(bool v)
        {
            if (m_Canvas != null) m_Canvas.gameObject.SetActive(v);
        }

        /// <summary>Resalta la categoría del piso indicado (1, 2 o 0 = Exterior).</summary>
        public void ShowFloor(int floor)
        {
            m_Floor = floor;
            for (int i = 0; i < m_HeaderImages.Count; i++)
            {
                bool here = k_ColumnFloors[i] == floor;
                m_HeaderImages[i].color = here ? k_Accent : k_Header;
                m_HeaderGlows[i].enabled = here;
                m_ColumnLines[i].color = here ? new Color(k_Accent.r, k_Accent.g, k_Accent.b, 0.85f) : k_ColumnLine;
            }
            if (m_Subtitle != null)
                m_Subtitle.text = "Estás en: " + k_FloorTitle[Mathf.Clamp(floor, 0, 2)] + "     ·     Apunta con el rayo y presiona el gatillo";
        }

        void ToggleDayNight()
        {
            if (dayNight != null) dayNight.Toggle();
            RefreshLabels();
        }

        void RefreshLabels()
        {
            bool night = dayNight != null && dayNight.night;
            if (m_DayNightLabel != null) m_DayNightLabel.text = night ? "Modo día" : "Modo noche";
            if (m_DayNightIcon != null)
            {
                m_DayNightIcon.sprite = Icon(night ? "Dia" : "Noche");
                m_DayNightIcon.enabled = m_DayNightIcon.sprite != null;
            }
        }

        // ----------------------------------------------------------------------------- construcción
        Sprite Icon(string key)
        {
            foreach (var i in icons)
                if (i != null && i.key == key) return i.sprite;
            return null;
        }

        static string RoomKey(RoomEntry r)
        {
            int i = r.id.IndexOf('_');
            return i >= 0 ? r.id.Substring(i + 1) : r.id;
        }

        void BuildUI()
        {
            m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Graphic.defaultGraphicMaterial != null)
            {
                // se dibuja encima de paredes y muebles para que nunca quede tapado
                m_OnTop = new Material(Graphic.defaultGraphicMaterial) { name = "UI_SiempreVisible" };
                m_OnTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            var go = new GameObject("MenuTeletransporte", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            m_Canvas = go.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.WorldSpace;
            Camera cam = player != null && player.head != null ? player.head.GetComponent<Camera>() : Camera.main;
            m_Canvas.worldCamera = cam;
            m_Canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;      // texto nítido en VR
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            var root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(k_W, k_H);
            root.localScale = Vector3.one * 0.00078f;

            // fondo
            var bg = AddImage(root, "Fondo", k_PanelBg, rounded, 1f);
            Stretch(bg.rectTransform);
            var bgLine = AddImage(root, "Borde", k_PanelLine, roundedOutline, 1f);
            Stretch(bgLine.rectTransform);

            var title = AddText(root, "Titulo", "¿A dónde quieres ir?", 62, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0f, k_H * 0.5f - 72f), new Vector2(k_W - 80f, 80f));
            m_Subtitle = AddText(root, "Subtitulo", "", 30, FontStyle.Normal, TextAnchor.MiddleCenter);
            m_Subtitle.color = k_TextMuted;
            Place(m_Subtitle.rectTransform, new Vector2(0f, k_H * 0.5f - 134f), new Vector2(k_W - 80f, 44f));

            // tres columnas: Piso 1 · Piso 2 · Exterior
            for (int c = 0; c < 3; c++)
            {
                int floor = k_ColumnFloors[c];
                float cx = (c - 1) * (k_ColW + k_ColGap);
                var col = AddImage(root, "Columna_" + k_FloorTitle[floor], k_Column, rounded, 1.5f);
                Place(col.rectTransform, new Vector2(cx, k_H * 0.5f + k_ColTop - k_ColH * 0.5f), new Vector2(k_ColW, k_ColH));
                var colLine = AddImage(col.rectTransform, "Borde", k_ColumnLine, roundedOutline, 1.5f);
                Stretch(colLine.rectTransform);
                m_ColumnLines.Add(colLine);

                float top = k_ColH * 0.5f;
                var glowImg = AddImage(col.rectTransform, "Brillo", k_AccentGlow, glow, 1.2f);
                Place(glowImg.rectTransform, new Vector2(0f, top - k_Pad - k_HeaderH * 0.5f), new Vector2(k_ColW + 30f, k_HeaderH + 44f));
                m_HeaderGlows.Add(glowImg);
                var header = AddImage(col.rectTransform, "Encabezado", k_Header, rounded, 2f);
                Place(header.rectTransform, new Vector2(0f, top - k_Pad - k_HeaderH * 0.5f), new Vector2(k_ColW - 2f * k_Pad, k_HeaderH));
                m_HeaderImages.Add(header);
                AddIconAndLabel(header.rectTransform, Icon(floor == 0 ? "Exterior" : "Piso"), k_FloorTitle[floor], 46, FontStyle.Bold, 54f, true);

                float y = top - k_Pad - k_HeaderH - 18f - k_BtnH * 0.5f;
                foreach (var r in rooms)
                {
                    if (r.floor != floor) continue;
                    var entry = r;
                    var b = AddButton(col.rectTransform, "Destino_" + r.id, () => GoTo(entry), 2.2f);
                    Place((RectTransform)b.transform, new Vector2(0f, y), new Vector2(k_ColW - 2f * k_Pad, k_BtnH));
                    AddIconAndLabel((RectTransform)b.transform, Icon(RoomKey(r)), r.displayName, 34, FontStyle.Normal, 44f, false);
                    var chev = AddText(b.transform, "Flecha", "›", 46, FontStyle.Normal, TextAnchor.MiddleCenter);
                    chev.color = k_TextMuted;
                    Place(chev.rectTransform, new Vector2((k_ColW - 2f * k_Pad) * 0.5f - 30f, 3f), new Vector2(40f, k_BtnH));
                    y -= k_BtnH + k_BtnGap;
                }
            }

            // pie: ayuda, Modo día/noche y Cerrar
            float fy = -k_H * 0.5f + 70f;
            var hint = AddText(root, "Ayuda", "Gatillo: elegir   •   Menú / Y / B: abrir o cerrar   •   PC: clic, Tab o M",
                               25, FontStyle.Normal, TextAnchor.MiddleLeft);
            hint.color = k_TextMuted;
            Place(hint.rectTransform, new Vector2(-k_W * 0.5f + 40f + 470f, fy), new Vector2(940f, 50f));

            var dn = AddButton(root, "Boton_DiaNoche", ToggleDayNight, 2f);
            Place((RectTransform)dn.transform, new Vector2(k_W * 0.5f - 40f - 240f - 24f - 160f, fy), new Vector2(320f, 78f));
            AddIconAndLabel((RectTransform)dn.transform, Icon("Noche"), "Modo noche", 32, FontStyle.Normal, 40f, true);
            m_DayNightLabel = dn.transform.Find("Texto").GetComponent<Text>();
            var dnIcon = dn.transform.Find("Icono");
            m_DayNightIcon = dnIcon != null ? dnIcon.GetComponent<Image>() : null;

            var close = AddButton(root, "Boton_Cerrar", Close, 2f);
            Place((RectTransform)close.transform, new Vector2(k_W * 0.5f - 40f - 120f, fy), new Vector2(240f, 78f));
            var closeLine = AddImage(close.transform, "Borde", new Color(k_Accent.r, k_Accent.g, k_Accent.b, 0.9f), roundedOutline, 2f);
            Stretch(closeLine.rectTransform);
            AddIconAndLabel((RectTransform)close.transform, Icon("Cerrar"), "Cerrar", 34, FontStyle.Bold, 30f, true);
        }

        /// <summary>Ícono + texto; centrados como grupo (encabezados) o alineados a la izquierda (destinos).</summary>
        void AddIconAndLabel(RectTransform parent, Sprite icon, string label, int size, FontStyle style, float iconSize, bool centered)
        {
            float w = parent.sizeDelta.x;
            var t = AddText(parent, "Texto", label, size, style, centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            const float gap = 18f;
            if (centered)
            {
                float tw = t.preferredWidth + 4f;
                float group = (icon != null ? iconSize + gap : 0f) + tw;
                float x0 = -group * 0.5f;
                if (icon != null)
                {
                    var ic = AddImage(parent, "Icono", Color.white, icon, 1f);
                    ic.preserveAspect = true;
                    Place(ic.rectTransform, new Vector2(x0 + iconSize * 0.5f, 0f), new Vector2(iconSize, iconSize));
                    x0 += iconSize + gap;
                }
                Place(t.rectTransform, new Vector2(x0 + tw * 0.5f, 0f), new Vector2(tw, parent.sizeDelta.y));
            }
            else
            {
                float left = -w * 0.5f + 26f;
                if (icon != null)
                {
                    var ic = AddImage(parent, "Icono", Color.white, icon, 1f);
                    ic.preserveAspect = true;
                    Place(ic.rectTransform, new Vector2(left + iconSize * 0.5f, 0f), new Vector2(iconSize, iconSize));
                }
                float tx = left + iconSize + 22f;
                float tw = w * 0.5f - 60f - tx;
                Place(t.rectTransform, new Vector2(tx + tw * 0.5f, 0f), new Vector2(tw, parent.sizeDelta.y));
            }
        }

        Image AddImage(Transform parent, string name, Color c, Sprite sprite, float cornerScale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = cornerScale;   // radio de las esquinas
            }
            img.color = c;
            img.raycastTarget = false;
            if (m_OnTop != null) img.material = m_OnTop;
            return img;
        }

        Text AddText(Transform parent, string name, string text, int size, FontStyle style, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = m_Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = k_Text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (m_OnTop != null) t.material = m_OnTop;
            return t;
        }

        Button AddButton(Transform parent, string name, UnityAction onClick, float cornerScale)
        {
            var img = AddImage(parent, name, Color.white, rounded, cornerScale);
            img.raycastTarget = true;      // el rayo del control y el mouse lo detectan
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = k_BtnNormal;
            cb.highlightedColor = k_BtnHover;
            cb.pressedColor = k_BtnPressed;
            cb.selectedColor = k_BtnNormal;
            cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            return b;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
