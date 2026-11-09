using System.IO;
using UnityEditor;
using UnityEngine;
public static class CascadeMenuAssets
{
    [MenuItem("Cascade/Import supplied menu assets")]
    public static string Import()
    {
        int count=0;
        foreach(string path in Directory.GetFiles("Assets/Cascade/Resources/V3/Menu","*.png",SearchOption.AllDirectories))
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var im=(TextureImporter)AssetImporter.GetAtPath(path);im.textureType=TextureImporterType.Sprite;im.spriteImportMode=SpriteImportMode.Single;
            im.alphaSource=TextureImporterAlphaSource.FromInput;im.alphaIsTransparency=true;im.sRGBTexture=true;im.mipmapEnabled=false;im.npotScale=TextureImporterNPOTScale.None;
            im.maxTextureSize=4096;im.textureCompression=TextureImporterCompression.Uncompressed;im.crunchedCompression=false;im.filterMode=FilterMode.Bilinear;im.wrapMode=TextureWrapMode.Clamp;im.spritePixelsPerUnit=100;
            var settings=new TextureImporterSettings();im.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteExtrude=0;im.SetTextureSettings(settings);
            im.ClearPlatformTextureSettings("iPhone");im.ClearPlatformTextureSettings("Standalone");im.SaveAndReimport();count++;
        }
        return "Imported "+count+" original PNGs, FullRect / bilinear / no mipmaps / max4096 / alpha";
    }
}
