using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CasaVR
{
    /// <summary>
    /// Puerta con bisagra. El pivote del objeto está en la bisagra (preparado en Blender).
    /// Clic = abrir/cerrar animado. Mantener seleccionado y mover el control = empujar/jalar la hoja.
    /// La hoja es sólida (BoxCollider + Rigidbody cinemático) y se detiene si fuera a golpear al jugador.
    /// </summary>
    public class HingedDoor : CasaInteractable
    {
        [Tooltip("Ángulo de apertura con signo, alrededor del eje vertical del mundo (grados).")]
        public float openAngle = 90f;
        public float speed = 150f;
        public bool startOpen;
        public AudioClip openClip;
        public AudioClip closeClip;

        Quaternion m_ClosedRotation;
        float m_Angle;
        float m_Target;
        BoxCollider m_Box;
        float m_GrabStartAngle;
        Vector3 m_GrabStartHand;
        Vector3 m_Tangent;
        float m_Radius = 0.6f;
        readonly Collider[] m_Hits = new Collider[16];

        public bool IsOpen => Mathf.Abs(m_Target) > Mathf.Abs(openAngle) * 0.5f;
        public float CurrentAngle => m_Angle;
        public override string Prompt => IsOpen ? "Cerrar puerta" : "Abrir puerta";
        protected override bool SupportsDrag => true;

        protected override void Awake()
        {
            base.Awake();
            m_ClosedRotation = transform.rotation;
            m_Box = GetComponent<BoxCollider>();
            if (startOpen)
            {
                m_Angle = m_Target = openAngle;
                transform.rotation = Quaternion.AngleAxis(m_Angle, Vector3.up) * m_ClosedRotation;
            }
        }

        public override void Interact()
        {
            bool opening = !IsOpen;
            m_Target = opening ? openAngle : 0f;
            PlayClip(opening ? openClip : closeClip, 0.8f);
        }

        public void SetOpen(bool open)
        {
            if (open != IsOpen) Interact();
        }

        protected override void Update()
        {
            base.Update();
            if (Grabber != null) return;
            if (!Mathf.Approximately(m_Angle, m_Target))
                TryApply(Mathf.MoveTowards(m_Angle, m_Target, speed * Time.deltaTime));
        }

        protected override void OnGrabStart(IXRSelectInteractor interactor, Vector3 grabPoint)
        {
            m_GrabStartAngle = m_Angle;
            m_GrabStartHand = interactor.transform.position;
            Vector3 r = grabPoint - transform.position; r.y = 0f;
            m_Radius = Mathf.Max(0.3f, r.magnitude);
            if (r.sqrMagnitude < 1e-4f) r = transform.right;
            // dirección en que se mueve el punto agarrado cuando el ángulo aumenta
            m_Tangent = Vector3.Cross(Vector3.up, r.normalized);
        }

        protected override void OnGrabUpdate(IXRSelectInteractor interactor)
        {
            Vector3 d = interactor.transform.position - m_GrabStartHand; d.y = 0f;
            float delta = Vector3.Dot(d, m_Tangent) / m_Radius * Mathf.Rad2Deg * 1.5f;
            float lo = Mathf.Min(0f, openAngle), hi = Mathf.Max(0f, openAngle);
            float desired = Mathf.Clamp(m_GrabStartAngle + delta, lo, hi);
            TryApply(Mathf.MoveTowards(m_Angle, desired, speed * 2f * Time.deltaTime));
        }

        protected override bool OnGrabEnd(IXRSelectInteractor interactor)
        {
            bool dragged = Mathf.Abs(m_Angle - m_GrabStartAngle) > 6f;
            if (dragged) m_Target = m_Angle;
            return dragged;
        }

        void TryApply(float angle)
        {
            Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up) * m_ClosedRotation;
            if (WouldHitPlayer(rot))
            {
                m_Target = m_Angle;   // se detiene: el jugador está en el camino
                return;
            }
            transform.rotation = rot;
            m_Angle = angle;
        }

        bool WouldHitPlayer(Quaternion rot)
        {
            var player = CasaVRPlayer.Instance;
            if (player == null || player.Controller == null || m_Box == null) return false;
            Vector3 s = transform.lossyScale;
            Vector3 center = transform.position + rot * Vector3.Scale(m_Box.center, s);
            Vector3 half = Vector3.Scale(m_Box.size, s) * 0.5f;
            int n = Physics.OverlapBoxNonAlloc(center, half, m_Hits, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (m_Hits[i] == player.Controller) return true;
            return false;
        }
    }
}
