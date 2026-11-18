using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CasaVR.Tests
{
    /// <summary>
    /// Pruebas de integración (Window > General > Test Runner > PlayMode > Run All).
    /// Cargan la escena real de la casa y verifican gravedad, colisiones, escaleras,
    /// puertas, ventanas, interruptores, agua y el menú de teletransporte.
    /// </summary>
    public class CasaVRPlayModeTests
    {
        const string ScenePath = "Assets/CasaVR/Scenes/CasaVR.unity";
        CasaVRPlayer m_Player;
        TeleportMenu m_Menu;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(0.8f);
            m_Player = CasaVRPlayer.Instance;
            m_Menu = Object.FindFirstObjectByType<TeleportMenu>();
            Assert.IsNotNull(m_Player, "No hay CasaVRPlayer en la escena");
            Assert.IsNotNull(m_Menu, "No hay TeleportMenu en la escena");
            m_Player.useDebugInput = true;
            m_Player.debugMove = Vector2.zero;
        }

        RoomEntry Room(string id)
        {
            var r = m_Menu.Find(id);
            Assert.IsNotNull(r, "Falta el destino " + id);
            return r;
        }

        IEnumerator Walk(Vector2 input, float seconds)
        {
            m_Player.debugMove = input;
            yield return new WaitForSeconds(seconds);
            m_Player.debugMove = Vector2.zero;
            yield return new WaitForSeconds(0.2f);
        }

        IEnumerator WalkTo(Vector3 target, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                Vector3 d = target - m_Player.head.position; d.y = 0f;
                if (d.magnitude < 0.04f) yield break;
                m_Player.Controller.Move(Vector3.ClampMagnitude(d, 1.5f * Time.deltaTime));
                t += Time.deltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Mesas_SePuedenRodearPorTodosLados()
        {
            foreach (int floor in new[] { 1, 2 })
            foreach (string mesa in new[] { "Comedor", "MesaCentro" })
            {
                string p = "P" + floor;
                var go = GameObject.Find("Mueble_" + p + "_" + mesa);
                Assert.IsNotNull(go, "Falta Mueble_" + p + "_" + mesa);
                Bounds b = go.GetComponent<Collider>().bounds;
                if (mesa == "Comedor")
                    foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                        if (c.name.StartsWith("Mueble_" + p + "_Silla_")) b.Encapsulate(c.bounds);
                // recorrido rectangular a 6 cm de holgura entre la cápsula y la mesa/sillas
                float m = m_Player.Controller.radius + m_Player.Controller.skinWidth + 0.06f;
                float y = b.min.y;
                var pts = new[]
                {
                    new Vector3(b.min.x - m, y, b.min.z - m), new Vector3(b.max.x + m, y, b.min.z - m),
                    new Vector3(b.max.x + m, y, b.max.z + m), new Vector3(b.min.x - m, y, b.max.z + m),
                };
                m_Player.TeleportTo(pts[0], pts[1]);
                yield return new WaitForSeconds(0.5f);
                for (int i = 1; i <= 4; i++)
                {
                    Vector3 target = pts[i % 4];
                    yield return WalkTo(target, 6f);
                    Vector3 h = m_Player.head.position; h.y = target.y;
                    Assert.Less(Vector3.Distance(h, target), 0.12f,
                        "No se pudo rodear " + mesa + " del piso " + floor + ": el jugador se atascó camino a la esquina " + (i % 4));
                }
            }
        }

        static float FloorBelow(Vector3 p) => CasaVRPlayer.GroundPoint(p).y - 0.01f;

        [UnityTest]
        public IEnumerator Altura_VistaHumanaYPiesEnElSuelo()
        {
            var r = Room("P1_Sala");
            m_Player.TeleportTo(r.spawn, r.look);
            yield return new WaitForSeconds(1f);
            float eye = m_Player.EyeHeight;
            Assert.That(eye, Is.InRange(1.6f, 1.8f), "Altura de la vista fuera de lo normal: " + eye.ToString("0.00") + " m");
            Assert.IsTrue(m_Player.IsGrounded, "El jugador no está apoyado en el piso");
            Assert.That(m_Player.transform.position.y, Is.EqualTo(FloorBelow(m_Player.transform.position)).Within(0.04f), "Los pies no están en el piso");
            float top = m_Player.Controller.center.y + m_Player.Controller.height * 0.5f + m_Player.Controller.skinWidth;
            Assert.Less(top, 2.0f, "La cápsula del jugador no cabría por las puertas (vano 2.07 m)");
            Assert.AreEqual(Vector3.one, m_Player.transform.lossyScale, "El XR Origin no debe estar escalado");
        }

        [UnityTest]
        public IEnumerator Teletransporte_TodosLosDestinosQuedanSobreElSuelo()
        {
            foreach (var r in m_Menu.rooms)
            {
                m_Player.TeleportTo(r.spawn, r.look);
                yield return new WaitForSeconds(0.6f);
                Vector3 feet = m_Player.transform.position;
                Assert.IsTrue(m_Player.IsGrounded, r.id + ": el jugador quedó flotando");
                Assert.That(feet.y, Is.EqualTo(FloorBelow(feet)).Within(0.05f), r.id + ": los pies no quedaron en el piso");
                Assert.That(m_Player.EyeHeight, Is.InRange(1.55f, 1.85f), r.id + ": altura de la vista incorrecta");
                Vector3 a = m_Player.head.position, b = r.spawn.position; a.y = b.y = 0f;
                Assert.Less(Vector3.Distance(a, b), 0.35f, r.id + ": no llegó al destino");
            }
        }

        [UnityTest]
        public IEnumerator Joystick_IzquierdoCaminaConGravedadYColisiones()
        {
            // control XR virtual (mismo camino que el Meta Quest: <XRController>{LeftHand}/{Primary2DAxis})
            InputSystem.RegisterLayout(@"{ ""name"": ""CasaVRPruebaControl"", ""extend"": ""XRController"",
                ""controls"": [ { ""name"": ""thumbstick"", ""layout"": ""Stick"", ""usage"": ""Primary2DAxis"" } ] }");
            var dev = InputSystem.AddDevice("CasaVRPruebaControl");
            bool wasDesktop = m_Player.DesktopMode;
            try
            {
                InputSystem.SetDeviceUsage(dev, UnityEngine.InputSystem.CommonUsages.LeftHand);
                m_Player.SetDesktopMode(false);
                m_Player.useDebugInput = false;
                var r = Room("P2_Pasillo");
                m_Player.TeleportTo(r.spawn, r.look);
                yield return new WaitForSeconds(0.5f);
                Vector3 p0 = m_Player.transform.position;
                Vector3 dir = r.look.position - r.spawn.position; dir.y = 0f; dir.Normalize();
                var stick = (StickControl)dev["thumbstick"];
                float t = 0f;
                while (t < 1.2f)
                {
                    InputSystem.QueueDeltaStateEvent(stick, new Vector2(0f, 1f));
                    t += Time.deltaTime;
                    yield return null;
                }
                InputSystem.QueueDeltaStateEvent(stick, Vector2.zero);
                yield return new WaitForSeconds(0.3f);
                Vector3 moved = m_Player.transform.position - p0;
                Assert.Greater(Vector3.Dot(moved, dir), 1.0f, "El joystick izquierdo no movió al jugador hacia adelante");
                Assert.IsTrue(m_Player.IsGrounded, "Al caminar con el joystick el jugador dejó de estar en el piso");
                Assert.That(m_Player.transform.position.y, Is.EqualTo(p0.y).Within(0.06f), "Al caminar el jugador subió o bajó");
            }
            finally
            {
                InputSystem.RemoveDevice(dev);
                m_Player.useDebugInput = true;
                if (wasDesktop) m_Player.SetDesktopMode(true);
            }
        }

        [UnityTest]
        public IEnumerator Puertas_SeCabePorTodasConLaAlturaMaxima()
        {
            float minH = m_Player.minBodyHeight;
            m_Player.minBodyHeight = m_Player.maxBodyHeight;      // persona alta: cápsula de altura máxima
            try
            {
                foreach (var door in Object.FindObjectsByType<HingedDoor>(FindObjectsSortMode.None).OrderBy(d => d.name))
                {
                    Transform root = door.transform.parent;
                    var frame = root.Find(root.name + "_Marco");
                    Assert.IsNotNull(frame, "Falta el marco de " + root.name);
                    Bounds fb = frame.GetComponent<Renderer>().bounds;
                    Vector3 n = fb.size.x < fb.size.z ? Vector3.right : Vector3.forward;   // normal del vano
                    Vector3 t = Vector3.Cross(Vector3.up, n);
                    Vector3 c = fb.center; c.y = fb.min.y;
                    // centro del paso libre: la hoja abierta ocupa unos centímetros del lado de la bisagra
                    c -= t * Mathf.Sign(Vector3.Dot(door.transform.position - c, t)) * 0.08f;
                    // lejos de la puerta mientras abre (si la hoja toca al jugador se detiene)
                    var lejos = Room("Ext_JardinTrasero");
                    m_Player.TeleportTo(lejos.spawn, lejos.look);
                    yield return null;
                    door.SetOpen(true);
                    yield return new WaitForSeconds(1.0f);
                    Assert.IsTrue(door.IsOpen && Mathf.Abs(door.CurrentAngle) > 80f, root.name + " no abrió por completo");
                    Vector3 a = c - n * 0.45f, b = c + n * 0.4f;
                    m_Player.TeleportTo(a, b);
                    yield return new WaitForSeconds(0.4f);
                    Assert.That(m_Player.Controller.height, Is.EqualTo(m_Player.maxBodyHeight).Within(0.02f));
                    yield return WalkTo(b, 4f);
                    Assert.Greater(Vector3.Dot(m_Player.head.position - c, n), 0.3f, "No se pudo pasar por " + root.name);
                    yield return WalkTo(a, 4f);
                    Assert.Less(Vector3.Dot(m_Player.head.position - c, n), -0.3f, "No se pudo regresar por " + root.name);
                    door.SetOpen(false);
                }
            }
            finally
            {
                m_Player.minBodyHeight = minH;
            }
        }

        [UnityTest]
        public IEnumerator Menu_TresCategoriasConDestinosYSinOpcionesDeGiro()
        {
            m_Menu.Open();
            yield return null;
            Assert.IsTrue(m_Menu.IsOpen);
            var canvas = m_Menu.MenuCanvas;
            var texts = canvas.GetComponentsInChildren<Text>(true).Select(x => x.text).ToList();
            foreach (var cat in new[] { "Piso 1", "Piso 2", "Exterior", "Cerrar" })
                Assert.Contains(cat, texts, "Falta " + cat + " en el menú");
            Assert.IsTrue(texts.Any(x => x == "Modo noche" || x == "Modo día"), "Falta el botón de modo día/noche");
            Assert.IsFalse(texts.Any(x => x.Contains("Giro")), "El menú no debe tener opciones de giro");
            var buttons = canvas.GetComponentsInChildren<Button>(true);
            Assert.AreEqual(m_Menu.rooms.Count + 2, buttons.Length, "Debe haber un botón por destino más Modo día/noche y Cerrar");
            foreach (var r in m_Menu.rooms)
                Assert.Contains(r.displayName, texts, "Falta el destino " + r.displayName);
            // el menú queda delante de la vista, a distancia cómoda y a la altura de los ojos
            Vector3 toMenu = canvas.transform.position - m_Player.head.position;
            Assert.That(toMenu.magnitude, Is.InRange(0.35f, 1.5f));
            Assert.That(canvas.transform.position.y - m_Player.head.position.y, Is.InRange(-0.25f, 0.05f));
            // elegir un destino con el botón (igual que el gatillo sobre el botón)
            var go = buttons.First(b => b.name == "Destino_P2_Cocina");
            go.onClick.Invoke();
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(m_Menu.IsOpen, "El menú debería cerrarse al elegir un destino");
            Assert.Greater(m_Player.transform.position.y, 2.5f, "No llegó al piso 2");
            m_Menu.Open();
            yield return null;
            Assert.AreEqual(2, m_Menu.CurrentFloorTab, "Debe resaltar el piso donde está el jugador");
            buttons.First(b => b.name == "Boton_Cerrar").onClick.Invoke();
            Assert.IsFalse(m_Menu.IsOpen);
        }

        [UnityTest]
        public IEnumerator Gravedad_ElJugadorCaeHastaElPiso()
        {
            var r = Room("P1_Sala");
            m_Player.TeleportTo(r.spawn.position + Vector3.up * 1.5f, r.look.position, false);   // sin ajustar al piso: debe caer
            yield return new WaitForSeconds(2f);
            Assert.That(m_Player.transform.position.y, Is.EqualTo(r.spawn.position.y).Within(0.08f), "El jugador no quedó sobre el piso");
            Assert.IsTrue(m_Player.IsGrounded, "El jugador no está apoyado en el piso");
        }

        [UnityTest]
        public IEnumerator Paredes_NoSePuedenAtravesar()
        {
            var r = Room("P1_Dormitorio");
            m_Player.TeleportTo(r.spawn.position, r.look.position);
            yield return new WaitForSeconds(0.5f);
            Vector3 origin = m_Player.transform.position + Vector3.up * 1.0f;
            RaycastHit best = default; bool found = false;
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                if (Physics.Raycast(origin, d, out RaycastHit h, 6f) && h.collider.name.StartsWith("Muro_") && (!found || h.distance < best.distance))
                { best = h; found = true; }
            }
            Assert.IsTrue(found, "No se encontró ninguna pared cerca");
            Vector3 dir = -best.normal; dir.y = 0f; dir.Normalize();
            m_Player.TeleportTo(m_Player.transform.position, m_Player.transform.position + dir);
            yield return Walk(new Vector2(0f, 1f), 3.5f);
            Vector3 p = m_Player.transform.position; p.y = best.point.y;
            float along = Vector3.Dot(best.point - p, dir);
            Assert.Greater(along, m_Player.Controller.radius - 0.05f, "El jugador atravesó la pared " + best.collider.name);
        }

        [UnityTest]
        public IEnumerator Escalera_SePuedeSubir()
        {
            var r = Room("Ext_Escalera");
            m_Player.TeleportTo(r.spawn.position, r.look.position);
            yield return new WaitForSeconds(0.5f);
            float y0 = m_Player.transform.position.y;
            yield return Walk(new Vector2(0f, 1f), 5f);
            Assert.Greater(m_Player.transform.position.y - y0, 1.3f, "No subió el primer tramo de la escalera");
        }

        [UnityTest]
        public IEnumerator Teletransporte_PorPisos()
        {
            Assert.Greater(m_Menu.rooms.Count(x => x.floor == 1), 4);
            Assert.Greater(m_Menu.rooms.Count(x => x.floor == 2), 4);
            Assert.Greater(m_Menu.rooms.Count(x => x.floor == 0), 1);
            var r = Room("P2_Dormitorio");
            m_Menu.Open();
            Assert.IsTrue(m_Menu.IsOpen);
            m_Menu.GoTo(r);
            yield return new WaitForSeconds(1.5f);
            Vector3 a = m_Player.head.position, b = r.spawn.position; a.y = b.y = 0f;
            Assert.Less(Vector3.Distance(a, b), 0.35f, "La cabeza no quedó sobre el destino");
            Assert.That(m_Player.transform.position.y, Is.EqualTo(r.spawn.position.y).Within(0.1f));
            Assert.IsFalse(m_Menu.IsOpen, "El menú debería cerrarse al teletransportar");
        }

        [UnityTest]
        public IEnumerator Puertas_AbrenYCierranSobreLaBisagra()
        {
            var r = Room("P1_Sala");
            m_Player.TeleportTo(r.spawn.position, r.look.position);
            yield return null;
            var doors = Object.FindObjectsByType<HingedDoor>(FindObjectsSortMode.None);
            Assert.AreEqual(10, doors.Length);
            var d = doors.First(x => x.name.Contains("P1_Dormitorio"));
            Vector3 pivot = d.transform.position;
            d.Interact();
            yield return new WaitForSeconds(1.2f);
            Assert.That(Mathf.Abs(d.CurrentAngle), Is.EqualTo(90f).Within(1f), "La puerta no abrió 90°");
            Assert.Less(Vector3.Distance(pivot, d.transform.position), 0.001f, "La bisagra se movió");
            d.Interact();
            yield return new WaitForSeconds(1.2f);
            Assert.That(d.CurrentAngle, Is.EqualTo(0f).Within(1f), "La puerta no cerró");
        }

        [UnityTest]
        public IEnumerator Ventanas_SeDeslizan()
        {
            var wins = Object.FindObjectsByType<SlidingWindow>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(wins.Length, 11);
            var w = wins[0];
            Vector3 p0 = w.transform.position;
            w.Interact();
            yield return new WaitForSeconds(2.5f);
            Assert.That(w.OpenAmount, Is.EqualTo(1f).Within(0.01f));
            Assert.Greater(Vector3.Distance(p0, w.transform.position), 0.3f);
            w.Interact();
            yield return new WaitForSeconds(2.5f);
            Assert.That(w.OpenAmount, Is.EqualTo(0f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Interruptores_ControlanSusLuces()
        {
            var switches = Object.FindObjectsByType<LightSwitch>(FindObjectsSortMode.None);
            Assert.Greater(switches.Length, 10);
            foreach (var s in switches)
            {
                Assert.IsNotNull(s.target, s.name + " sin grupo de luces");
                bool before = s.target.isOn;
                s.Interact();
                Assert.AreEqual(!before, s.target.isOn);
                Assert.IsTrue(s.target.lights.All(l => l.enabled == (s.target.isOn && !s.target.IsCulled)), s.name);
                s.Interact();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Grifos_YDucha_DejanCorrerElAgua()
        {
            var faucets = Object.FindObjectsByType<WaterFaucet>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(faucets.Length, 8);
            foreach (var f in faucets) f.SetOpen(true);
            yield return new WaitForSeconds(1f);
            foreach (var f in faucets)
            {
                Assert.IsTrue(f.IsWaterRunning, f.name + " no tiene agua corriendo");
                Assert.IsTrue(f.GetComponentsInChildren<ParticleSystem>().Any(p => p.isPlaying), f.name + " sin partículas");
            }
            foreach (var f in faucets) f.SetOpen(false);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(faucets.All(f => !f.IsWaterRunning));
        }
    }
}
