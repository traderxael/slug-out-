using UnityEditor;
using UnityEngine;

namespace SlugOut.Editor
{
    /// Las texturas se extrajeron del GLB porque el FBX embebido no las suelta en Unity.
    /// El mapa normal necesita importarse como NormalMap y el base como sRGB; si no,
    /// el cuerpo se ve lavado y el relieve invertido.
    public class ImportadorFbxPixar : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("Shane/Texturas/")) return;
            TextureImporter ti = (TextureImporter)assetImporter;
            if (p.Contains("shane_base"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.maxTextureSize = 2048;
                ti.mipmapEnabled = true;
            }
            else if (p.Contains("shane_normal"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
                ti.maxTextureSize = 1024;
            }
            else if (p.Contains("shane_orm"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.maxTextureSize = 1024;
            }
        }
    }
}
