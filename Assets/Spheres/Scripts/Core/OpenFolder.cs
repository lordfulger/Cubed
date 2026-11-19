using UnityEngine;

namespace Spheres
{
    /// <summary>Open a folder in Explorer / Finder / the Linux file manager.</summary>
    public static class FolderOpener
    {
        public static void Open(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (Application.isMobilePlatform)
            {
                // Android/iOS can't open a file manager at a path; tell the player where it is instead.
                try { System.IO.Directory.CreateDirectory(path); } catch (System.Exception) { }
                SpheresHud.AddFeedText("Saved in " + path);
                return;
            }
            try
            {
                System.IO.Directory.CreateDirectory(path);
                Application.OpenURL(new System.Uri(System.IO.Path.GetFullPath(path)).AbsoluteUri); // file:///C:/… or file:///Users/…
            }
            catch (System.Exception e) { Debug.LogWarning("[Spheres] Couldn't open " + path + ": " + e.Message); }
        }
    }
}
