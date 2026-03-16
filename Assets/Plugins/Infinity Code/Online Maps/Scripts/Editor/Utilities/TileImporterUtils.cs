/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.IO;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public static class TileImporterUtils
    {
        public static void FixImportSettings(MapSource source, string resourcesPath, string streamingAssetsPath)
        {
            string path;
            string specialFolderName;

            if (source == MapSource.Resources || source == MapSource.ResourcesAndOnline)
            {
                path = resourcesPath;
                specialFolderName = "Resources";
            }
            else
            {
                path = streamingAssetsPath;
                specialFolderName = "StreamingAssets";
            }

            int tokenIndex = path.IndexOf("{");

            string specialFolder = Path.Combine(Application.dataPath, specialFolderName);

            if (tokenIndex != -1)
            {
                if (tokenIndex > 1)
                {
                    string folder = path.Substring(0, tokenIndex - 1);
                    specialFolder = Path.Combine(specialFolder, folder);
                }
            }
            else specialFolder = Path.Combine(specialFolder, "OnlineMapsTiles");

            if (!Directory.Exists(specialFolder)) return;

            string[] tiles = Directory.GetFiles(specialFolder, "*.png", SearchOption.AllDirectories);
            float count = tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                string shortPath = "Assets/" + tiles[i].Substring(Application.dataPath.Length + 1);
                FixTileImporter(shortPath, i / count);
            }

            EditorUtility.ClearProgressBar();
        }
        
        private static void FixTileImporter(string shortPath, float progress)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(shortPath) as TextureImporter;
            EditorUtility.DisplayProgressBar("Update import settings for tiles", "Please wait, this may take several minutes.", progress);
            if (!textureImporter) return;
            
            textureImporter.mipmapEnabled = false;
            textureImporter.isReadable = true;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.maxTextureSize = 256;
            AssetDatabase.ImportAsset(shortPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}