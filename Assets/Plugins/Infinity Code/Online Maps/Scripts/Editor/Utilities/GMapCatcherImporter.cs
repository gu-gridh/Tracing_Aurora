/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.IO;
using UnityEditor;

namespace OnlineMaps.Editors
{
    public static class GMapCatcherImporter
    {
        public static void Import(MapSource source)
        {
            string folder = EditorUtility.OpenFolderPanel("Select GMapCatcher tiles folder", string.Empty, "");
            if (string.IsNullOrEmpty(folder)) return;

            string[] files = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories);
            if (files.Length == 0) return;

            string specialFolderName;

            if (source == MapSource.Resources || source == MapSource.ResourcesAndOnline)
            {
                specialFolderName = "Resources";
            }
            else
            {
                specialFolderName = "StreamingAssets";
            }

            string specialPath = "Assets/" + specialFolderName + "/OnlineMapsTiles";

            bool needAsk = true;
            bool overwrite = false;
            foreach (string file in files)
            {
                if (!ImportFile(file, folder, specialPath, ref overwrite, ref needAsk)) break;
            }

            AssetDatabase.Refresh();
        }

        private static bool ImportFile(string file, string folder, string resPath, ref bool overwrite, ref bool needAsk)
        {
            string shortPath = file.Substring(folder.Length + 1);
            shortPath = shortPath.Replace('\\', '/');
            string[] shortArr = shortPath.Split('/');
            int zoom = 17 - int.Parse(shortArr[0]);
            int x = int.Parse(shortArr[1]) * 1024 + int.Parse(shortArr[2]);
            int y = int.Parse(shortArr[3]) * 1024 + int.Parse(shortArr[4].Substring(0, shortArr[4].Length - 4));
            string dir = Path.Combine(resPath, string.Format("{0}/{1}", zoom, x));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string destFileName = Path.Combine(dir, y + ".png");
            if (File.Exists(destFileName))
            {
                if (needAsk)
                {
                    needAsk = false;
                    int result = EditorUtility.DisplayDialogComplex("File already exists", "File already exists. Overwrite?", "Overwrite", "Skip", "Cancel");
                    if (result == 0) overwrite = true;
                    else if (result == 1)
                    {
                        overwrite = false;
                        return true;
                    }
                    else return false;
                }

                if (!overwrite) return true;
            }
            File.Copy(file, destFileName, true);
            return true;
        }
    }
}