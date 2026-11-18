using UnityEditor;
using UnityEngine;

namespace CasaVR.EditorTools
{
    /// <summary>
    /// Configuración de importación automática para el modelo (escala real 1 unidad = 1 m, sin rotaciones
    /// extra de Blender), las texturas (repetición, mipmaps, tamaño para Quest) y los audios.
    /// </summary>
    public class CasaVRAssetPostprocessor : AssetPostprocessor
    {
        const string Root = "Assets/CasaVR/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root + "Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = true;                 // necesario para MeshCollider en tiempo de ejecución
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.addCollider = false;               // los colliders se agregan por tipo en el constructor de escena
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.preserveHierarchy = true;
            mi.sortHierarchyByName = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(Root + "Textures/UI/"))
            {
                // sprites del menú: íconos y fondos redondeados (9-slice) con transparencia
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f;
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.mipmapEnabled = true;           // evita parpadeo al verlos de lejos en VR
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                bool sliced = assetPath.Contains("UI_Redondeado") || assetPath.Contains("UI_Brillo");
                ti.spriteBorder = sliced ? new Vector4(56f, 56f, 56f, 56f) : Vector4.zero;
                return;
            }
            bool particle = assetPath.Contains("T_Gota") || assetPath.Contains("T_Agua");
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = particle ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (assetPath.Contains("T_Agua_Chorro")) ti.wrapModeV = TextureWrapMode.Repeat;
            ti.anisoLevel = particle ? 1 : 4;
            ti.filterMode = FilterMode.Trilinear;
            ti.alphaIsTransparency = particle;
            ti.alphaSource = particle ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root + "Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            ai.defaultSampleSettings = s;
        }
    }
}
