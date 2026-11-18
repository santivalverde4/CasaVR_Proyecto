using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager.UI;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace CasaVR.EditorTools
{
    /// <summary>
    /// Menú CasaVR > 1. Configurar proyecto: Android ARM64 / IL2CPP / Vulkan / GameActivity, OpenXR con
    /// soporte Meta Quest (Quest 2, 3, 3S y Pro) y perfiles de control Touch, OpenXR en PC (Link / simulador),
    /// calidad URP para VR autónoma e importación del XR Interaction Simulator.
    /// Es idempotente: se puede ejecutar varias veces y solo cambia lo necesario.
    /// </summary>
    public static class CasaVRProjectSetup
    {
        const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string XriPackage = "com.unity.xr.interaction.toolkit";

        [MenuItem("CasaVR/1. Configurar proyecto (Meta Quest + OpenXR)", priority = 1)]
        public static void ConfigureFromMenu()
        {
            var log = new StringBuilder();
            Configure(log);
            bool switchNow = EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                             EditorUtility.DisplayDialog("CasaVR",
                                 "Configuración aplicada.\n\n¿Cambiar ahora la plataforma activa a Android (necesario para compilar el APK)?\n" +
                                 "Puede tardar unos minutos la primera vez.", "Cambiar a Android", "Más tarde");
            if (switchNow)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                log.AppendLine("• Plataforma activa: Android");
            }
            Debug.Log("[CasaVR] Configuración del proyecto:\n" + log);
            EditorUtility.DisplayDialog("CasaVR", "Proyecto configurado.\n\n" + log +
                "\nSiguiente paso: CasaVR > 2. Construir escena de la casa.", "OK");
        }

        public static void Configure(StringBuilder log)
        {
            EnsureURP(log);
            ConfigurePlayer(log);
            ConfigureXR(BuildTargetGroup.Android, true, log);
            ConfigureXR(BuildTargetGroup.Standalone, false, log);
            ConfigureQuality(log);
            ImportSimulatorSample(log);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Player / Android
        static void ConfigurePlayer(StringBuilder log)
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            if (PlayerSettings.productName == "New Unity Project" || string.IsNullOrEmpty(PlayerSettings.productName))
                PlayerSettings.productName = "Casa VR";
            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (string.IsNullOrEmpty(id) || id.StartsWith("com.DefaultCompany") || id.StartsWith("com.Company"))
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.casavr.apartamentos");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel32)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            log.AppendLine("• Color lineal, Android ARM64 + IL2CPP, API mínima 32, Vulkan, GameActivity, ASTC");
        }

        // ------------------------------------------------------------------ XR Plug-in Management + OpenXR
        static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTarget()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget per);
            if (per != null) return per;
            string guid = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget").FirstOrDefault();
            if (!string.IsNullOrEmpty(guid))
                per = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guid));
            if (per == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
                per = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(per, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                AssetDatabase.SaveAssets();
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, per, true);
            return per;
        }

        static void ConfigureXR(BuildTargetGroup group, bool quest, StringBuilder log)
        {
            var per = GetOrCreatePerBuildTarget();
            if (!per.HasSettingsForBuildTarget(group)) per.CreateDefaultSettingsForBuildTarget(group);
            if (!per.HasManagerSettingsForBuildTarget(group)) per.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = per.SettingsForBuildTarget(group);
            // Android: XR al iniciar. PC: desactivado para probar con el XR Interaction Simulator sin errores;
            // para Quest Link actívalo en Project Settings > XR Plug-in Management (pestaña PC).
            general.InitManagerOnStart = quest;
            EditorUtility.SetDirty(general);
            var manager = per.ManagerSettingsForBuildTarget(group);
            bool hasOpenXR = manager.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoaderType);
            if (!hasOpenXR)
                XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, group);
            // quitar otros loaders (Oculus XR Plugin) para evitar conflictos con OpenXR
            foreach (var l in manager.activeLoaders.ToList())
                if (l != null && l.GetType().FullName != OpenXRLoaderType)
                    XRPackageMetadataStore.RemoveLoader(manager, l.GetType().FullName, group);
            EditorUtility.SetDirty(per);
            EditorUtility.SetDirty(manager);

            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null)
            {
                log.AppendLine($"• ATENCIÓN: no se encontraron ajustes de OpenXR para {group}. Abre Project Settings > XR Plug-in Management > OpenXR una vez y vuelve a ejecutar.");
                return;
            }
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group);
            var enabled = new List<string>();
            foreach (var f in settings.GetFeatures())
            {
                if (f == null) continue;
                string n = f.GetType().Name;
                bool want = n == "OculusTouchControllerProfile" ||
                            (quest && (n == "MetaQuestFeature" || n == "MetaQuestTouchPlusControllerProfile" || n == "MetaQuestTouchProControllerProfile"));
                if (want && !f.enabled)
                {
                    f.enabled = true;
                    EditorUtility.SetDirty(f);
                }
                if (f.enabled) enabled.Add(n);
            }
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            if (quest) settings.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            EditorUtility.SetDirty(settings);
            log.AppendLine($"• OpenXR ({group}): {string.Join(", ", enabled)} · Single Pass Instanced" +
                           (quest ? "" : " · inicio automático desactivado en PC (simulador)"));
        }

        // ------------------------------------------------------------------ URP (si está instalado y no hay asset asignado)
        static void EnsureURP(StringBuilder log)
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            var assetType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            var dataType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
            if (assetType == null || dataType == null)
            {
                log.AppendLine("• URP no está instalado: se usará el pipeline Built-in (recomendado instalar URP para Quest)");
                return;
            }
            const string dir = "Assets/CasaVR/Settings";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/CasaVR", "Settings");
            var data = ScriptableObject.CreateInstance(dataType);
            AssetDatabase.CreateAsset(data, dir + "/CasaVR_URP_Renderer.asset");
            var create = assetType.GetMethod("Create", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var asset = create != null ? create.Invoke(null, new object[] { data }) as RenderPipelineAsset : null;
            if (asset == null)
            {
                log.AppendLine("• No se pudo crear el asset de URP automáticamente (créalo en Assets > Create > Rendering)");
                return;
            }
            AssetDatabase.CreateAsset(asset, dir + "/CasaVR_URP.asset");
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            log.AppendLine("• Creado y asignado el asset de URP (Assets/CasaVR/Settings)");
        }

        // ------------------------------------------------------------------ Calidad (URP)
        static void ConfigureQuality(StringBuilder log)
        {
            var rp = GraphicsSettings.defaultRenderPipeline;
            if (rp == null)
            {
                log.AppendLine("• Render pipeline: Built-in (se recomienda URP para Quest; la escena funciona en ambos)");
                return;
            }
            var assets = new List<RenderPipelineAsset> { rp };
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                var q = QualitySettings.GetRenderPipelineAssetAt(i);
                if (q != null && !assets.Contains(q)) assets.Add(q);
            }
            foreach (var a in assets)
            {
                var so = new SerializedObject(a);
                SetInt(so, "m_MSAA", 4);
                SetBool(so, "m_SupportsHDR", false);
                SetFloat(so, "m_RenderScale", 1f);
                SetBool(so, "m_RequireDepthTexture", false);
                SetBool(so, "m_RequireOpaqueTexture", false);
                SetBool(so, "m_MainLightShadowsSupported", true);
                SetInt(so, "m_MainLightShadowmapResolution", 1024);
                SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
                SetBool(so, "m_AdditionalLightShadowsSupported", false);
                SetFloat(so, "m_ShadowDistance", 22f);
                SetInt(so, "m_ShadowCascadeCount", 1);
                SetBool(so, "m_SoftShadowsSupported", false);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.AppendLine($"• URP ({assets.Count} asset/s): MSAA 4x, sin HDR, sombras del sol 1024, 8 luces por objeto");
        }

        static void SetInt(SerializedObject so, string p, int v) { var sp = so.FindProperty(p); if (sp != null) sp.intValue = v; }
        static void SetBool(SerializedObject so, string p, bool v) { var sp = so.FindProperty(p); if (sp != null) sp.boolValue = v; }
        static void SetFloat(SerializedObject so, string p, float v) { var sp = so.FindProperty(p); if (sp != null) sp.floatValue = v; }

        // ------------------------------------------------------------------ Simulador XR (pruebas en PC)
        public static void ImportSimulatorSample(StringBuilder log)
        {
            if (FindSimulatorPrefab() != null)
            {
                log.AppendLine("• XR Interaction Simulator: ya importado");
                return;
            }
            var info = UnityEditor.PackageManager.PackageInfo.FindForPackageName(XriPackage);
            if (info == null)
            {
                log.AppendLine("• ATENCIÓN: XR Interaction Toolkit no está instalado");
                return;
            }
            foreach (var s in Sample.FindByPackage(XriPackage, info.version))
            {
                if (s.displayName != "XR Interaction Simulator") continue;
                bool ok = s.isImported || s.Import(Sample.ImportOptions.OverridePreviousImports);
                log.AppendLine(ok ? "• XR Interaction Simulator importado (muestra de XRI)" : "• No se pudo importar el XR Interaction Simulator");
                return;
            }
            log.AppendLine("• No se encontró la muestra XR Interaction Simulator en XRI " + info.version);
        }

        public static GameObject FindSimulatorPrefab()
        {
            foreach (var guid in AssetDatabase.FindAssets("XR Interaction Simulator t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("/XR Interaction Simulator.prefab"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }
    }
}
