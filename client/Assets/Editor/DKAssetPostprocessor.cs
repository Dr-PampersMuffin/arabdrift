using UnityEditor;
using UnityEngine;

/// Import rules for generated assets, so nothing needs setting by hand:
///   Resources/DriftKuwait/Cars/<id>/  body.obj (no materials, real normals), albedo (sRGB), normal (normal map),
///                                     metallic (linear, R metallic + A smoothness)
///   Resources/DriftKuwait/UI/         UI art as sprites, no mipmaps
///   Resources/DriftKuwait/Tex/        tiling world textures (repeat, mipmaps, linear for *_n normal maps)
///   Resources/DriftKuwait/Audio/      music streams, effects decompress on load
public class DKAssetPostprocessor : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/DriftKuwait/Cars/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.globalScale = 1f;
        mi.useFileScale = false;
        mi.isReadable = false;
        mi.addCollider = false;
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.importAnimation = false;
        mi.importCameras = false;
        mi.importLights = false;
    }

    void OnPreprocessTexture()
    {
        var ti = (TextureImporter)assetImporter;
        string p = assetPath.Replace('\\', '/');
        var ios = ti.GetPlatformTextureSettings("iPhone");
        if (p.Contains("/DriftKuwait/Cars/"))
        {
            string f = System.IO.Path.GetFileNameWithoutExtension(p);
            ti.textureType = f == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = f == "albedo" || f == "thumb";
            ti.alphaSource = f == "metallic" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.mipmapEnabled = f != "thumb";
            ti.maxTextureSize = 2048;
            ios.overridden = true; ios.maxTextureSize = 2048; ios.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(ios);
        }
        else if (p.Contains("/DriftKuwait/UI/"))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;
            ti.wrapMode = TextureWrapMode.Clamp;
            ios.overridden = true; ios.maxTextureSize = 2048; ios.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(ios);
        }
        else if (p.Contains("/DriftKuwait/Tex/"))
        {
            string f = System.IO.Path.GetFileNameWithoutExtension(p);
            ti.textureType = f.EndsWith("_n") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.maxTextureSize = 1024;
            ios.overridden = true; ios.maxTextureSize = 1024; ios.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(ios);
        }
    }

    void OnPreprocessAudio()
    {
        if (!assetPath.Contains("/DriftKuwait/Audio/")) return;
        var ai = (AudioImporter)assetImporter;
        var s = ai.defaultSampleSettings;
        bool music = assetPath.Contains("/Music/") || assetPath.Contains("/Voice/") || assetPath.Contains("amb_");
        s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = music ? 0.6f : 0.75f;
        ai.defaultSampleSettings = s;
        ai.forceToMono = false;
        ai.loadInBackground = music;
    }
}
