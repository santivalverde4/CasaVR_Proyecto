using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CasaVR
{
    /// <summary>Cualquier objeto con el que se puede interactuar (VR o escritorio).</summary>
    public interface ICasaInteractable
    {
        string Prompt { get; }
        void Interact();
    }

    /// <summary>
    /// Base reutilizable compatible con XR Interaction Toolkit: se conecta a un XRBaseInteractable
    /// (XRSimpleInteractable) del mismo objeto. Un "clic" (seleccionar y soltar sin arrastrar)
    /// llama a <see cref="Interact"/>; las clases que soportan arrastre (puertas, ventanas)
    /// reciben además OnGrabStart/Update/End mientras el control mantiene la selección.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class CasaInteractable : MonoBehaviour, ICasaInteractable
    {
        public XRBaseInteractable xrInteractable;
        public AudioSource audioSource;

        public abstract string Prompt { get; }
        public abstract void Interact();

        protected virtual bool SupportsDrag => false;
        protected virtual void OnGrabStart(IXRSelectInteractor interactor, Vector3 grabPoint) { }
        protected virtual void OnGrabUpdate(IXRSelectInteractor interactor) { }
        /// <summary>Devuelve true si hubo arrastre real (entonces no se trata como clic).</summary>
        protected virtual bool OnGrabEnd(IXRSelectInteractor interactor) => false;

        protected IXRSelectInteractor Grabber { get; private set; }

        protected virtual void Awake()
        {
            if (xrInteractable == null) xrInteractable = GetComponent<XRBaseInteractable>();
        }

        protected virtual void OnEnable()
        {
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.AddListener(OnSelectEntered);
                xrInteractable.selectExited.AddListener(OnSelectExited);
            }
        }

        protected virtual void OnDisable()
        {
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
                xrInteractable.selectExited.RemoveListener(OnSelectExited);
            }
            Grabber = null;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (Grabber != null) return;
            Grabber = args.interactorObject;
            if (SupportsDrag) OnGrabStart(Grabber, GrabPoint(Grabber));
            else Interact();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject != Grabber) return;
            var g = Grabber;
            Grabber = null;
            if (!SupportsDrag) return;
            bool dragged = OnGrabEnd(g);
            if (!dragged && !args.isCanceled) Interact();
        }

        protected virtual void Update()
        {
            if (Grabber != null && SupportsDrag) OnGrabUpdate(Grabber);
        }

        /// <summary>Punto donde el rayo toca el objeto (o la posición del control).</summary>
        protected static Vector3 GrabPoint(IXRSelectInteractor interactor)
        {
            if (interactor is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out RaycastHit hit))
                return hit.point;
            return interactor.transform.position;
        }

        protected void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 0.6f;
                audioSource.maxDistance = 12f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            }
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
