using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace CasaVR
{
    /// <summary>
    /// Jugador de la casa: CharacterController con gravedad real, colisiones con paredes/muebles,
    /// caminata continua con el joystick izquierdo, subida de escaleras (rampas invisibles + stepOffset),
    /// giro por pasos o suave y "cuerpo que sigue a la cabeza" (el movimiento físico del visor también
    /// respeta las paredes).
    /// Altura: los pies del XR Origin quedan en el piso y la vista a una altura humana. Con visor en modo
    /// Floor (Meta Quest) el runtime ya entrega la altura real de la cabeza, así que el Camera Offset
    /// queda en 0; sin altura de piso (modo Device o XR Interaction Simulator) el offset es
    /// <see cref="standingEyeHeight"/>. Nunca se suman las dos (antes eso ponía la vista a ~3.2 m).
    /// Funciona con visor (Meta Quest / OpenXR), con el XR Interaction Simulator y en modo escritorio
    /// (teclado y mouse) cuando no hay ningún dispositivo XR.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    public class CasaVRPlayer : MonoBehaviour
    {
        public static CasaVRPlayer Instance { get; private set; }

        [Header("Referencias")]
        public XROrigin origin;
        public Transform cameraOffset;
        public Transform head;
        public GameObject[] xrOnlyObjects;        // controles, rayos, etc.

        [Header("Entradas XR")]
        public InputActionReference moveInput;    // stick izquierdo
        public InputActionReference turnInput;    // stick derecho
        [Header("Entradas escritorio")]
        public InputActionReference desktopMove;
        public InputActionReference desktopLook;
        public InputActionReference desktopRun;
        public InputActionReference desktopReleaseCursor;

        [Header("Movimiento")]
        public float moveSpeed = 1.6f;
        public float runMultiplier = 1.8f;
        public bool snapTurn = true;
        public float snapTurnAngle = 45f;
        public float smoothTurnSpeed = 90f;
        public float gravity = -9.81f;
        public float maxFallSpeed = -25f;
        public float minBodyHeight = 1.0f;
        [Tooltip("Altura máxima de la cápsula: menor que el vano libre de las puertas (2.07 m)")]
        public float maxBodyHeight = 1.85f;
        [Tooltip("Altura de los ojos cuando el dispositivo no la entrega (simulador XR, modo Device)")]
        public float standingEyeHeight = 1.70f;
        public float desktopEyeHeight = 1.70f;
        [Tooltip("Zona muerta del joystick izquierdo")]
        public float stickDeadzone = 0.15f;
        public float mouseSensitivity = 0.12f;
        [Tooltip("Altura (m) a partir de la cual se considera que el jugador está en el piso 2")]
        public float floor2Height = 2.5f;

        [Header("Inicio")]
        [Tooltip("Punto donde aparece el jugador cuando el visor empieza a reportar su posición")]
        public Transform startSpawn;
        public Transform startLook;

        [Header("Depuración / pruebas")]
        public bool useDebugInput;
        public Vector2 debugMove;

        public CharacterController Controller { get; private set; }
        public bool DesktopMode { get; private set; }
        public bool IsGrounded => m_Grounded;
        public float VerticalVelocity => m_VerticalVelocity;
        /// <summary>Bloquea el movimiento (p. ej. mientras el menú está abierto en modo escritorio).</summary>
        public bool InputLocked { get; set; }

        float m_VerticalVelocity;
        bool m_Grounded;
        bool m_SnapReady = true;
        float m_Pitch;
        bool m_ModeEvaluated;
        bool m_Teleported;
        static readonly List<UnityEngine.XR.XRInputSubsystem> s_InputSubsystems = new List<UnityEngine.XR.XRInputSubsystem>();
        int m_FloorGuessMode = -1;
        bool m_FloorGuess;

        /// <summary>Altura (m) que se está aplicando al Camera Offset.</summary>
        public float AppliedCameraOffset => cameraOffset != null ? cameraOffset.localPosition.y : 0f;
        /// <summary>Altura de los ojos sobre los pies del jugador.</summary>
        public float EyeHeight => head != null ? head.position.y - transform.position.y : 0f;

        void Awake()
        {
            Instance = this;
            Controller = GetComponent<CharacterController>();
            if (origin == null) origin = GetComponent<XROrigin>();
            if (head == null && origin != null && origin.Camera != null) head = origin.Camera.transform;
            if (cameraOffset == null && origin != null && origin.CameraFloorOffsetObject != null)
                cameraOffset = origin.CameraFloorOffsetObject.transform;
            Controller.center = new Vector3(0f, Controller.height * 0.5f + Controller.skinWidth, 0f);
            // XROrigin usa este valor en modo Device; debe coincidir con el nuestro para no pelear por el offset
            if (origin != null) origin.CameraYOffset = standingEyeHeight;
            UpdateCameraHeight();
        }

        void OnEnable()
        {
            EnableAction(moveInput); EnableAction(turnInput);
            EnableAction(desktopMove); EnableAction(desktopLook); EnableAction(desktopRun); EnableAction(desktopReleaseCursor);
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        IEnumerator Start()
        {
            SetDesktopMode(!IsXRPresent());
            // El simulador XR puede crear sus dispositivos unos cuadros después.
            yield return new WaitForSeconds(0.5f);
            SetDesktopMode(!IsXRPresent());
            m_ModeEvaluated = true;
            // Con visor real el origen de tracking puede estar lejos del centro del cuarto: cuando la cabeza
            // ya tiene posición, se recoloca al jugador en el punto de inicio (evita arrastrarlo contra paredes).
            // Con el simulador XR la cabeza simulada empieza en (0,0,0) y el XR Origin ya está en el punto de
            // inicio, así que no se hace (evita que el jugador "salte" de vuelta a la entrada).
            if (!DesktopMode && RealTrackingRunning() && startSpawn != null && head != null && !m_Teleported)
            {
                Vector3 p0 = transform.position;
                float t = 0f;
                while (t < 3f && head.localPosition.sqrMagnitude < 1e-6f) { t += Time.deltaTime; yield return null; }
                bool moved = (transform.position - p0).sqrMagnitude > 0.04f;
                if (!m_Teleported && !moved) TeleportTo(startSpawn, startLook);
            }
        }

        static void EnableAction(InputActionReference r)
        {
            if (r != null && r.action != null && !r.action.enabled) r.action.Enable();
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!m_ModeEvaluated) return;
            if (device is XRHMD && (change == InputDeviceChange.Added || change == InputDeviceChange.Removed))
                SetDesktopMode(!IsXRPresent());
        }

        /// <summary>True si hay un subsistema de tracking XR real funcionando (visor, no el simulador).</summary>
        public static bool RealTrackingRunning()
        {
            SubsystemManager.GetSubsystems(s_InputSubsystems);
            foreach (var s in s_InputSubsystems)
                if (s != null && s.running) return true;
            return false;
        }

        /// <summary>True si hay un visor real (subsistema de display activo) o un HMD simulado.</summary>
        public static bool IsXRPresent()
        {
            var displays = new List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays)
                if (d.running) return true;
            foreach (var dev in InputSystem.devices)
                if (dev is XRHMD) return true;
            return false;
        }

        public void SetDesktopMode(bool desktop)
        {
            DesktopMode = desktop;
            if (xrOnlyObjects != null)
                foreach (var go in xrOnlyObjects)
                    if (go != null) go.SetActive(!desktop);
            if (head != null)
            {
                var tpd = head.GetComponent<TrackedPoseDriver>();
                // la altura de escritorio vive en la cámara; la de XR, en el Camera Offset: nunca las dos
                head.localPosition = desktop ? new Vector3(0f, desktopEyeHeight, 0f) : Vector3.zero;
                head.localRotation = Quaternion.identity;
                m_Pitch = 0f;
                if (tpd != null) tpd.enabled = !desktop;
            }
            if (desktop && cameraOffset != null)
                cameraOffset.localPosition = Vector3.zero;
            else
                UpdateCameraHeight();
            SetCursorLocked(desktop);
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            RoomLights.SetActiveFloor(transform.position.y > floor2Height ? 2 : 1);
            if (Controller == null || !Controller.enabled) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (DesktopMode) UpdateDesktopLook();
            else { UpdateCameraHeight(); UpdateTurn(); }

            UpdateBodyHeight();
            if (!DesktopMode) FollowHead();

            // ---- desplazamiento + gravedad (un solo Move para respetar colisiones)
            Vector2 input = ReadMoveInput();
            Vector3 fwd = head != null ? head.forward : transform.forward;
            fwd = Vector3.ProjectOnPlane(fwd, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(head != null ? head.up : transform.forward, Vector3.up);
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float speed = moveSpeed;
            if (DesktopMode && desktopRun != null && desktopRun.action != null && desktopRun.action.IsPressed()) speed *= runMultiplier;
            Vector3 horizontal = (fwd * input.y + right * input.x) * speed;

            // m_Grounded viene del Move con gravedad del cuadro anterior (no del Move horizontal de FollowHead)
            if (m_Grounded && m_VerticalVelocity < 0f)
                m_VerticalVelocity = -2f;           // mantiene el contacto en rampas y escaleras
            else
                m_VerticalVelocity = Mathf.Max(m_VerticalVelocity + gravity * dt, maxFallSpeed);

            CollisionFlags flags = Controller.Move((horizontal + Vector3.up * m_VerticalVelocity) * dt);
            m_Grounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && m_VerticalVelocity > 0f) m_VerticalVelocity = 0f;
        }

        Vector2 ReadMoveInput()
        {
            if (InputLocked) return Vector2.zero;
            if (useDebugInput) return Vector2.ClampMagnitude(debugMove, 1f);
            Vector2 v = Vector2.zero;
            if (DesktopMode)
            {
                if (desktopMove != null && desktopMove.action != null) v = desktopMove.action.ReadValue<Vector2>();
            }
            else
            {
                // joystick izquierdo del control (Input System: <XRController>{LeftHand}/{Primary2DAxis})
                // (la acción ya aplica la zona muerta con el procesador StickDeadzone)
                if (moveInput != null && moveInput.action != null) v = moveInput.action.ReadValue<Vector2>();
                // respaldo: API de XR de Unity, por si el perfil de interacción del control no
                // llegó al Input System (p. ej. un perfil de OpenXR desactivado)
                if (v.sqrMagnitude < 1e-4f)
                {
                    v = ReadLegacyStick(UnityEngine.XR.XRNode.LeftHand);
                    float m = v.magnitude;
                    v = m < stickDeadzone ? Vector2.zero : v * (Mathf.Min(1f, (m - stickDeadzone) / (1f - stickDeadzone)) / m);
                }
            }
            return Vector2.ClampMagnitude(v, 1f);
        }

        static Vector2 ReadLegacyStick(UnityEngine.XR.XRNode node)
        {
            var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 v)) return v;
            return Vector2.zero;
        }

        /// <summary>
        /// Fija la altura del Camera Offset según cómo reporta el dispositivo la cabeza (ver resumen de la clase).
        /// Se evalúa cada cuadro: el modo de tracking puede cambiar al recentrar o al conectar el visor.
        /// </summary>
        public void UpdateCameraHeight()
        {
            if (cameraOffset == null || DesktopMode) return;
            float target = DesiredCameraOffset();
            var lp = cameraOffset.localPosition;
            if (Mathf.Abs(lp.y - target) > 0.001f)
            {
                lp.y = target;
                cameraOffset.localPosition = lp;
            }
        }

        float DesiredCameraOffset()
        {
            SubsystemManager.GetSubsystems(s_InputSubsystems);
            bool realTracking = false;
            var mode = UnityEngine.XR.TrackingOriginModeFlags.Unknown;
            foreach (var s in s_InputSubsystems)
            {
                if (s == null || !s.running) continue;
                realTracking = true;
                mode = s.GetTrackingOriginMode();
                if (mode == UnityEngine.XR.TrackingOriginModeFlags.Floor) break;
            }
            if (!realTracking) { m_FloorGuessMode = -1; return standingEyeHeight; }   // simulador XR
            if (mode == UnityEngine.XR.TrackingOriginModeFlags.Floor) return 0f;
            // Modo Device: normalmente la cabeza empieza en y≈0 → se sube a la altura de los ojos. Si el runtime
            // ya entrega alturas desde el piso (cabeza > 1 m), no se suma nada. Se decide una sola vez por modo
            // (no cada cuadro), para que agacharse no haga saltar la vista.
            if (m_FloorGuessMode != (int)mode && head != null && head.localPosition.sqrMagnitude > 1e-4f)
            {
                m_FloorGuessMode = (int)mode;
                m_FloorGuess = head.localPosition.y > 1.0f;
            }
            return m_FloorGuessMode == (int)mode && m_FloorGuess ? 0f : standingEyeHeight;
        }

        void UpdateTurn()
        {
            if (InputLocked || turnInput == null || turnInput.action == null || origin == null) return;
            float x = turnInput.action.ReadValue<Vector2>().x;
            if (snapTurn)
            {
                if (Mathf.Abs(x) > 0.75f && m_SnapReady)
                {
                    origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(x) * snapTurnAngle);
                    m_SnapReady = false;
                }
                else if (Mathf.Abs(x) < 0.3f)
                {
                    m_SnapReady = true;
                }
            }
            else if (Mathf.Abs(x) > 0.15f)
            {
                origin.RotateAroundCameraUsingOriginUp(x * smoothTurnSpeed * Time.deltaTime);
            }
        }

        void UpdateDesktopLook()
        {
            if (head == null) return;
            if (desktopReleaseCursor != null && desktopReleaseCursor.action != null && desktopReleaseCursor.action.WasPressedThisFrame())
                SetCursorLocked(false);
            if (Cursor.lockState != CursorLockMode.Locked || InputLocked) return;
            if (desktopLook == null || desktopLook.action == null) return;
            Vector2 d = desktopLook.action.ReadValue<Vector2>() * mouseSensitivity;
            transform.Rotate(0f, d.x, 0f, Space.World);
            m_Pitch = Mathf.Clamp(m_Pitch - d.y, -80f, 80f);
            head.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
        }

        void UpdateBodyHeight()
        {
            float h;
            if (DesktopMode || origin == null) h = desktopEyeHeight + 0.12f;
            else h = origin.CameraInOriginSpaceHeight + 0.1f;
            h = Mathf.Clamp(h, minBodyHeight, maxBodyHeight);
            if (Mathf.Abs(Controller.height - h) > 0.01f)
            {
                Controller.height = h;
                Controller.center = new Vector3(0f, h * 0.5f + Controller.skinWidth, 0f);
            }
        }

        /// <summary>
        /// Mueve la cápsula hacia donde está la cabeza (caminar físicamente) usando CharacterController.Move,
        /// y compensa el Camera Offset para que la vista no se desplace. Si una pared lo impide, la vista
        /// se empuja de vuelta: no se puede atravesar paredes ni siquiera caminando en la habitación real.
        /// </summary>
        void FollowHead()
        {
            if (head == null || cameraOffset == null) return;
            Vector3 delta = head.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0004f) return;
            Vector3 before = transform.position;
            Controller.Move(delta);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            // La cámara no debe moverse por el Move del cuerpo...
            Vector3 blocked = delta - moved;
            // ...y lo que no se pudo mover (pared) empuja la vista hacia atrás.
            cameraOffset.position -= moved + blocked;
        }

        /// <summary>
        /// Teletransporta al jugador: la cabeza queda sobre 'target' mirando hacia 'lookAt' y los pies sobre
        /// el piso de ese punto (se busca con un rayo hacia abajo, así nunca aparece flotando ni enterrado).
        /// </summary>
        public void TeleportTo(Vector3 target, Vector3 lookAt) => TeleportTo(target, lookAt, true);

        public void TeleportTo(Vector3 target, Vector3 lookAt, bool snapToGround)
        {
            m_Teleported = true;
            if (snapToGround) target = GroundPoint(target);
            Controller.enabled = false;
            Vector3 dir = lookAt - target; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            if (DesktopMode)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                m_Pitch = 0f;
                if (head != null) head.localRotation = Quaternion.identity;
                if (cameraOffset != null) cameraOffset.localPosition = Vector3.zero;
                transform.position = target;
            }
            else
            {
                if (cameraOffset != null)
                {
                    var lp = cameraOffset.localPosition;
                    cameraOffset.localPosition = new Vector3(0f, lp.y, 0f);
                }
                Vector3 camFwd = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : transform.forward;
                if (camFwd.sqrMagnitude > 1e-4f && origin != null)
                    origin.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(camFwd, dir, Vector3.up));
                Vector3 headOffset = head != null ? head.position - transform.position : Vector3.zero;
                headOffset.y = 0f;
                transform.position = target - headOffset;
                FollowHeadImmediate();
            }
            m_VerticalVelocity = 0f;
            m_Grounded = false;
            Physics.SyncTransforms();
            Controller.enabled = true;
        }

        /// <summary>
        /// Piso bajo 'p': la superficie pisable más alta que no esté más de 0.35 m por encima de 'p'
        /// (un escalón) ni más de 2 m por debajo. Si no hay ninguna, devuelve 'p'.
        /// </summary>
        public static Vector3 GroundPoint(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 0.35f, Vector3.down, 2.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float bestY = float.MinValue; bool found = false;
            foreach (var h in hits)
            {
                if (h.normal.y < 0.6f) continue;                    // solo superficies pisables
                if (h.collider.GetComponentInParent<CasaVRPlayer>() != null) continue;
                if (h.point.y > bestY) { bestY = h.point.y; found = true; }
            }
            return found ? new Vector3(p.x, bestY + 0.01f, p.z) : p;
        }

        void FollowHeadImmediate()
        {
            if (head == null || cameraOffset == null) return;
            Vector3 delta = head.position - transform.position; delta.y = 0f;
            transform.position += delta;
            cameraOffset.position -= delta;
        }

        public void TeleportTo(Transform spawn, Transform look)
        {
            if (spawn == null) return;
            TeleportTo(spawn.position, look != null ? look.position : spawn.position + spawn.forward);
        }
    }
}
