namespace BetterTransitSelector.Domain {
    #region Using Statements

    using System.IO;

    using Colossal.PSI.Environment;

    #endregion

    /// <summary>
    /// The folder creators pass pack assets through:
    /// <c>ModsData/BetterTransitSelector/SharedPrefabs</c>.
    /// </summary>
    /// <remarks>
    /// A collection or a family across two creators' uploads is the same pack asset in both --
    /// the asset's identity is the key. So the second creator needs the first's files, and this
    /// folder is the hand-off point in both directions:
    ///
    /// <list type="bullet">
    /// <item><b>Sending.</b> "Export my packs" in the options copies every pack the player made
    /// in the editor here -- the <c>.Prefab</c> and its <c>.cid</c>, which carries the GUID and
    /// must travel with it -- so there is one obvious place to grab the files from instead of
    /// hunting through the editor project.</item>
    /// <item><b>Receiving.</b> Files dropped here are simply loaded: the game's user asset
    /// database scans the whole user data folder recursively, ModsData included, and watches it,
    /// so a pack copied in shows up in the editor's asset browser. Nothing of ours reads the
    /// folder. When the receiver publishes, the editor's dependency collector packages the pack
    /// from wherever it lives, so it ships in their upload as it did in the sender's.</item>
    /// </list>
    ///
    /// The exporter's own copy landing here is harmless: the asset database registers one asset
    /// per GUID and ignores a second file carrying the same one (FileSystemDataSource logs
    /// "Duplicate Asset ... ignored"). The folder is under ModsData rather than the mod's own
    /// folder because the mod folder is wiped on every deploy.
    /// </remarks>
    public static class SharedPrefabs {
        public static readonly string Folder =
            Path.Combine(EnvPath.kUserDataPath, "ModsData", "BetterTransitSelector", "SharedPrefabs");

        /// <summary>Makes sure the folder exists; returns it.</summary>
        public static string Ensure() {
            Directory.CreateDirectory(Folder);
            return Folder;
        }

        /// <summary>
        /// Copies one asset's files into the folder: the file itself and its <c>.cid</c> sidecar.
        /// Overwrites, so re-exporting after an edit refreshes the copy.
        /// </summary>
        /// <returns>False when the source has no <c>.cid</c>, in which case nothing is copied: a
        /// pack without its GUID is a different pack on arrival, so it is better not sent.</returns>
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

        /// <summary>Opens the folder in the system file browser.</summary>
        public static void Open() {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName        = Ensure(),
                UseShellExecute = true,
            });
        }
    }
}
