using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CasaVR
{
    /// <summary>
    /// Hoja corrediza de ventana: se desliza sobre su riel (eje calculado desde el modelo).
    /// Clic = abrir/cerrar; mantener y mover el control = deslizar a mano.
    /// </summary>
    public class SlidingWindow : CasaInteractable
    {
        [Tooltip("Desplazamiento de la hoja abierta, en el espacio local del padre.")]
        public Vector3 openLocalOffset = new Vector3(0.5f, 0f, 0f);
        public float speed = 0.5f;
        public AudioClip slideClip;

        Vector3 m_ClosedLocal;
        float m_T;
        float m_TargetT;
        float m_GrabStartT;
        Vector3 m_GrabStartHand;

        public bool IsOpen => m_TargetT > 0.5f;
        public float OpenAmount => m_T;
        public override string Prompt => IsOpen ? "Cerrar ventana" : "Abrir ventana";
        protected override bool SupportsDrag => true;

        float WorldDistance => transform.parent != null
            ? transform.parent.TransformVector(openLocalOffset).magnitude
            : openLocalOffset.magnitude;

        Vector3 WorldAxis => (transform.parent != null
            ? transform.parent.TransformDirection(openLocalOffset)
            : openLocalOffset).normalized;

        protected override void Awake()
        {
            base.Awake();
            m_ClosedLocal = transform.localPosition;
        }

        public override void Interact()
        {
            m_TargetT = IsOpen ? 0f : 1f;
            PlayClip(slideClip, 0.7f);
        }

        protected override void Update()
        {
            base.Update();
            if (Grabber == null && !Mathf.Approximately(m_T, m_TargetT))
            {
                float dist = Mathf.Max(0.01f, WorldDistance);
                m_T = Mathf.MoveTowards(m_T, m_TargetT, speed / dist * Time.deltaTime);
                Apply();
            }
        }

        protected override void OnGrabStart(IXRSelectInteractor interactor, Vector3 grabPoint)
        {
            m_GrabStartT = m_T;
            m_GrabStartHand = interactor.transform.position;
        }

        protected override void OnGrabUpdate(IXRSelectInteractor interactor)
        {
            float dist = Mathf.Max(0.01f, WorldDistance);
            float d = Vector3.Dot(interactor.transform.position - m_GrabStartHand, WorldAxis);
            float desired = Mathf.Clamp01(m_GrabStartT + d * 1.5f / dist);
            m_T = Mathf.MoveTowards(m_T, desired, 2f * speed / dist * Time.deltaTime);
            Apply();
        }

        protected override bool OnGrabEnd(IXRSelectInteractor interactor)
        {
            bool dragged = Mathf.Abs(m_T - m_GrabStartT) > 0.06f;
            if (dragged) m_TargetT = m_T;
            return dragged;
        }

        void Apply()
        {
            transform.localPosition = m_ClosedLocal + openLocalOffset * m_T;
        }
    }
}
