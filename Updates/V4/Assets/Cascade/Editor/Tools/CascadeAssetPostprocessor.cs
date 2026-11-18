using UnityEditor;
using UnityEngine;

namespace Cascade.EditorTools
{
    /// <summary>Единые настройки импорта для графики и музыки Cascade.</summary>
    public sealed class CascadeAssetPostprocessor : AssetPostprocessor
    {
        private const string SpriteRoot = "Assets/Cascade/Resources/Sprites/";
        private const string MusicRoot = "Assets/Cascade/Resources/Music/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteRoot))
            {
                return;
            }
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicRoot))
            {
                return;
            }
            AudioImporter importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
