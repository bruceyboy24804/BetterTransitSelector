namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System.IO;

    using Colossal.PSI.Environment;

    #endregion

    public static class SharedPrefabs {
        public static readonly string Folder =
            Path.Combine(EnvPath.kUserDataPath, "ModsData", "BetterTransitSelector", "SharedPrefabs");

        public static string Ensure() {
            Directory.CreateDirectory(Folder);
            return Folder;
        }

        public static bool Export(string assetPath) {
            var cid = assetPath + ".cid";
            if (!File.Exists(assetPath) || !File.Exists(cid)) {
                return false;
            }

            Ensure();
            File.Copy(assetPath, Path.Combine(Folder, Path.GetFileName(assetPath)), true);
            File.Copy(cid, Path.Combine(Folder, Path.GetFileName(cid)), true);
            return true;
        }

        public static void Open() {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName        = Ensure(),
                UseShellExecute = true,
            });
        }
    }
}
