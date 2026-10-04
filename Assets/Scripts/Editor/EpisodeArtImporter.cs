using UnityEditor;
using UnityEngine;

namespace RecreioEspacial.EditorTools
{
    /// <summary>
    /// Configura automaticamente a importação das imagens do episódio (Assets/Resources/EP01):
    /// tipo Sprite (mantém o tamanho original, sem forçar potência de 2), até 4096 px
    /// (os cenários têm 2672 px de largura), sem mipmaps e com compressão de alta qualidade.
    /// </summary>
    public class EpisodeArtImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/EP01/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }

    /// <summary>Menu "Recreio Espacial" no topo do Editor.</summary>
    public static class RecreioMenu
    {
        [MenuItem("Recreio Espacial/Reimportar arte do EP01")]
        static void ReimportArt()
        {
            AssetDatabase.ImportAsset("Assets/Resources/EP01", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("Arte do EP01 reimportada.");
        }

        [MenuItem("Recreio Espacial/Apagar jogo salvo")]
        static void DeleteSave()
        {
            Core.GameState.DeleteSave();
            Debug.Log("Jogo salvo apagado (o botão Continuar some da tela de título).");
        }
    }
}
