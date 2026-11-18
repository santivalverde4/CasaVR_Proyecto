using System.Collections.Generic;
using UnityEngine;

namespace CasaVR
{
    /// <summary>
    /// Enciende el XR Interaction Simulator solo cuando NO hay un visor real funcionando.
    /// Con Meta Quest (Link o APK) el simulador quitaría el visor real del Input System y la cámara
    /// quedaría a ras del piso o sin seguir la cabeza. El objeto está marcado EditorOnly: no llega al APK.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SimulatorGate : MonoBehaviour
    {
        public GameObject simulator;
        [Tooltip("CasaVR > Opciones > Activar o desactivar el simulador XR")]
        public bool enableSimulator = true;

        static readonly List<UnityEngine.XR.XRDisplaySubsystem> s_Displays = new List<UnityEngine.XR.XRDisplaySubsystem>();

        public static bool RealHeadsetRunning()
        {
            SubsystemManager.GetSubsystems(s_Displays);
            foreach (var d in s_Displays)
                if (d != null && d.running) return true;
            return false;
        }

        void Awake()
        {
            if (simulator == null) return;
            bool on = enableSimulator && !RealHeadsetRunning();
            simulator.SetActive(on);
            if (enableSimulator && !on)
                Debug.Log("[CasaVR] Visor real detectado: el XR Interaction Simulator queda apagado.");
        }
    }
}
