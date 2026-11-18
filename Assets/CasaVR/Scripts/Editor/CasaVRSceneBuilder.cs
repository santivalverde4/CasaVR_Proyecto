using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CasaVR.EditorTools
{
    /// <summary>
    /// Menú CasaVR > 2. Construir escena: crea materiales URP desde la tabla, instancia el modelo de Blender,
    /// agrega colisiones (paredes, pisos, muebles, rampas de escalera), puertas, ventanas, interruptores,
    /// luces por habitación, grifos con agua, el jugador XR con gravedad, el menú de teletransporte y el
    /// simulador XR. Se puede volver a ejecutar en cualquier momento (regenera la escena).
    /// </summary>
    public static class CasaVRSceneBuilder
    {
        public const string Root = "Assets/CasaVR";
        public const string ModelPath = Root + "/Models/CasaVR_Casa.fbx";
        public const string ScenePath = Root + "/Scenes/CasaVR.unity";
        public const string InputPath = Root + "/Input/CasaVR_Controles.inputactions";
        const string StartSpawn = "Ext_Entrada";

        static readonly Dictionary<string, string> k_RoomNames = new Dictionary<string, string>
        {
            { "Sala", "Sala" }, { "Cocina", "Cocina y comedor" }, { "Dormitorio", "Dormitorio" }, { "Bano", "Baño" },
            { "Lavanderia", "Lavandería" }, { "Patio", "Patio" }, { "Balcon", "Balcón techado" }, { "Pasillo", "Pasillo exterior" },
            { "Entrada", "Entrada principal" }, { "Escalera", "Escalera" }, { "JardinFrontal", "Jardín frontal" },
            { "JardinTrasero", "Jardín trasero" },
        };

        static readonly Dictionary<string, string> k_GroupNames = new Dictionary<string, string>
        {
            { "SalaCocina", "Sala y cocina" }, { "Dormitorio", "Dormitorio" }, { "Bano", "Baño" }, { "Lavanderia", "Lavandería" },
            { "Patio", "Patio" }, { "Balcon", "Balcón" }, { "Porche", "Porche" }, { "Pasillo", "Pasillo" }, { "Escalera", "Escalera" },
        };

        class Ctx
        {
            public Dictionary<string, Material> mats = new Dictionary<string, Material>();
            public Dictionary<string, RoomLights> groups = new Dictionary<string, RoomLights>();
            public Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
            public Dictionary<string, InputActionReference> refs = new Dictionary<string, InputActionReference>();
            public InputActionAsset actions;
            public Transform lightsRoot;
            public List<RoomEntry> rooms = new List<RoomEntry>();
            public StringBuilder log = new StringBuilder();
            public int doors, windows, switches, faucets, lights, meshColliders, boxColliders, ramps;
        }

        public static bool IsURP
        {
            get
            {
                var rp = GraphicsSettings.currentRenderPipeline;
                return rp != null && rp.GetType().Name.Contains("Universal");
            }
        }

        [MenuItem("CasaVR/2. Construir escena de la casa", priority = 2)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var ctx = Build();
            if (ctx == null) return;
            Debug.Log("[CasaVR] Escena construida:\n" + ctx.log);
            EditorUtility.DisplayDialog("CasaVR", "Escena creada en " + ScenePath + "\n\n" + ctx.log +
                "\nPulsa Play para probar (con el XR Interaction Simulator o en modo escritorio).", "OK");
        }

        static Ctx Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            {
                EditorUtility.DisplayDialog("CasaVR", "No se encontró el modelo " + ModelPath, "OK");
                return null;
            }
            var ctx = new Ctx();
            try
            {
                EditorUtility.DisplayProgressBar("CasaVR", "Revisando importación del modelo...", 0.05f);
                EnsureImportSettings();
                EditorUtility.DisplayProgressBar("CasaVR", "Materiales...", 0.1f);
                CreateMaterials(ctx);
                RemapModelMaterials(ctx);
                LoadAudio(ctx);
                LoadInput(ctx);

                EditorUtility.DisplayProgressBar("CasaVR", "Escena...", 0.3f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var env = SetupEnvironment(ctx);
                var house = InstantiateHouse();
                RebuildHierarchy(house);
                ctx.lightsRoot = new GameObject("Luces de habitaciones").transform;

                EditorUtility.DisplayProgressBar("CasaVR", "Colisiones e interacciones...", 0.5f);
                ProcessHouse(house, ctx);

                EditorUtility.DisplayProgressBar("CasaVR", "Jugador XR...", 0.8f);
                var player = BuildPlayer(ctx);
                BuildSystems(ctx, player, env);
                AddSimulator(ctx);

                var start = ctx.rooms.Find(r => r.id == StartSpawn) ?? ctx.rooms.FirstOrDefault();
                if (start != null && start.spawn != null)
                {
                    Vector3 dir = (start.look != null ? start.look.position : start.spawn.position + Vector3.forward) - start.spawn.position;
                    dir.y = 0f;
                    player.transform.SetPositionAndRotation(start.spawn.position,
                        dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity);
                }

                Lightmapping.lightingSettings = GetLightingSettings();
                if (!AssetDatabase.IsValidFolder(Root + "/Scenes")) AssetDatabase.CreateFolder(Root, "Scenes");
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuild();
                ctx.log.AppendLine($"• Puertas: {ctx.doors} · Ventanas corredizas: {ctx.windows} · Interruptores: {ctx.switches}");
                ctx.log.AppendLine($"• Luces: {ctx.lights} en {ctx.groups.Count} grupos · Grifos/ducha: {ctx.faucets}");
                ctx.log.AppendLine($"• Colliders: {ctx.meshColliders} de malla, {ctx.boxColliders} de caja, {ctx.ramps} rampas de escalera");
                ctx.log.AppendLine($"• Destinos del menú: {ctx.rooms.Count}");
                ctx.log.AppendLine(IsURP ? "• Render pipeline: URP" : "• Render pipeline: Built-in (materiales Standard)");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return ctx;
        }

        /// <summary>
        /// Si el paquete se importó antes de compilar el AssetPostprocessor, el modelo/texturas quedan con la
        /// configuración por defecto: se reimportan para aplicar escala 1 m, ejes, lectura de malla, etc.
        /// </summary>
        public static void EnsureImportSettings()
        {
            var mi = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (mi != null && (!mi.isReadable || !mi.bakeAxisConversion || mi.addCollider || mi.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription))
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                bool ui = p.Contains("/Textures/UI/");
                bool sliced = ui && (p.Contains("UI_Redondeado") || p.Contains("UI_Brillo"));
                if (ti != null && (ti.maxTextureSize != 1024 || (ui && ti.textureType != TextureImporterType.Sprite) ||
                                   (sliced && ti.spriteBorder == Vector4.zero)))
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
        }

        // =========================================================================== materiales
        static Material LoadOrCreate(string name, Shader shader)
        {
            string dir = Root + "/Materials";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root, "Materials");
            string path = dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        static Shader FindShader(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return Shader.Find("Standard");
        }

        static void CreateMaterials(Ctx ctx)
        {
            bool urp = IsURP;
            Shader lit = urp ? FindShader("Universal Render Pipeline/Lit") : FindShader("Standard");
            foreach (var d in CasaVRMaterialTable.All)
            {
                var m = LoadOrCreate(d.name, lit);
                ApplyDef(m, d, urp);
                EditorUtility.SetDirty(m);
                ctx.mats[d.name] = m;
            }
            // lámpara apagada
            var on = ctx.mats["M_Lampara_Difusor"];
            var off = LoadOrCreate("M_Lampara_Difusor_Apagada", lit);
            var dOff = CasaVRMaterialTable.All.First(x => x.name == "M_Lampara_Difusor");
            dOff.emission = Color.black;
            dOff.color = new Color(0.86f, 0.85f, 0.82f);
            ApplyDef(off, dOff, urp);
            ctx.mats[off.name] = off;
            // agua
            Shader unlit = urp ? FindShader("Universal Render Pipeline/Unlit") : FindShader("Unlit/Transparent");
            var chorro = LoadOrCreate("M_Agua_Chorro", unlit);
            SetupUnlitTransparent(chorro, Tex("T_Agua_Chorro"), new Color(0.82f, 0.92f, 1f, 0.6f), urp);
            ctx.mats[chorro.name] = chorro;
            Shader part = urp ? FindShader("Universal Render Pipeline/Particles/Unlit") : FindShader("Particles/Standard Unlit", "Legacy Shaders/Particles/Alpha Blended");
            var gotas = LoadOrCreate("M_Agua_Gotas", part);
            SetupUnlitTransparent(gotas, Tex("T_Gota"), new Color(0.85f, 0.93f, 1f, 0.75f), urp);
            ctx.mats[gotas.name] = gotas;
            // el rayo se colorea por color de vértice (gradientes de XRInteractorLineVisual)
            var rayo = LoadOrCreate("M_Rayo", urp ? FindShader("Universal Render Pipeline/Particles/Unlit") : FindShader("Sprites/Default"));
            SetupUnlitTransparent(rayo, null, Color.white, urp);
            ctx.mats[rayo.name] = rayo;
            var cielo = LoadOrCreate("M_Cielo", FindShader("Skybox/Procedural"));
            if (cielo.HasProperty("_SunSize")) cielo.SetFloat("_SunSize", 0.035f);
            if (cielo.HasProperty("_AtmosphereThickness")) cielo.SetFloat("_AtmosphereThickness", 0.9f);
            if (cielo.HasProperty("_SkyTint")) cielo.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.75f));
            if (cielo.HasProperty("_GroundColor")) cielo.SetColor("_GroundColor", new Color(0.32f, 0.36f, 0.28f));
            if (cielo.HasProperty("_Exposure")) cielo.SetFloat("_Exposure", 1.2f);
            ctx.mats[cielo.name] = cielo;
            AssetDatabase.SaveAssets();
        }

        static Texture2D Tex(string n) => n == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + n + ".png");

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static void ApplyDef(Material m, MatDef d, bool urp)
        {
            var tex = Tex(d.texture);
            float tiling = 1f / Mathf.Max(0.05f, d.textureMeters);
            var scale = new Vector2(tiling, tiling);
            Color c = d.color; c.a = d.alpha;
            if (urp)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", scale);
                m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", d.smoothness);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", d.metallic);
                if (m.HasProperty("_WorkflowMode")) m.SetFloat("_WorkflowMode", 1f);
            }
            else
            {
                m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_MainTex", scale);
                m.SetColor("_Color", c);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", d.smoothness);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", d.metallic);
            }
            if (d.alpha < 0.999f) MakeTransparent(m, urp);
            if (d.emission.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", d.emission * 2.4f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            m.enableInstancing = true;
        }

        static void MakeTransparent(Material m, bool urp)
        {
            if (urp)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                if (m.HasProperty("_BlendModePreserveSpecular")) m.SetFloat("_BlendModePreserveSpecular", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Transparent");
            }
            else
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Transparent");
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        static void SetupUnlitTransparent(Material m, Texture2D tex, Color c, bool urp)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            SetColor(m, c);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
            if (urp) MakeTransparent(m, true);
            else
            {
                m.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        static void RemapModelMaterials(Ctx ctx)
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) return;
            var existing = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (var kv in ctx.mats)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                if (existing.TryGetValue(id, out var cur) && cur == kv.Value) continue;
                importer.AddRemap(id, kv.Value);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        static void LoadAudio(Ctx ctx)
        {
            foreach (var n in new[] { "CasaVR_Agua_Grifo", "CasaVR_Agua_Ducha", "CasaVR_Click", "CasaVR_Puerta_Abrir", "CasaVR_Puerta_Cerrar", "CasaVR_Ventana" })
                ctx.clips[n] = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + n + ".wav");
        }

        static void LoadInput(Ctx ctx)
        {
            ctx.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(InputPath))
            {
                if (o is InputActionReference r && (r.hideFlags & HideFlags.HideInHierarchy) == 0 && r.action != null)
                    ctx.refs[r.action.actionMap.name + "/" + r.action.name] = r;
            }
            if (ctx.refs.Count == 0) ctx.log.AppendLine("• ATENCIÓN: no se cargaron las acciones de entrada");
        }

        static InputActionReference Ref(Ctx ctx, string path)
        {
            ctx.refs.TryGetValue(path, out var r);
            if (r == null) Debug.LogWarning("[CasaVR] Acción no encontrada: " + path);
            return r;
        }

        static LightingSettings GetLightingSettings()
        {
            string path = Root + "/Scenes/CasaVR_Iluminacion.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls == null)
            {
                ls = new LightingSettings { name = "CasaVR_Iluminacion" };
                ls.bakedGI = false;
                ls.realtimeGI = false;
                if (!AssetDatabase.IsValidFolder(Root + "/Scenes")) AssetDatabase.CreateFolder(Root, "Scenes");
                AssetDatabase.CreateAsset(ls, path);
            }
            return ls;
        }

        static void AddSceneToBuild()
        {
            var list = EditorBuildSettings.scenes.ToList();
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // =========================================================================== entorno
        static DayNightController SetupEnvironment(Ctx ctx)
        {
            var sunGO = new GameObject("Sol");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            sunGO.transform.rotation = Quaternion.Euler(50f, -150f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = ctx.mats["M_Cielo"];
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            var dn = sunGO.AddComponent<DayNightController>();
            dn.sun = sun;
            dn.skybox = ctx.mats["M_Cielo"];
            dn.Apply();
            return dn;
        }

        static GameObject InstantiateHouse()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Casa";
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go;
        }

        // =========================================================================== jerarquía
        // El FBX se exporta con jerarquía plana (garantiza posiciones exactas). Aquí se vuelven a emparentar
        // las partes por nombre (Puerta_X_Hoja -> Puerta_X, Grifo_X_Manija -> Grifo_X, ...) y se agrupan por tipo.
        static readonly string[] k_PartSuffixes =
        {
            "_HojaAbierta", "_Hoja", "_Marco", "_Abre", "_MolduraInt", "_MolduraExt", "_Placa", "_Tecla", "_Frente",
            "_Cuerpo", "_Manija", "_Salida", "_Fondo", "_EjeManija", "_Mira",
        };

        static readonly (string group, string[] prefixes)[] k_Groups =
        {
            ("Arquitectura", new[] { "Muro_", "Losa_", "Cubierta", "Columna_", "Celosia_", "VentanaFija", "Moldura_" }),
            ("Escalera", new[] { "Escalera_", "Pasillo_P2", "Baranda" }),
            ("Puertas", new[] { "Puerta_" }),
            ("Ventanas", new[] { "Ventana_" }),
            ("Pisos y umbrales", new[] { "Piso_", "Umbral_" }),
            ("Muebles piso 1", new[] { "Mueble_P1_" }),
            ("Muebles piso 2", new[] { "Mueble_P2_" }),
            ("Lamparas", new[] { "Lampara_", "Luz_" }),
            ("Interruptores", new[] { "Interruptor_" }),
            ("Grifos y ducha", new[] { "Grifo_" }),
            ("Exterior", new[] { "Terreno", "Camino_", "Seto_", "Arbol_", "Arbustos" }),
            ("Colisiones invisibles", new[] { "COL_" }),
            ("Puntos de teletransporte", new[] { "Spawn_" }),
        };

        static void RebuildHierarchy(GameObject house)
        {
            var all = house.GetComponentsInChildren<Transform>(true).Where(t => t != house.transform).ToList();
            var byName = new Dictionary<string, Transform>();
            foreach (var t in all)
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            foreach (var t in all)
            {
                foreach (var suf in k_PartSuffixes)
                {
                    if (!t.name.EndsWith(suf)) continue;
                    string parentName = t.name.Substring(0, t.name.Length - suf.Length);
                    if (byName.TryGetValue(parentName, out var p) && p != t)
                        t.SetParent(p, true);
                    break;
                }
            }
            var containers = new Dictionary<string, Transform>();
            foreach (var t in all)
            {
                if (t.parent != house.transform) continue;
                foreach (var (group, prefixes) in k_Groups)
                {
                    if (!prefixes.Any(t.name.StartsWith)) continue;
                    if (!containers.TryGetValue(group, out var c))
                    {
                        c = new GameObject(group).transform;
                        c.SetParent(house.transform, false);
                        containers[group] = c;
                    }
                    t.SetParent(c, true);
                    break;
                }
            }
        }

        // =========================================================================== casa
        static bool Starts(string n, params string[] p) => p.Any(n.StartsWith);

        static void ProcessHouse(GameObject house, Ctx ctx)
        {
            var all = house.GetComponentsInChildren<Transform>(true).ToList();
            // 1) luces primero (los interruptores las necesitan)
            foreach (var t in all)
                if (t.name.StartsWith("Luz_")) SetupLight(t, all, ctx);
            foreach (var t in all)
            {
                string n = t.name;
                if (n.StartsWith("Puerta_") && t.Find(n + "_Hoja") != null) SetupDoor(t, ctx);
                else if (n.StartsWith("Ventana_") && t.Find(n + "_Hoja") != null) SetupWindow(t, ctx);
                else if (n.StartsWith("Interruptor_") && t.Find(n + "_Placa") != null) SetupSwitch(t, ctx);
                else if (n.StartsWith("Grifo_") && t.Find(n + "_Manija") != null) SetupFaucet(t, ctx);
                else if (n.StartsWith("Spawn_") && !n.EndsWith("_Mira")) SetupSpawn(t, ctx);
            }
            // 2) colisiones y banderas estáticas
            foreach (var t in all)
            {
                if (t == null) continue;
                string n = t.name;
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                bool dynamicPart = n.EndsWith("_Hoja") || n.EndsWith("_Tecla") || n.EndsWith("_Manija");
                if (!dynamicPart)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                        StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
                if (n.StartsWith("COL_"))
                {
                    var mc = t.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    var r = t.GetComponent<MeshRenderer>();
                    if (r != null) Object.DestroyImmediate(r);
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    t.gameObject.isStatic = true;
                    if (n.StartsWith("COL_Rampa")) ctx.ramps++; else ctx.meshColliders++;
                    continue;
                }
                if (dynamicPart || Starts(n, "Escalera_Tramo", "Piso_", "Umbral_", "Lampara_", "Camino_", "Arbustos", "Grifo_", "Interruptor_")
                    || n.EndsWith("_Copa") || n.Contains("Alfombra") || n.Contains("Tapete") || n.Contains("Espejo") || n.Contains("Salpicadero")
                    || n.Contains("_TV_") || n.Contains("Toallero"))
                    continue;
                if (Starts(n, "Muro_", "Losa_", "Cubierta", "Columna_", "Celosia_", "Escalera_Descanso", "Pasillo_P2", "Moldura",
                           "VentanaFija", "Terreno", "Seto", "Baranda") || n.EndsWith("_Marco") || n.Contains("_Moldura") || n.Contains("Mampara"))
                {
                    var mc = t.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    ctx.meshColliders++;
                }
                else if (n.Contains("LamparaPie"))
                {
                    // lámpara de pie: cilindro delgado en vez de la caja de la pantalla
                    var b = mf.sharedMesh.bounds;
                    var cap = t.gameObject.AddComponent<CapsuleCollider>();
                    cap.center = b.center;
                    cap.height = b.size.y;
                    cap.radius = 0.16f;
                    cap.direction = 1;
                }
                else if (n.EndsWith("_Tronco"))
                {
                    var b = mf.sharedMesh.bounds;
                    var cap = t.gameObject.AddComponent<CapsuleCollider>();
                    cap.center = b.center;
                    cap.height = b.size.y;
                    cap.radius = Mathf.Max(0.12f, Mathf.Min(b.size.x, b.size.z) * 0.25f);
                    cap.direction = 1;
                }
                else if (n.StartsWith("Mueble_"))
                {
                    var b = mf.sharedMesh.bounds;
                    var bc = t.gameObject.AddComponent<BoxCollider>();
                    bc.center = b.center;
                    bc.size = b.size;
                    ctx.boxColliders++;
                }
            }
        }

        static Bounds RendererBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.05f);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>BoxCollider en el espacio local de 'go' que envuelve las mallas de 'parts' (aunque estén rotadas).</summary>
        static void FitBox(GameObject go, IEnumerable<Transform> parts, float pad)
        {
            bool any = false;
            Bounds local = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var p in parts)
            {
                if (p == null) continue;
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                        var l = go.transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                        if (!any) { local = new Bounds(l, Vector3.zero); any = true; }
                        else local.Encapsulate(l);
                    }
                }
            }
            var bc = go.AddComponent<BoxCollider>();
            if (!any) { bc.size = Vector3.one * 0.1f; return; }
            bc.center = local.center;
            bc.size = local.size + Vector3.one * pad;
        }

        static XRSimpleInteractable AddInteractable(GameObject go)
        {
            var xi = go.GetComponent<XRSimpleInteractable>();
            if (xi == null) xi = go.AddComponent<XRSimpleInteractable>();
            // Multiple: si la otra mano también selecciona, no se cancela (ni se "clica") la primera
            xi.selectMode = InteractableSelectMode.Multiple;
            return xi;
        }

        static void SetupDoor(Transform root, Ctx ctx)
        {
            string n = root.name;
            var leaf = root.Find(n + "_Hoja");
            var mark = root.Find(n + "_Abre");
            var mf = leaf.GetComponent<MeshFilter>();
            var rb = leaf.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var bc = leaf.gameObject.AddComponent<BoxCollider>();
            if (mf != null && mf.sharedMesh != null)
            {
                var b = mf.sharedMesh.bounds;
                bc.center = b.center;
                bc.size = b.size;
            }
            var door = leaf.gameObject.AddComponent<HingedDoor>();
            door.xrInteractable = AddInteractable(leaf.gameObject);
            Vector3 hinge = leaf.position;
            Vector3 closed = RendererBounds(leaf).center - hinge; closed.y = 0f;
            Vector3 open = (mark != null ? mark.position : hinge + leaf.forward) - hinge; open.y = 0f;
            float sign = Mathf.Sign(Vector3.SignedAngle(closed, open, Vector3.up));
            door.openAngle = 90f * (sign == 0f ? 1f : sign);
            door.openClip = ctx.clips["CasaVR_Puerta_Abrir"];
            door.closeClip = ctx.clips["CasaVR_Puerta_Cerrar"];
            ctx.doors++;
        }

        static void SetupWindow(Transform root, Ctx ctx)
        {
            string n = root.name;
            var sash = root.Find(n + "_Hoja");
            var target = root.Find(n + "_HojaAbierta");
            if (target == null) return;
            var rb = sash.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var mf = sash.GetComponent<MeshFilter>();
            var bc = sash.gameObject.AddComponent<BoxCollider>();
            if (mf != null && mf.sharedMesh != null)
            {
                var b = mf.sharedMesh.bounds;
                bc.center = b.center;
                bc.size = b.size + new Vector3(0.01f, 0.01f, 0.01f);
            }
            var w = sash.gameObject.AddComponent<SlidingWindow>();
            w.xrInteractable = AddInteractable(sash.gameObject);
            w.openLocalOffset = sash.parent.InverseTransformVector(target.position - sash.position);
            w.slideClip = ctx.clips["CasaVR_Ventana"];
            ctx.windows++;
        }

        static string GroupOfLight(string n)
        {
            // Luz_<grupo>_<k>
            string s = n.Substring(4);
            int i = s.LastIndexOf('_');
            return i > 0 ? s.Substring(0, i) : s;
        }

        static RoomLights Group(string id, Ctx ctx)
        {
            if (ctx.groups.TryGetValue(id, out var g)) return g;
            var go = new GameObject("Luces_" + id);
            go.transform.SetParent(ctx.lightsRoot, false);
            g = go.AddComponent<RoomLights>();
            g.groupId = id;
            string key = id.Contains("_") ? id.Substring(id.IndexOf('_') + 1) : id;
            string floor = id.StartsWith("P1_") ? " (piso 1)" : id.StartsWith("P2_") ? " (piso 2)" : "";
            g.displayName = (k_GroupNames.TryGetValue(key, out var dn) ? dn : key) + floor;
            g.floor = id.StartsWith("P1_") && !id.Contains("Porche") ? 1 : (id.StartsWith("P2_") && !id.Contains("Pasillo") ? 2 : 0);
            g.onMaterial = ctx.mats["M_Lampara_Difusor"];
            g.offMaterial = ctx.mats["M_Lampara_Difusor_Apagada"];
            g.isOn = true;
            ctx.groups[id] = g;
            return g;
        }

        static void SetupLight(Transform t, List<Transform> all, Ctx ctx)
        {
            string grp = GroupOfLight(t.name);
            var g = Group(grp, ctx);
            var l = t.gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            bool exterior = grp.Contains("Porche") || grp.Contains("Pasillo") || grp.Contains("Escalera") || grp.Contains("Patio") || grp.Contains("Balcon");
            l.range = exterior ? 5f : 5.5f;
            l.intensity = exterior ? 1.6f : 2.2f;
            l.color = new Color(1f, 0.89f, 0.76f);
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            l.renderMode = LightRenderMode.Auto;
            g.lights.Add(l);
            string fixture = "Lampara_" + t.name.Substring(4);
            var f = all.Find(x => x != null && x.name == fixture);
            if (f != null)
            {
                var r = f.GetComponent<Renderer>();
                if (r != null) g.fixtures.Add(r);
            }
            ctx.lights++;
        }

        static void SetupSwitch(Transform root, Ctx ctx)
        {
            string n = root.name;
            string grp = n.Substring("Interruptor_".Length);
            var plate = root.Find(n + "_Placa");
            var key = root.Find(n + "_Tecla");
            var front = root.Find(n + "_Frente");
            FitBox(root.gameObject, new[] { plate, key }, 0.025f);
            var s = root.gameObject.AddComponent<LightSwitch>();
            s.xrInteractable = AddInteractable(root.gameObject);
            s.target = Group(grp, ctx);
            s.rocker = key;
            if (front != null && key != null)
            {
                Vector3 normal = front.position - root.position; normal.y = 0f;
                Vector3 axis = Vector3.Cross(Vector3.up, normal.normalized);
                s.rockerAxisLocal = key.InverseTransformDirection(axis).normalized;
            }
            s.clickClip = ctx.clips["CasaVR_Click"];
            ctx.switches++;
        }

        static void SetupFaucet(Transform root, Ctx ctx)
        {
            string n = root.name;
            var handle = root.Find(n + "_Manija");
            var outlet = root.Find(n + "_Salida");
            var bottom = root.Find(n + "_Fondo");
            var axisMark = root.Find(n + "_EjeManija");
            bool shower = n.EndsWith("Ducha");
            var body = root.Find(n + "_Cuerpo");
            FitBox(root.gameObject, shower ? new[] { handle } : new[] { handle, body }, shower ? 0.08f : 0.04f);
            var f = root.gameObject.AddComponent<WaterFaucet>();
            f.xrInteractable = AddInteractable(root.gameObject);
            f.kind = shower ? WaterFaucet.Kind.Ducha : WaterFaucet.Kind.Grifo;
            f.handle = handle;
            f.outlet = outlet;
            f.bottom = bottom;
            Vector3 axis = axisMark != null ? (axisMark.position - handle.position).normalized : Vector3.up;
            f.handleAxisLocal = handle.InverseTransformDirection(axis).normalized;
            float angle = n.EndsWith("Pila") ? 120f : (shower ? 90f : 40f);
            if (!shower && !n.EndsWith("Pila"))
            {
                // la palanca debe subir al abrir
                Vector3 tip = RendererBounds(handle).center - handle.position;
                Vector3 rotated = Quaternion.AngleAxis(angle, axis) * tip;
                if (rotated.y < tip.y) angle = -angle;
            }
            f.openAngle = angle;
            f.streamMaterial = ctx.mats["M_Agua_Chorro"];
            f.dropMaterial = ctx.mats["M_Agua_Gotas"];
            f.waterLoop = ctx.clips[shower ? "CasaVR_Agua_Ducha" : "CasaVR_Agua_Grifo"];
            f.clickClip = ctx.clips["CasaVR_Click"];
            ctx.faucets++;
        }

        static void SetupSpawn(Transform t, Ctx ctx)
        {
            string id = t.name.Substring("Spawn_".Length);
            int floor = id.StartsWith("P1_") ? 1 : id.StartsWith("P2_") ? 2 : 0;
            string key = id.Contains("_") ? id.Substring(id.IndexOf('_') + 1) : id;
            ctx.rooms.Add(new RoomEntry
            {
                id = id,
                displayName = k_RoomNames.TryGetValue(key, out var dn) ? dn : key,
                floor = floor,
                spawn = t,
                look = t.Find(t.name + "_Mira"),
            });
        }

        // =========================================================================== jugador
        static CasaVRPlayer BuildPlayer(Ctx ctx)
        {
            var rig = new GameObject("XR Origin (CasaVR)");
            rig.layer = 2;   // "Ignore Raycast": los rayos de los controles no chocan con el propio cuerpo
            var cc = rig.AddComponent<CharacterController>();
            cc.height = 1.8f;      // se ajusta a la altura de la cabeza, máx. 1.85 m (vano de puertas 2.07 m)
            cc.radius = 0.2f;      // cápsula delgada (hombros ~0.45 m): pasa por puertas y entre muebles
            cc.skinWidth = 0.02f;  // ~10 % del radio, como recomienda Unity
            cc.stepOffset = 0.32f;
            cc.slopeLimit = 50f;
            cc.minMoveDistance = 0f;
            cc.center = new Vector3(0f, cc.height * 0.5f + cc.skinWidth, 0f);

            var origin = rig.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(rig.transform, false);
            // Una sola fuente de altura: el Camera Offset (1.70 m) y la cámara en 0 dentro de él.
            // En Meta Quest (modo Floor) CasaVRPlayer pone el offset en 0 porque el visor ya entrega la
            // altura real de la cabeza; con el simulador XR o en modo Device se queda en 1.70 m.
            offset.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(offset.transform, false);
            camGO.transform.localPosition = Vector3.zero;
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGO.AddComponent<AudioListener>();
            var tpd = camGO.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(Ref(ctx, "CasaVR Cabeza/Position"));
            tpd.rotationInput = new InputActionProperty(Ref(ctx, "CasaVR Cabeza/Rotation"));
            tpd.trackingStateInput = new InputActionProperty(Ref(ctx, "CasaVR Cabeza/Tracking State"));
            origin.Origin = rig;
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.7f;

            var left = BuildController("Control Izquierdo", "CasaVR Izquierda", offset.transform, ctx);
            var right = BuildController("Control Derecho", "CasaVR Derecha", offset.transform, ctx);

            var iam = rig.AddComponent<InputActionManager>();
            iam.actionAssets = new List<InputActionAsset> { ctx.actions };

            var player = rig.AddComponent<CasaVRPlayer>();
            var startEntry = ctx.rooms.Find(r => r.id == StartSpawn);
            if (startEntry != null) { player.startSpawn = startEntry.spawn; player.startLook = startEntry.look; }
            player.origin = origin;
            player.cameraOffset = offset.transform;
            player.head = cam.transform;
            player.xrOnlyObjects = new[] { left, right };
            player.moveInput = Ref(ctx, "CasaVR Izquierda/Thumbstick");
            player.turnInput = Ref(ctx, "CasaVR Derecha/Thumbstick");
            player.desktopMove = Ref(ctx, "CasaVR Escritorio/Move");
            player.desktopLook = Ref(ctx, "CasaVR Escritorio/Look");
            player.desktopRun = Ref(ctx, "CasaVR Escritorio/Run");
            player.desktopReleaseCursor = Ref(ctx, "CasaVR Escritorio/Release Cursor");

            var fadeGO = new GameObject("Fundido");
            fadeGO.transform.SetParent(cam.transform, false);
            var fader = fadeGO.AddComponent<ScreenFader>();
            fader.targetCamera = cam;

            var di = camGO.AddComponent<DesktopInteractor>();
            di.player = player;
            di.cam = cam;
            di.interactAction = Ref(ctx, "CasaVR Escritorio/Interact");
            return player;
        }

        static GameObject BuildController(string name, string map, Transform parent, Ctx ctx)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tpd = go.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(Ref(ctx, map + "/Position"));
            tpd.rotationInput = new InputActionProperty(Ref(ctx, map + "/Rotation"));
            tpd.trackingStateInput = new InputActionProperty(Ref(ctx, map + "/Tracking State"));

            // modelo simple del control
            var model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Modelo";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(go.transform, false);
            model.transform.localScale = new Vector3(0.035f, 0.055f, 0.035f);
            model.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            model.transform.localPosition = new Vector3(0f, -0.01f, -0.04f);
            var mr = model.GetComponent<Renderer>();
            mr.sharedMaterial = ControllerMaterial(ctx);
            mr.sortingOrder = 200;      // después del menú (sortingOrder 100): el control se ve al apuntarlo

            var ray = go.AddComponent<XRRayInteractor>();
            Bind(ray.selectInput, Ref(ctx, map + "/Select"), Ref(ctx, map + "/Select Value"));
            Bind(ray.activateInput, Ref(ctx, map + "/Activate"), Ref(ctx, map + "/Activate Value"));
            Bind(ray.uiPressInput, Ref(ctx, map + "/UI Press"), Ref(ctx, map + "/UI Press Value"));
            ray.enableUIInteraction = true;
            ray.maxRaycastDistance = 8f;
            ray.raycastMask = ~(1 << 2);
            var line = go.AddComponent<XRInteractorLineVisual>();
            line.lineWidth = 0.006f;
            line.validColorGradient = Grad(new Color(0.3f, 0.85f, 1f, 1f), new Color(0.3f, 0.85f, 1f, 0.6f));
            line.invalidColorGradient = Grad(new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0f));
            var lr = go.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.sharedMaterial = ctx.mats["M_Rayo"];
                lr.sortingOrder = 200;  // el rayo se ve sobre el menú al señalar un destino
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
            }
            EditorUtility.SetDirty(ray);
            return go;
        }

        /// <summary>Material del control: igual al negro mate, pero en la cola transparente para dibujarse después del menú.</summary>
        static Material ControllerMaterial(Ctx ctx)
        {
            const string path = Root + "/Materials/M_Control.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(ctx.mats["M_Negro_Mate"]) { name = "M_Control" };
                if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(ctx.mats["M_Negro_Mate"]);
            if (m.HasProperty("_QueueControl")) m.SetFloat("_QueueControl", 1f);   // URP: respetar la cola manual
            m.renderQueue = 3100;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Bind(XRInputButtonReader reader, InputActionReference performed, InputActionReference value)
        {
            if (reader == null) return;
            reader.inputSourceMode = XRInputButtonReader.InputSourceMode.InputActionReference;
            reader.inputActionReferencePerformed = performed;
            reader.inputActionReferenceValue = value;
        }

        static Gradient Grad(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(a.a, 0f), new GradientAlphaKey(b.a, 1f) });
            return g;
        }

        // =========================================================================== sistemas
        static void BuildSystems(Ctx ctx, CasaVRPlayer player, DayNightController dayNight)
        {
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();

            var menuGO = new GameObject("Menu de teletransporte");
            var menu = menuGO.AddComponent<TeleportMenu>();
            menu.player = player;
            menu.fader = player.head.GetComponentInChildren<ScreenFader>();
            menu.dayNight = dayNight;
            menu.xrMenuActions = new[] { Ref(ctx, "CasaVR Izquierda/Menu"), Ref(ctx, "CasaVR Derecha/Menu") };
            menu.desktopMenuAction = Ref(ctx, "CasaVR Escritorio/Menu");
            menu.rounded = UISprite("UI_Redondeado");
            menu.roundedOutline = UISprite("UI_RedondeadoBorde");
            menu.glow = UISprite("UI_Brillo");
            menu.icons = new List<MenuIcon>();
            foreach (var guid in AssetDatabase.FindAssets("Icono_ t:Sprite", new[] { Root + "/Textures/UI" }))
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sp != null && sp.name.StartsWith("Icono_"))
                    menu.icons.Add(new MenuIcon { key = sp.name.Substring("Icono_".Length), sprite = sp });
            }
            if (menu.rounded == null) ctx.log.AppendLine("• ATENCIÓN: no se encontraron los sprites del menú (Textures/UI); el menú se verá sin esquinas redondeadas.");
            int Order(RoomEntry r)
            {
                string[] order = { "Sala", "Cocina", "Dormitorio", "Bano", "Lavanderia", "Patio", "Balcon", "Pasillo",
                                   "Entrada", "Escalera", "JardinFrontal", "JardinTrasero" };
                string key = r.id.Substring(r.id.IndexOf('_') + 1);
                int i = System.Array.IndexOf(order, key);
                return r.floor * 100 + (i < 0 ? 99 : i);
            }
            menu.rooms = ctx.rooms.OrderBy(Order).ToList();
            var di = player.head.GetComponent<DesktopInteractor>();
            if (di != null) di.menu = menu;
        }

        static Sprite UISprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Textures/UI/" + name + ".png");

        static void AddSimulator(Ctx ctx)
        {
            var prefab = CasaVRProjectSetup.FindSimulatorPrefab();
            if (prefab == null)
            {
                ctx.log.AppendLine("• Simulador XR no importado (CasaVR > 1. Configurar proyecto lo importa). Sin él se usa el modo escritorio.");
                return;
            }
            // El simulador queda apagado en la escena; SimulatorGate lo enciende al dar Play solo si no hay
            // un visor real (con Quest Link el simulador quitaría el visor del Input System).
            var gateGO = new GameObject("CasaVR Simulador (solo editor)");
            gateGO.tag = "EditorOnly";   // no se incluye en el APK
            var gate = gateGO.AddComponent<SimulatorGate>();
            gate.enableSimulator = EditorPrefs.GetBool("CasaVR_SimuladorActivo", true);
            var sim = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            sim.tag = "EditorOnly";
            sim.transform.SetParent(gateGO.transform, false);
            sim.SetActive(false);
            gate.simulator = sim;
            ctx.log.AppendLine("• XR Interaction Simulator agregado (solo editor, se apaga solo si hay un visor conectado)" +
                               (gate.enableSimulator ? "" : " — desactivado"));
        }
    }
}
