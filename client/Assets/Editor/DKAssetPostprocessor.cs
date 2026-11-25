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
            ti.sRGBTexture = f == "albedo" || f == "thumb" || f == "decal";
            if (f == "decal") ti.alphaIsTransparency = true;
            ti.alphaSource = (f == "metallic" || f == "decal") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.mipmapEnabled = f != "thumb";
            ti.maxTextureSize = 2048;
            ios.overridden = true; ios.maxTextureSize = 2048; ios.format = TextureImporterFormat.ASTC_6x6;
            if (f == "paintmask")
            {
                // Paint-shop source: albedo + body mask, read on the CPU to repaint the car, so it stays uncompressed.
                ti.sRGBTexture = true; ti.isReadable = true; ti.mipmapEnabled = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ios.maxTextureSize = 1024; ios.format = TextureImporterFormat.RGBA32;
            }
            ti.SetPlatformTextureSettings(ios);
        }
        else if (p.Contains("/DriftKuwait/Crowd/"))
        {
            // Spectator cut-outs: alpha-tested billboards
            ti.textureType = TextureImporterType.Default;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;
            var cios = ti.GetPlatformTextureSettings("iPhone");
            cios.overridden = true; cios.maxTextureSize = 1024; cios.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(cios);
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
        else if (p.Contains("/DriftKuwait/Map/"))
        {
            // The painted map is shown zoomed in: keep it full size and sharp, no stretching to a power of two.
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 4096;
            ios.overridden = true; ios.maxTextureSize = 4096; ios.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(ios);
        }
        else if (p.Contains("/DriftKuwait/Tex/"))
        {
            string f = System.IO.Path.GetFileNameWithoutExtension(p);
            ti.textureType = f.EndsWith("_n") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            // Photo facades, gates, boundary walls and the sky are seen big: keep them sharp.
            bool hero = f.StartsWith("pbr_") || f.StartsWith("fac_") || f.StartsWith("gate_") || f.StartsWith("wall_") || f.StartsWith("sky_") || f.StartsWith("frond");
            int size = hero ? 2048 : 1024;
            if (f.StartsWith("fac_") || f.StartsWith("gate_") || f.StartsWith("sky_")) ti.wrapMode = TextureWrapMode.Clamp;
            if (f.StartsWith("sky_")) { ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; }
            if (f.StartsWith("frond")) ti.alphaIsTransparency = true;
            ti.maxTextureSize = size;
            ios.overridden = true; ios.maxTextureSize = size; ios.format = f.EndsWith("_n") ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_5x5;

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
