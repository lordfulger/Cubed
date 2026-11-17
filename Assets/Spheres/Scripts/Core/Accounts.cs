using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Spheres
{
    /// <summary>
    /// Online accounts through Unity Gaming Services (username + password), using its web API directly so the
    /// game needs no extra packages. Signing in restores your profile (characters, unlocks, stats) from the
    /// cloud on any PC and keeps it backed up. The session is remembered between launches; passwords are never
    /// stored. Setup: link the Unity project to a Unity Cloud project (Project Settings > Services), then in the
    /// Unity dashboard enable Authentication > Username &amp; Password and Cloud Save. See docs/ACCOUNTS.md.
    /// </summary>
    public class Accounts : MonoBehaviour
    {
        const string AuthBase = "https://player-auth.services.api.unity.com/v1/authentication/";
        const string CloudSaveBase = "https://cloud-save.services.api.unity.com/v1/data/projects/";
        const string SessionKey = "spheres.account.session";
        const string UserKey = "spheres.account.user";
        const string ProfileKey = "profile";

        public static Accounts Instance { get; private set; }

        public bool SignedIn => !string.IsNullOrEmpty(idToken);
        public string Username { get; private set; } = "";
        public string PlayerId { get; private set; } = "";
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "";
        public string Error { get; private set; } = "";
        public DateTime LastSync { get; private set; }

        string idToken;
        float tokenExpires;
        float uploadAt = -1f;

        /// <summary>The Unity Cloud project id: from linking the project, or Resources/SpheresOnline.txt.</summary>
        public static string ProjectId
        {
            get
            {
                if (!string.IsNullOrEmpty(Application.cloudProjectId)) return Application.cloudProjectId;
                var asset = Resources.Load<TextAsset>("SpheresOnline");
                return asset != null ? asset.text.Trim() : "";
            }
        }

        public static bool Configured => !string.IsNullOrEmpty(ProjectId);

        [Serializable] class Credentials { public string username; public string password; }
        [Serializable] class SessionRequest { public string sessionToken; }
        [Serializable] class AuthResponse { public string idToken; public string sessionToken; public string userId; public int expiresIn; }
        [Serializable] class ErrorResponse { public string title; public string detail; public int status; }
        [Serializable] class CloudItem { public string key; public string value; }
        [Serializable] class CloudItems { public CloudItem[] results; }
        [Serializable] class CloudProfile { public long savedAt; public string profile; }

        public static void Ensure(GameObject host)
        {
            if (Instance != null || host == null) return;
            Instance = host.AddComponent<Accounts>();
        }

        void Start()
        {
            Profile.Saved += () => { if (SignedIn) uploadAt = Time.unscaledTime + 4f; };
            // Remembered session: sign straight back in.
            if (Configured && PlayerPrefs.HasKey(SessionKey))
                StartCoroutine(Resume());
        }

        void Update()
        {
            if (!SignedIn) return;
            if (Time.unscaledTime > tokenExpires - 120f && !Busy) StartCoroutine(Resume());
            if (uploadAt > 0f && Time.unscaledTime > uploadAt && !Busy)
            {
                uploadAt = -1f;
                StartCoroutine(Upload());
            }
        }

        // ---------------------------------------------------------------- public actions

        /// <summary>Usernames: 3-20 letters, numbers or . - @ _</summary>
        public static string CheckUsername(string u)
        {
            if (string.IsNullOrEmpty(u) || u.Length < 3 || u.Length > 20) return "Usernames are 3 to 20 characters.";
            foreach (char c in u)
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '@' || c == '_')) return "Usernames can use letters, numbers and . - @ _";
            return null;
        }

        /// <summary>Passwords: 8-30 characters with an upper-case letter, a lower-case letter, a number and a symbol.</summary>
        public static string CheckPassword(string p)
        {
            if (string.IsNullOrEmpty(p) || p.Length < 8 || p.Length > 30) return "Passwords are 8 to 30 characters.";
            bool up = false, low = false, num = false, sym = false;
            foreach (char c in p)
            {
                if (char.IsUpper(c)) up = true;
                else if (char.IsLower(c)) low = true;
                else if (char.IsDigit(c)) num = true;
                else sym = true;
            }
            if (!(up && low && num && sym)) return "Passwords need an upper-case letter, a lower-case letter, a number and a symbol.";
            return null;
        }

        public void SignUp(string username, string password) => StartCoroutine(UsernamePassword("usernamepassword/sign-up", username, password, true));
        public void SignIn(string username, string password) => StartCoroutine(UsernamePassword("usernamepassword/sign-in", username, password, false));

        public void SignOut()
        {
            if (SignedIn && !Busy) StartCoroutine(Upload());
            idToken = null;
            PlayerId = "";
            Username = "";
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.DeleteKey(UserKey);
            PlayerPrefs.Save();
            Status = "Signed out. Your profile stays on this PC.";
            Error = "";
        }

        public void SyncNow()
        {
            if (SignedIn && !Busy) StartCoroutine(SyncProfile());
        }

        // ---------------------------------------------------------------- flows

        IEnumerator UsernamePassword(string path, string username, string password, bool creating)
        {
            if (Busy) yield break;
            Error = "";
            if (!Configured) { Error = "Online accounts aren't set up in this build yet."; yield break; }
            string bad = CheckUsername(username) ?? (creating ? CheckPassword(password) : null);
            if (bad != null) { Error = bad; yield break; }
            Busy = true;
            Status = creating ? "Creating your account..." : "Signing in...";
            string body = JsonUtility.ToJson(new Credentials { username = username, password = password });
            AuthResponse auth = null;
            yield return Post(AuthBase + path, body, null, r => auth = r);
            if (auth != null)
            {
                Accept(auth, username);
                Status = creating ? "Account created! Welcome, " + username + "." : "Signed in as " + username + ".";
                yield return SyncProfile();
            }
            Busy = false;
        }

        IEnumerator Resume()
        {
            if (Busy) yield break;
            string session = PlayerPrefs.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(session)) yield break;
            Busy = true;
            bool first = !SignedIn;
            Status = first ? "Signing in..." : Status;
            AuthResponse auth = null;
            yield return Post(AuthBase + "session-token", JsonUtility.ToJson(new SessionRequest { sessionToken = session }), null, r => auth = r);
            if (auth != null)
            {
                Accept(auth, PlayerPrefs.GetString(UserKey, ""));
                if (first)
                {
                    Status = "Signed in as " + Username + ".";
                    yield return SyncProfile();
                }
            }
            else if (first)
            {
                // The saved session expired or was revoked: ask for the password again.
                PlayerPrefs.DeleteKey(SessionKey);
                Status = "";
            }
            Busy = false;
        }

        void Accept(AuthResponse auth, string username)
        {
            idToken = auth.idToken;
            PlayerId = auth.userId ?? "";
            Username = string.IsNullOrEmpty(username) ? "Player" : username;
            tokenExpires = Time.unscaledTime + Mathf.Max(300, auth.expiresIn > 0 ? auth.expiresIn : 3600);
            if (!string.IsNullOrEmpty(auth.sessionToken)) PlayerPrefs.SetString(SessionKey, auth.sessionToken);
            PlayerPrefs.SetString(UserKey, Username);
            PlayerPrefs.Save();
            var gm = GameManager.Instance;
            if (gm != null && (string.IsNullOrWhiteSpace(gm.Settings.onlineName) || gm.Settings.onlineName == "Player"))
            {
                gm.Settings.onlineName = Username.Length > 16 ? Username.Substring(0, 16) : Username;
                gm.SaveSettings();
            }
        }

        /// <summary>Newest wins: load the cloud profile if it's newer than this PC's, otherwise back this PC's up.</summary>
        IEnumerator SyncProfile()
        {
            if (!SignedIn) yield break;
            string url = CloudSaveBase + ProjectId + "/players/" + PlayerId + "/items?keys=" + ProfileKey;
            string text = null;
            yield return Get(url, t => text = t);
            CloudProfile cloud = null;
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    var items = JsonUtility.FromJson<CloudItems>(text);
                    var item = items?.results != null && items.results.Length > 0 ? items.results[0] : null;
                    if (item != null && !string.IsNullOrEmpty(item.value)) cloud = JsonUtility.FromJson<CloudProfile>(item.value);
                }
                catch (Exception e) { Debug.LogWarning("[Spheres] Cloud profile unreadable: " + e.Message); }
            }
            var local = Profile.Current;
            if (cloud != null && !string.IsNullOrEmpty(cloud.profile) && cloud.savedAt > local.savedAt)
            {
                Profile.ReplaceFromJson(cloud.profile);
                Toasts.Show("Profile loaded", "Your characters, unlocks and stats were restored from your account.", new Color(0.5f, 0.85f, 1f));
                LastSync = DateTime.Now;
            }
            else yield return Upload();
        }

        IEnumerator Upload()
        {
            if (!SignedIn) yield break;
            var p = Profile.Current;
            if (p.savedAt == 0) p.Save();
            var payload = JsonUtility.ToJson(new CloudProfile { savedAt = p.savedAt, profile = JsonUtility.ToJson(p) });
            string body = JsonUtility.ToJson(new CloudItem { key = ProfileKey, value = payload });
            string url = CloudSaveBase + ProjectId + "/players/" + PlayerId + "/items";
            bool ok = false;
            yield return Send(url, "POST", body, idToken, _ => ok = true);
            if (ok) LastSync = DateTime.Now;
        }

        // ---------------------------------------------------------------- HTTP

        IEnumerator Post(string url, string body, string bearer, Action<AuthResponse> done)
        {
            string text = null;
            yield return Send(url, "POST", body, bearer, t => text = t);
            if (text == null) yield break;
            AuthResponse r = null;
            try { r = JsonUtility.FromJson<AuthResponse>(text); }
            catch (Exception e) { Error = "Unexpected reply: " + e.Message; }
            if (r != null && !string.IsNullOrEmpty(r.idToken)) done(r);
            else if (string.IsNullOrEmpty(Error)) Error = "Sign-in failed (no token returned).";
        }

        IEnumerator Get(string url, Action<string> done) => Send(url, "GET", null, idToken, done);

        IEnumerator Send(string url, string method, string body, string bearer, Action<string> done)
        {
            using (var req = new UnityWebRequest(url, method))
            {
                if (body != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("ProjectId", ProjectId);
                req.SetRequestHeader("UnityEnvironment", "production");
                if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
                req.timeout = 20;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    done(req.downloadHandler.text);
                    yield break;
                }
                // 404 on a cloud read just means "nothing saved yet".
                if (method == "GET" && req.responseCode == 404) { done(""); yield break; }
                string detail = req.error;
                try
                {
                    var err = JsonUtility.FromJson<ErrorResponse>(req.downloadHandler.text);
                    if (err != null && !string.IsNullOrEmpty(err.detail)) detail = err.detail;
                    else if (err != null && !string.IsNullOrEmpty(err.title)) detail = err.title;
                }
                catch (Exception) { }
                Error = Friendly(req.responseCode, detail);
                Status = "";
                Debug.LogWarning("[Spheres] Account request failed (" + req.responseCode + "): " + req.downloadHandler.text);
            }
        }

        static string Friendly(long code, string detail)
        {
            string d = (detail ?? "").ToLowerInvariant();
            if (code == 0) return "Couldn't reach the account server. Check your internet connection.";
            if (d.Contains("already exists") || d.Contains("existing")) return "That username is taken. Try another one.";
            if (code == 401 || d.Contains("invalid username or password") || d.Contains("wrong")) return "Wrong username or password.";
            if (d.Contains("not enabled") || d.Contains("provider")) return "Username sign-in isn't switched on for this game yet (see docs/ACCOUNTS.md).";
            if (code == 429) return "Too many attempts. Wait a minute and try again.";
            return "Account error: " + detail;
        }
    }
}
