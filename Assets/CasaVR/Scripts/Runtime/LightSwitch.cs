using UnityEngine;

namespace CasaVR
{
    /// <summary>Interruptor de pared: enciende/apaga las luces de su habitación (RoomLights).</summary>
    public class LightSwitch : CasaInteractable
    {
        public RoomLights target;
        public Transform rocker;
        [Tooltip("Eje de giro de la tecla en el espacio local de la tecla.")]
        public Vector3 rockerAxisLocal = Vector3.right;
        public float rockerAngle = 9f;
        public AudioClip clickClip;

        Quaternion m_RockerBase;

        public override string Prompt => target != null && target.isOn ? "Apagar luz" : "Encender luz";

        protected override void Awake()
        {
            base.Awake();
            if (rocker != null) m_RockerBase = rocker.localRotation;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (target != null) target.Changed += OnTargetChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (target != null) target.Changed -= OnTargetChanged;
        }

        void Start() => UpdateRocker();

        void OnTargetChanged(RoomLights r) => UpdateRocker();

        public override void Interact()
        {
            if (target != null) target.Toggle();
            PlayClip(clickClip, 0.9f);
        }

        void UpdateRocker()
        {
            if (rocker == null) return;
            bool on = target != null && target.isOn;
            rocker.localRotation = m_RockerBase * Quaternion.AngleAxis(on ? rockerAngle : -rockerAngle, rockerAxisLocal);
        }
    }
}
