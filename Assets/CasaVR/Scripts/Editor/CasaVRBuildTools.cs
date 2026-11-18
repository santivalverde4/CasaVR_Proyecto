using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace CasaVR.EditorTools
{
    /// <summary>Validación de la escena/proyecto, compilación del APK y utilidades.</summary>
    public static class CasaVRBuildTools
    {
        [MenuItem("CasaVR/3. Validar escena y proyecto", priority = 3)]
        public static void ValidateFromMenu()
        {
            var sb = new StringBuilder();
            int errors = Validate(sb);
            Debug.Log("[CasaVR] Validación:\n" + sb);
            EditorUtility.DisplayDialog("CasaVR - Validación", (errors == 0 ? "Todo correcto.\n\n" : errors + " problema(s):\n\n") + sb, "OK");
        }

        public static int Validate(StringBuilder sb)
        {
            int errors = 0;
            void Ok(string m) => sb.AppendLine("✔ " + m);
            void Bad(string m) { sb.AppendLine("✘ " + m); errors++; }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != CasaVRSceneBuilder.ScenePath)
                Bad("La escena activa no es " + CasaVRSceneBuilder.ScenePath + " (ábrela o ejecútalo después de construirla)");

            var player = Object.FindFirstObjectByType<CasaVRPlayer>();
            if (player == null) Bad("No hay jugador (CasaVRPlayer)");
            else
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc.stepOffset < 0.2f) Bad("stepOffset del CharacterController muy bajo para escaleras");
                else Ok($"Jugador con CharacterController (altura {cc.height:0.00} m, paso {cc.stepOffset:0.00} m, pendiente {cc.slopeLimit:0}°) y gravedad {player.gravity} m/s²");
                if (!Physics.Raycast(player.transform.position + Vector3.up, Vector3.down, 3f))
                    Bad("No hay piso con colisión bajo el punto de inicio del jugador");
                else Ok("Hay piso con colisión bajo el punto de inicio");
                // altura: una sola fuente (Camera Offset), cámara en 0, origen sin escalar, cápsula < vano de puertas
                if (player.transform.lossyScale != Vector3.one) Bad("El XR Origin está escalado (debe ser 1, 1, 1)");
                else if (!EditorApplication.isPlaying && player.head != null && Mathf.Abs(player.head.localPosition.y) > 0.01f)
                    Bad($"La cámara tiene altura propia ({player.head.localPosition.y:0.00} m) además del Camera Offset: la vista quedaría duplicada");
                else if (player.maxBodyHeight > 2.0f) Bad($"La cápsula puede medir {player.maxBodyHeight:0.00} m: no cabría por las puertas (2.07 m)");
                else Ok($"Altura: vista {player.standingEyeHeight:0.00} m (simulador/Device) u offset 0 con Quest (Floor); cápsula máx. {player.maxBodyHeight:0.00} m");
            }
            int walls = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Count(c => c.name.StartsWith("Muro_"));
            if (walls < 20) Bad("Pocas paredes con MeshCollider: " + walls); else Ok("Paredes sólidas: " + walls);
            int floors = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Count(c => c.name.StartsWith("Losa_") || c.name.StartsWith("Escalera_Descanso") || c.name.StartsWith("Pasillo_P2"));
            if (floors < 4) Bad("Faltan pisos/losas con colisión"); else Ok("Pisos y descansos con colisión: " + floors);
            int ramps = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Count(c => c.name.StartsWith("COL_Rampa"));
            if (ramps < 2) Bad("Faltan las rampas de colisión de la escalera"); else Ok("Rampas de escalera: " + ramps);

            var doors = Object.FindObjectsByType<HingedDoor>(FindObjectsSortMode.None);
            int badDoors = doors.Count(d => d.GetComponent<BoxCollider>() == null || d.GetComponent<XRBaseInteractable>() == null || d.GetComponent<Rigidbody>() == null);
            if (doors.Length != 10 || badDoors > 0) Bad($"Puertas: {doors.Length} (esperadas 10), incompletas: {badDoors}"); else Ok("10 puertas con bisagra, colisión e interacción XR");
            var wins = Object.FindObjectsByType<SlidingWindow>(FindObjectsSortMode.None);
            if (wins.Length < 11) Bad("Ventanas corredizas: " + wins.Length + " (esperadas 11)"); else Ok("Ventanas corredizas: " + wins.Length);
            var groups = Object.FindObjectsByType<RoomLights>(FindObjectsSortMode.None);
            var switches = Object.FindObjectsByType<LightSwitch>(FindObjectsSortMode.None);
            var orphan = groups.Where(g => !switches.Any(s => s.target == g)).Select(g => g.groupId).ToList();
            if (orphan.Count > 0) Bad("Grupos de luz sin interruptor: " + string.Join(", ", orphan));
            else Ok($"{switches.Length} interruptores controlan {groups.Length} grupos de luces ({groups.Sum(g => g.lights.Count)} luces)");
            var faucets = Object.FindObjectsByType<WaterFaucet>(FindObjectsSortMode.None);
            if (faucets.Length < 8) Bad("Grifos/duchas: " + faucets.Length + " (esperados 8)");
            else Ok($"Grifos y duchas con agua: {faucets.Length} ({faucets.Count(f => f.kind == WaterFaucet.Kind.Ducha)} duchas)");
            var menu = Object.FindFirstObjectByType<TeleportMenu>();
            if (menu == null || menu.rooms.Count < 10) Bad("Menú de teletransporte incompleto");
            else Ok($"Menú de teletransporte: piso 1 ({menu.rooms.Count(r => r.floor == 1)}), piso 2 ({menu.rooms.Count(r => r.floor == 2)}), exterior ({menu.rooms.Count(r => r.floor == 0)})");
            if (menu != null && menu.rooms.Any(r => r.spawn == null)) Bad("Hay destinos del menú sin punto de aparición");
            if (menu != null && (menu.rounded == null || menu.icons.Count < 12)) Bad("Faltan los sprites del menú (Assets/CasaVR/Textures/UI); vuelve a construir la escena");
            else if (menu != null) Ok($"Menú: tres categorías con {menu.icons.Count} íconos");

            // proyecto
            if (PlayerSettings.colorSpace != ColorSpace.Linear) Bad("Color space no es Linear");
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0) Bad("Android no compila ARM64");
            else Ok("Android ARM64");
            var per = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            bool loader = per != null && per.Manager != null && per.Manager.activeLoaders.Any(l => l != null && l.GetType().Name == "OpenXRLoader");
            if (!loader) Bad("OpenXR no está activo para Android (CasaVR > 1. Configurar proyecto)"); else Ok("OpenXR activo para Android");
            var ox = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            bool quest = ox != null && ox.GetFeatures().Any(f => f != null && f.enabled && f.GetType().Name == "MetaQuestFeature");
            bool touch = ox != null && ox.GetFeatures().Any(f => f != null && f.enabled && f.GetType().Name.Contains("Touch"));
            if (!quest) Bad("Falta activar 'Meta Quest Support' en OpenXR (Android)"); else Ok("Meta Quest Support activo");
            if (!touch) Bad("Falta un perfil de control Touch en OpenXR (Android)"); else Ok("Perfil de controles Touch activo");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                sb.AppendLine("• Aviso: la plataforma activa no es Android (necesario solo para compilar el APK)");
            return errors;
        }

        [MenuItem("CasaVR/4. Compilar APK para Meta Quest", priority = 4)]
        public static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUtility.DisplayDialog("CasaVR", "La plataforma activa no es Android. ¿Cambiar ahora?", "Cambiar", "Cancelar")) return;
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
            Directory.CreateDirectory("Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { CasaVRSceneBuilder.ScenePath },
                locationPathName = "Builds/CasaVR.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            string msg = s.result == BuildResult.Succeeded
                ? $"APK listo: {Path.GetFullPath(opts.locationPathName)}\nTamaño: {s.totalSize / (1024f * 1024f):0.0} MB\n\nInstálalo con Meta Quest Developer Hub o:\nadb install -r Builds/CasaVR.apk"
                : $"La compilación terminó con estado {s.result} ({s.totalErrors} errores). Revisa la consola.";
            Debug.Log("[CasaVR] " + msg);
            EditorUtility.DisplayDialog("CasaVR", msg, "OK");
        }

        [MenuItem("CasaVR/Opciones/Activar o desactivar el simulador XR en la escena", priority = 20)]
        public static void ToggleSimulator()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("CasaVR", "Detén Play y vuelve a usar esta opción: el simulador se enciende o apaga al iniciar Play.", "OK");
                return;
            }
            var gates = Object.FindObjectsByType<SimulatorGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool now = gates.Length > 0 ? !gates[0].enableSimulator : !EditorPrefs.GetBool("CasaVR_SimuladorActivo", true);
            EditorPrefs.SetBool("CasaVR_SimuladorActivo", now);
            foreach (var gate in gates)
            {
                gate.enableSimulator = now;
                EditorUtility.SetDirty(gate);
                EditorSceneManager.MarkSceneDirty(gate.gameObject.scene);
            }
            if (gates.Length == 0)
                Debug.Log("[CasaVR] No hay escena de la casa abierta: el cambio se aplicará al construir la escena.");
            Debug.Log("[CasaVR] Simulador XR " + (now ? "activado" : "desactivado (se usará el modo escritorio sin visor)"));
        }

        [MenuItem("CasaVR/Opciones/Hornear iluminación (opcional, mejora la calidad)", priority = 21)]
        public static void BakeLighting()
        {
            if (!Lightmapping.TryGetLightingSettings(out LightingSettings ls) || ls == null)
            {
                Debug.LogWarning("[CasaVR] Construye primero la escena (CasaVR > 2).");
                return;
            }
            if (!EditorUtility.DisplayDialog("CasaVR", "Se activará iluminación indirecta horneada (Baked Indirect) y se generarán lightmaps.\n" +
                    "Las luces de las habitaciones siguen siendo en tiempo real para que los interruptores funcionen.\nPuede tardar varios minutos.", "Hornear", "Cancelar"))
                return;
            ls.bakedGI = true;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapResolution = 8f;
            ls.lightmapMaxSize = 1024;
            var importer = AssetImporter.GetAtPath(CasaVRSceneBuilder.ModelPath) as ModelImporter;
            if (importer != null && !importer.generateSecondaryUV)
            {
                importer.generateSecondaryUV = true;
                importer.SaveAndReimport();
            }
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.lightmapBakeType = LightmapBakeType.Mixed;
                EditorUtility.SetDirty(sun);
                EditorSceneManager.MarkSceneDirty(sun.gameObject.scene);
            }
            Lightmapping.BakeAsync();
        }
    }
}
