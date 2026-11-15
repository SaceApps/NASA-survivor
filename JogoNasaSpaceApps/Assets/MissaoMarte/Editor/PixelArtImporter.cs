using UnityEditor;
using UnityEngine;

namespace MissaoMarte.EditorTools
{
    /// <summary>
    /// Spritesheets do Space Explorers Pixel Pack (Resources/Personagens e Resources/Props) são pixel
    /// art: sem filtro, sem compressão e sem mipmaps, para não borrar nem gerar artefatos. Os quadros
    /// são recortados em tempo de execução a partir do JSON (ver UI/SpriteSheet.cs).
    /// </summary>
    sealed class PixelArtImporter : AssetPostprocessor
    {
        static readonly string[] Folders =
        {
            "Assets/MissaoMarte/Resources/Personagens/",
            "Assets/MissaoMarte/Resources/Props/",
        };

        public override uint GetVersion()
        {
            return 2;
        }

        void OnPreprocessTexture()
        {
            if (!System.Array.Exists(Folders, folder => assetPath.StartsWith(folder)))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // A folha de objetos tem 768 x 3584 px; com limite menor o Unity a reduziria.
            importer.maxTextureSize = 4096;
        }
    }
}
