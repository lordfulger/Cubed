// Steam Workshop: publish maps (and content-pack folders), and load subscribed items as content.
// Compiled only with SPHERES_STEAM (see docs/STEAM.md); otherwise the stub below is used.
#if SPHERES_STEAM
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Spheres.Net
{
    public static class SteamWorkshop
    {
        public static bool Available => SteamIntegration.Available;
        public static string Status { get; private set; } = "";
        public static bool Busy { get; private set; }
        /// <summary>Installed subscribed item folders (loaded as content).</summary>
        public static readonly List<string> InstalledFolders = new List<string>();

        /// <summary>Find subscribed items, download missing ones, and load them all as content.</summary>
        public static async void LoadSubscribed()
        {
            if (!Available || Busy) return;
            Busy = true;
            Status = "Checking Workshop subscriptions...";
            try
            {
                InstalledFolders.Clear();
                int pageNo = 1, total = 0;
                while (pageNo < 20)
                {
                    var page = await Steamworks.Ugc.Query.Items.WhereUserSubscribed().GetPageAsync(pageNo);
                    if (!page.HasValue || page.Value.ResultCount == 0) break;
                    foreach (var item in page.Value.Entries)
                    {
                        total++;
                        if (!item.IsInstalled) await item.DownloadAsync();
                        if (item.IsInstalled && !string.IsNullOrEmpty(item.Directory)) InstalledFolders.Add(item.Directory);
                    }
                    page.Value.Dispose();
                    pageNo++;
                }
                foreach (var f in InstalledFolders) ContentLoader.AddFolder(f);
                if (InstalledFolders.Count > 0)
                {
                    ContentLoader.LoadAll();
                    MapRegistry.RefreshUserMaps();
                }
                Status = total == 0 ? "No Workshop subscriptions" : InstalledFolders.Count + " Workshop item(s) loaded";
            }
            catch (Exception e)
            {
                Status = "Workshop error: " + e.Message;
                Debug.LogException(e);
            }
            Busy = false;
        }

        /// <summary>Publish a map: writes it (plus a preview image) to a staging folder and uploads it.</summary>
        public static void PublishMap(MapData map)
        {
            if (map == null) return;
            string safe = string.Join("_", (map.name ?? "map").Split(Path.GetInvalidFileNameChars()));
            string dir = Path.Combine(Application.persistentDataPath, "WorkshopUpload", safe);
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, safe + ".spheresmap.json"), map.ToJson(true));
                string preview = Path.Combine(Application.persistentDataPath, "WorkshopUpload", safe + "_preview.png");
                var thumb = MapThumbnails.Get(map, "publish:" + map.name);
                if (thumb != null) File.WriteAllBytes(preview, thumb.EncodeToPNG());
                PublishFolder(dir, map.name, map.description, File.Exists(preview) ? preview : null, "Map");
            }
            catch (Exception e)
            {
                Status = "Publish failed: " + e.Message;
                Debug.LogException(e);
            }
        }

        /// <summary>Upload any content folder (maps, card packs, block packs) as a new Workshop item.</summary>
        public static async void PublishFolder(string folder, string title, string description, string previewPng, string tag)
        {
            if (!Available || Busy) { Status = Available ? "Busy - try again in a moment" : "Steam isn't running"; return; }
            Busy = true;
            Status = "Uploading \"" + title + "\" to the Workshop...";
            try
            {
                var editor = Steamworks.Ugc.Editor.NewCommunityFile
                    .WithTitle(string.IsNullOrWhiteSpace(title) ? "Untitled" : title)
                    .WithDescription(description ?? "")
                    .WithContent(folder)
                    .WithTag(tag ?? "Content")
                    .WithPublicVisibility();
                if (!string.IsNullOrEmpty(previewPng)) editor = editor.WithPreviewFile(previewPng);
                var result = await editor.SubmitAsync();
                if (result.Success)
                {
                    Status = "Published! Workshop item " + result.FileId.Value
                        + (result.NeedsWorkshopAgreement ? " - accept the Workshop agreement on Steam to make it visible" : "");
                    Steamworks.SteamFriends.OpenWebOverlay("steam://url/CommunityFilePage/" + result.FileId.Value);
                }
                else Status = "Upload failed: " + result.Result;
            }
            catch (Exception e)
            {
                Status = "Upload failed: " + e.Message;
                Debug.LogException(e);
            }
            Busy = false;
        }

        /// <summary>Open the game's Workshop page in the Steam overlay.</summary>
        public static void Browse()
        {
            if (!Available) return;
            Steamworks.SteamFriends.OpenWebOverlay("steam://url/SteamWorkshopPage/" + SteamIntegration.AppId);
        }
    }
}
#else
namespace Spheres.Net
{
    /// <summary>Stub used when the game is built without SPHERES_STEAM.</summary>
    public static class SteamWorkshop
    {
        public static bool Available => false;
        public static string Status => "Steam Workshop needs a Steam build";
        public static bool Busy => false;
        public static readonly System.Collections.Generic.List<string> InstalledFolders = new System.Collections.Generic.List<string>();
        public static void LoadSubscribed() { }
        public static void PublishMap(MapData map) { }
        public static void PublishFolder(string folder, string title, string description, string previewPng, string tag) { }
        public static void Browse() { }
    }
}
#endif
