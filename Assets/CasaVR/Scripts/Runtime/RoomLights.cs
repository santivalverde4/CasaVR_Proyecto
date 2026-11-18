using System.Collections.Generic;
using UnityEngine;

namespace CasaVR
{
    /// <summary>Grupo de luces de una habitación (luces + lámparas con material emisivo).</summary>
    public class RoomLights : MonoBehaviour
    {
        public string groupId;
        public string displayName;
        public List<Light> lights = new List<Light>();
        public List<Renderer> fixtures = new List<Renderer>();
        public Material onMaterial;
        public Material offMaterial;
        public bool isOn = true;
        [Tooltip("1 = piso 1, 2 = piso 2, 0 = exterior/áreas comunes (siempre activas)")]
        public int floor;

        bool m_Culled;
        static int s_ActiveFloor = -1;
        public bool IsCulled => m_Culled;

        static readonly Dictionary<string, RoomLights> s_All = new Dictionary<string, RoomLights>();
        public static IReadOnlyDictionary<string, RoomLights> All => s_All;
        public event System.Action<RoomLights> Changed;

        void Awake()
        {
            if (!string.IsNullOrEmpty(groupId)) s_All[groupId] = this;
            m_Culled = s_ActiveFloor > 0 && floor != 0 && floor != s_ActiveFloor;
            Apply();
        }

        void OnDestroy()
        {
            if (s_All.Count <= 1) s_ActiveFloor = -1;
            if (!string.IsNullOrEmpty(groupId) && s_All.TryGetValue(groupId, out var g) && g == this)
                s_All.Remove(groupId);
        }

        public void Toggle() => SetOn(!isOn);

        /// <summary>
        /// Optimización para Quest: las luces del piso donde NO está el jugador se desactivan
        /// (su estado encendido/apagado se conserva). Las áreas comunes (floor = 0) siempre quedan activas.
        /// </summary>
        public static void SetActiveFloor(int activeFloor)
        {
            if (activeFloor == s_ActiveFloor) return;
            s_ActiveFloor = activeFloor;
            foreach (var g in s_All.Values)
            {
                if (g == null) continue;
                g.m_Culled = g.floor != 0 && g.floor != activeFloor;
                g.Apply();
            }
        }

        public void SetOn(bool on)
        {
            isOn = on;
            Apply();
            Changed?.Invoke(this);
        }

        void Apply()
        {
            foreach (var l in lights)
                if (l != null) l.enabled = isOn && !m_Culled;
            if (onMaterial == null || offMaterial == null) return;
            Material from = isOn ? offMaterial : onMaterial;
            Material to = isOn ? onMaterial : offMaterial;
            foreach (var r in fixtures)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == from) { mats[i] = to; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
