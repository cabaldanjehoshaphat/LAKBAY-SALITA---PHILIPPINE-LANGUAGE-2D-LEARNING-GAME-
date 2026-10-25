using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// CloudSave
/// Email accounts and online progress for Lakbay Salita, using Supabase (https://supabase.com). It talks to the project's web API with plain
/// UnityWebRequest calls, so it works in the Windows game and in the WebGL (browser) game, and needs no Supabase package.
///  - Log in using Gmail (SignInWithGoogle): Supabase Auth with the Google provider. The player never types a password in the game. The Windows game opens
///    the web browser and a tiny local listener (GoogleLoopback) receives the answer; the browser version opens a pop-up and the callback page
///    (WebAuth/callback.html) sends the answer back (LakbayAuth.jslib). Needs Google turned on in Supabase (Documentation/Supabase/Google_Sign_In_Setup.md).
///  - Account: SignUp, SignIn, SignOut, SendPasswordReset. The player can use a real email address, or just a USERNAME (3 to 20 letters, numbers,
///    dot, dash or underscore): a username is turned into a made-up address "name@players.lakbay-salita.invalid" (the reserved ".invalid" ending can never
///    be a real mailbox), so no email is collected. A username account has no email, so its password can not be reset by mail. Supabase Auth keeps the email and the password hash; the game never
///    stores the password, only the login tokens (SaveStore keys "account.*", which are not synced). The tokens are refreshed when they run out.
///  - Progress: SyncNow downloads the player's row from the table public.progress (one row per player, see Documentation/Supabase/Supabase_Setup.sql),
///    merges it with this device's progress (CloudMerge), saves the result on this device and uploads it. So a player who signs in on a NEW device
///    gets the progress of the old device back, and progress made on two devices is combined.
///  - Automatic: when signed in, the game syncs once at start and a few seconds after the progress changed (NotifyChanged, called by SaveStore and
///    PlayerDatabase). Without internet nothing is lost: the progress stays on the device and is uploaded at the next sync.
///  - DeleteOnlineData removes the player's row (the account itself stays; it can be removed in the Supabase dashboard).
/// The only values that identify the project are the Project URL and the publishable key below. The publishable key is public by design (the table
/// is protected by row level security); never put the secret / service_role key in the game.
/// Status text for the screen: CloudSave.Status; CloudSave.Changed is raised when the state or the text changes.
/// </summary>
public class CloudSave : MonoBehaviour
{
    public const string ProjectUrl = "https://qislgffezdzjolyofvsh.supabase.co";
    public const string PublishableKey = "sb_publishable_kRODcDxbD8LnXHEn7DH00g_B3R3VxFE";

    /// <summary>
    /// The callback page of the browser version (WebAuth/callback.html, published with GitHub Pages). After the Gmail sign in, Supabase sends the pop-up
    /// here; the page hands the login tokens back to the game and closes. This address must also be in Supabase: Authentication > URL Configuration >
    /// Redirect URLs. See Documentation/Supabase/Google_Sign_In_Setup.md.
    /// </summary>
    public const string WebCallbackUrl = "https://cabaldanjehoshaphat.github.io/LAKBAY-SALITA---PHILIPPINE-LANGUAGE-2D-LEARNING-GAME-/callback.html";

    /// <summary>The ending of the made-up address behind a username (".invalid" is reserved: it never belongs to a real mailbox).</summary>
    public const string UsernameDomain = "players.lakbay-salita.invalid";

    private const string EmailKey = "account.email";
    private const string UserKey = "account.user";
    private const string AccessKey = "account.access";
    private const string RefreshKey = "account.refresh";
    private const string ExpiresKey = "account.expires";
    private const string LastSyncKey = "account.lastSync";
    private const string HashKey = "account.hash";
    /// <summary>The account whose progress is on this device. It survives a logout (ClearSession keeps it) so another account never mixes with it.</summary>
    private const string OwnerKey = "account.owner";
    private const float DelaySeconds = 8f;
    private const float RetrySeconds = 60f;

    [Serializable] private class Credentials { public string email; public string password; }
    [Serializable] private class RefreshBody { public string refresh_token; }
    [Serializable] private class EmailBody { public string email; }
    [Serializable] private class AuthUser { public string id; public string email; }
    [Serializable]
    private class AuthResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public AuthUser user;
        public string id;      // a sign up that still needs the email to be confirmed answers with the user fields at the top
        public string email;
    }
    [Serializable]
    private class AuthError
    {
        public string error;
        public string error_code;
        public string error_description;
        public string msg;
        public string message;
        public string hint;
    }
    [Serializable] private class ProgressUpload { public string user_id; public CloudSaveBlob save; public PlayerData player; }
    [Serializable] private class ProgressRow { public string user_id; public CloudSaveBlob save; public PlayerData player; public string updated_at; }
    [Serializable] private class ProgressRows { public ProgressRow[] rows; }
    [Serializable] private class AuthProviders { public bool google; }
    [Serializable] private class AuthSettings { public AuthProviders external; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int Lakbay_OpenAuthPopup(string url, string allowedOrigin);
    [DllImport("__Internal")] private static extern void Lakbay_CloseAuthPopup();
#endif

    private static CloudSave _instance;
    private static string _status = string.Empty;
    private bool _busy;
    private float _pendingSince = -1f;
    private float _retryAt = -1f;

    /// <summary>Raised when the signed-in state or the status text changed (screens refresh themselves from it).</summary>
    public static event Action Changed;

    public static bool SignedIn
    {
        get { return !string.IsNullOrEmpty(SaveStore.GetString(RefreshKey, string.Empty)); }
    }

    /// <summary>The address the account is registered with (for a username account this is the made-up address).</summary>
    public static string Email
    {
        get { return SaveStore.GetString(EmailKey, string.Empty); }
    }

    /// <summary>True when the player signed up with a username instead of a real email address.</summary>
    public static bool IsUsernameAccount
    {
        get { return Email.EndsWith("@" + UsernameDomain, StringComparison.Ordinal); }
    }

    /// <summary>What to show as "signed in as": the username, or the email address.</summary>
    public static string DisplayName
    {
        get
        {
            string email = Email;
            return email.EndsWith("@" + UsernameDomain, StringComparison.Ordinal) ? email.Substring(0, email.Length - UsernameDomain.Length - 1) : email;
        }
    }

    /// <summary>True while a sign in, a sync or another online call is running.</summary>
    public static bool Busy
    {
        get { return _instance != null && _instance._busy; }
    }

    /// <summary>The last message for the player, for example "Saved online at 14:32".</summary>
    public static string Status
    {
        get { return _status; }
    }

    private static void SetStatus(string text)
    {
        _status = text;
        if (Changed != null)
        {
            Changed();
        }
    }

    // ------------------------------------------------------------------ life cycle

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }
        var go = new GameObject("CloudSave");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<CloudSave>();
    }

    private void Start()
    {
        if (SignedIn)
        {
            SyncNow(null);   // once when the game opens
        }
    }

    private void Update()
    {
        if (_googleWaiting)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            string answer;
            if (GoogleLoopback.TryTake(out answer))
            {
                HandleGoogleResult(answer);
            }
#endif
            if (_googleWaiting && Time.unscaledTime > _googleDeadline)
            {
                CancelGoogle("The sign in took too long. Please try again.");
            }
        }
        if (!SignedIn || _busy)
        {
            return;
        }
        if (_pendingSince >= 0f && Time.unscaledTime - _pendingSince > DelaySeconds)
        {
            _pendingSince = -1f;
            if (CloudMerge.LocalHash() != SaveStore.GetString(HashKey, string.Empty))
            {
                SyncNow(null);
            }
        }
        else if (_retryAt >= 0f && Time.unscaledTime > _retryAt)
        {
            _retryAt = -1f;
            SyncNow(null);
        }
    }

    /// <summary>Called when the saved progress changed: a sync follows a few seconds later (and not at all when nothing really changed).</summary>
    public static void NotifyChanged()
    {
        if (_instance != null && SignedIn && _instance._pendingSince < 0f)
        {
            _instance._pendingSince = Time.unscaledTime;
        }
    }

    // ------------------------------------------------------------------ account

    // ------------------------------------------------------------------ Sign in with Google (Gmail)

    private static bool _googleWaiting;
    private static int _googleSession;
    private Action<bool, string> _googleDone;
    private float _googleDeadline;

    /// <summary>True while the Google sign in window is open and the game waits for the player to finish there.</summary>
    public static bool GoogleWaiting
    {
        get { return _googleWaiting; }
    }

    /// <summary>
    /// "Log in using Gmail": opens the Google sign in (a pop-up in the browser version, the web browser in the Windows game). When the player has
    /// chosen a Gmail account, the game is signed in and the progress is synced (done(true, "Saved online at ...")). Calling it again while waiting cancels.
    /// </summary>
    public static void SignInWithGoogle(Action<bool, string> done)
    {
        if (_instance == null)
        {
            return;
        }
        if (_googleWaiting)
        {
            CancelGoogle();
            return;
        }
        if (_instance._busy)
        {
            Finish(done, false, "Please wait a moment.");
            return;
        }
        Run(_instance.GoogleStartRoutine(done));
    }

    /// <summary>Stops waiting for the Google sign in (the player pressed the button again, or it took too long).</summary>
    public static void CancelGoogle()
    {
        CancelGoogle("Sign in was cancelled.");
    }

    private static void CancelGoogle(string message)
    {
        if (!_googleWaiting)
        {
            return;
        }
        Action<bool, string> done = _instance != null ? _instance._googleDone : null;
        EndGoogle();
        Finish(done, false, message);
    }

    private static void EndGoogle()
    {
        _googleWaiting = false;
        if (_instance != null)
        {
            _instance._googleDeadline = 0f;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        Lakbay_CloseAuthPopup();
#else
        if (_instance != null)
        {
            _instance.StartCoroutine(_instance.StopLoopbackLater(_googleSession));
        }
        else
        {
            GoogleLoopback.Stop();
        }
#endif
        if (Changed != null)
        {
            Changed();
        }
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    /// <summary>The local listener answers the browser page first, then it is closed (unless a new sign in started meanwhile).</summary>
    private IEnumerator StopLoopbackLater(int session)
    {
        yield return new WaitForSecondsRealtime(2f);
        if (session == _googleSession && !_googleWaiting)
        {
            GoogleLoopback.Stop();
        }
    }
#endif

    private IEnumerator GoogleStartRoutine(Action<bool, string> done)
    {
        _busy = true;
        SetStatus("Checking the Gmail sign in...");
        long code = 0;
        string text = null;
        yield return Send("GET", "/auth/v1/settings", string.Empty, null, null, delegate (long c, string t) { code = c; text = t; });
        _busy = false;
        if (code < 200 || code >= 300)
        {
            Finish(done, false, Explain(code, text));
            yield break;
        }
        AuthSettings settings = JsonUtility.FromJson<AuthSettings>(text);
        if (settings == null || settings.external == null || !settings.external.google)
        {
            Finish(done, false, "Gmail sign in is not turned on yet. Please try again later.");
            yield break;
        }

        _googleSession++;
        string redirect;
#if UNITY_WEBGL && !UNITY_EDITOR
        redirect = WebCallbackUrl;
#else
        int port = GoogleLoopback.Start();
        if (port == 0)
        {
            Finish(done, false, "Could not start the sign in. Please try again.");
            yield break;
        }
        redirect = "http://127.0.0.1:" + port + "/callback";
#endif
        string url = ProjectUrl + "/auth/v1/authorize?provider=google&redirect_to=" + Uri.EscapeDataString(redirect);
#if UNITY_WEBGL && !UNITY_EDITOR
        if (Lakbay_OpenAuthPopup(url, new Uri(WebCallbackUrl).GetLeftPart(UriPartial.Authority)) == 0)
        {
            Finish(done, false, "Your browser blocked the sign in window. Please allow pop-ups for this page and try again.");
            yield break;
        }
#else
        Application.OpenURL(url);
#endif
        _googleDone = done;
        _googleDeadline = Time.unscaledTime + 180f;
        _googleWaiting = true;
        SetStatus("Choose your Gmail account in the window that opened. Waiting...");
    }

    /// <summary>Called by LakbayAuth.jslib (browser version) with the answer of the callback page; the Windows game feeds the same text from GoogleLoopback.</summary>
    public void OnAuthFragment(string payload)
    {
        HandleGoogleResult(payload);
    }

    private void HandleGoogleResult(string payload)
    {
        if (!_googleWaiting)
        {
            return;
        }
        Dictionary<string, string> values = ParseQuery(payload);
        string error;
        if (values.TryGetValue("error", out error))
        {
            string description;
            values.TryGetValue("error_description", out description);
            Action<bool, string> done = _googleDone;
            EndGoogle();
            Finish(done, false, error == "access_denied" ? "Sign in was cancelled." : "Gmail sign in failed" + (string.IsNullOrEmpty(description) ? "." : ": " + UnityWebRequest.UnEscapeURL(description.Replace('+', ' '))));   // Supabase encodes the text twice
            return;
        }
        string access;
        if (!values.TryGetValue("access_token", out access) || string.IsNullOrEmpty(access))
        {
            return;   // not an answer the game understands: keep waiting
        }
        StartCoroutine(GoogleFinishRoutine(values));
    }

    private IEnumerator GoogleFinishRoutine(Dictionary<string, string> values)
    {
        Action<bool, string> done = _googleDone;
        EndGoogle();
        _busy = true;
        SetStatus("Signing in...");
        string access = values["access_token"];
        string refresh;
        values.TryGetValue("refresh_token", out refresh);
        string expiresText;
        int expires = 3600;
        if (values.TryGetValue("expires_in", out expiresText))
        {
            int.TryParse(expiresText, NumberStyles.Integer, CultureInfo.InvariantCulture, out expires);
        }
        long code = 0;
        string text = null;
        yield return Send("GET", "/auth/v1/user", string.Empty, access, null, delegate (long c, string t) { code = c; text = t; });
        _busy = false;
        AuthUser user = code >= 200 && code < 300 ? JsonUtility.FromJson<AuthUser>(text) : null;
        if (user == null || string.IsNullOrEmpty(user.id))
        {
            Finish(done, false, code >= 200 && code < 300 ? "Could not sign in. Please try again." : Explain(code, text));
            yield break;
        }
        StoreSession(new AuthResponse { access_token = access, refresh_token = refresh ?? string.Empty, expires_in = expires, user = user }, user.email ?? string.Empty);
        SetStatus("Signed in. Getting your progress...");
        yield return SyncRoutine(done);
    }

    private static Dictionary<string, string> ParseQuery(string payload)
    {
        var map = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(payload))
        {
            return map;
        }
        foreach (string part in payload.TrimStart('#', '?').Split('&'))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            map[part.Substring(0, eq)] = UnityWebRequest.UnEscapeURL(part.Substring(eq + 1));
        }
        return map;
    }

    /// <summary>Creates an account. done(true, text): signed in, or the text says that the email must be confirmed first.</summary>
    public static void SignUp(string email, string password, Action<bool, string> done)
    {
        string login;
        if (!Check(email, password, done, out login)) return;
        Run(_instance.SignUpRoutine(login, password, done));
    }

    public static void SignIn(string email, string password, Action<bool, string> done)
    {
        string login;
        if (!Check(email, password, done, out login)) return;
        Run(_instance.SignInRoutine(login, password, done));
    }

    private static bool _signingOut;

    /// <summary>
    /// Logs out of this device. What is not online yet is saved first; then the login is forgotten and, when everything is safe online, the progress of this
    /// device is cleared. The next account that logs in here starts from its own online copy and never mixes with this one. done is called at the end
    /// (the text is in Status).
    /// </summary>
    public static void SignOut(Action done = null, bool clearDevice = false)
    {
        if (_instance == null || _signingOut || !SignedIn)
        {
            if (done != null) done();
            return;
        }
        _instance.StartCoroutine(_instance.SignOutRoutine(done, clearDevice));
    }

    /// <summary>clearDevice: nothing is saved first and the device is always cleared (used after the online copy was deleted on purpose).</summary>
    private IEnumerator SignOutRoutine(Action done, bool clearDevice)
    {
        _signingOut = true;
        while (_busy)
        {
            yield return null;
        }
        if (!clearDevice && CloudMerge.LocalHash() != SaveStore.GetString(HashKey, string.Empty))
        {
            SetStatus("Saving your progress...");
            yield return SyncRoutine(null);
        }
        bool safe = clearDevice || CloudMerge.LocalHash() == SaveStore.GetString(HashKey, string.Empty);   // everything is online
        string access = SaveStore.GetString(AccessKey, string.Empty);
        if (!string.IsNullOrEmpty(access))
        {
            StartCoroutine(Send("POST", "/auth/v1/logout", string.Empty, access, null, null));   // best effort
        }
        ClearSession();
        if (safe && PlayerDatabase.Instance != null)
        {
            PlayerDatabase.Instance.ClearLocalProgress(true);
            SaveStore.DeleteKey(OwnerKey);
            SaveStore.Save();
            SetStatus(clearDevice ? "Your online progress was deleted and this device was cleared." : "Signed out. Log in again to get your progress back.");
        }
        else
        {
            SetStatus("Signed out. Some progress could not be saved online, so it stays on this device.");
        }
        _signingOut = false;
        if (done != null) done();
    }

    /// <summary>
    /// The progress on this device belongs to the account that last synced here (OwnerKey). When a different account logs in, that progress is cleared
    /// first, so two accounts on one device never mix. Progress that never belonged to an account (no owner) is merged into the account as before.
    /// </summary>
    private static void PrepareDeviceFor(string userId)
    {
        string owner = SaveStore.GetString(OwnerKey, string.Empty);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(owner) || owner == userId)
        {
            return;
        }
        if (SignedIn)
        {
            ClearSession();   // the old login must not upload the cleared progress
        }
        if (PlayerDatabase.Instance != null)
        {
            PlayerDatabase.Instance.ClearLocalProgress(true);
        }
        SaveStore.DeleteKey(OwnerKey);
        SaveStore.Save();
    }

    /// <summary>Signs out without a message (used when the profile is reset: the online copy is kept, so signing in again brings it back).</summary>
    public static void SignOutQuiet()
    {
        if (SignedIn)
        {
            ClearSession();
        }
    }

    public static void SendPasswordReset(string email, Action<bool, string> done)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            Finish(done, false, "Type your email address first.");
            return;
        }
        if (!email.Contains("@"))
        {
            Finish(done, false, "A username has no email, so its password can not be reset by mail. Sign up with an email address to get this.");
            return;
        }
        Run(_instance.ResetRoutine(email.Trim(), done));
    }

    /// <summary>Downloads the online progress, merges it with this device and uploads the result.</summary>
    public static void SyncNow(Action<bool, string> done)
    {
        if (!SignedIn)
        {
            Finish(done, false, "Sign in first.");
            return;
        }
        Run(_instance.SyncRoutine(done));
    }

    /// <summary>Removes the player's online progress (the row). The progress on this device stays.</summary>
    public static void DeleteOnlineData(Action<bool, string> done)
    {
        if (!SignedIn)
        {
            Finish(done, false, "Sign in first.");
            return;
        }
        Run(_instance.DeleteRoutine(done));
    }

    /// <summary>
    /// Turns what the player typed into the login address: an email address stays as it is (lower case); a username becomes
    /// "username@players.lakbay-salita.invalid". Returns false with a message when it is not usable.
    /// </summary>
    public static bool Resolve(string typed, out string login, out string error)
    {
        login = null;
        error = null;
        string text = typed == null ? string.Empty : typed.Trim();
        if (text.Length == 0)
        {
            error = "Please type your username or email.";
            return false;
        }
        if (text.Contains("@"))
        {
            int at = text.IndexOf('@');
            if (at < 1 || at == text.Length - 1 || !text.Substring(at).Contains("."))
            {
                error = "That email address does not look right.";
                return false;
            }
            login = text.ToLowerInvariant();
            return true;
        }
        string name = text.ToLowerInvariant();
        if (name.Length < 3 || name.Length > 20)
        {
            error = "A username needs 3 to 20 characters.";
            return false;
        }
        foreach (char c in name)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
            if (!ok)
            {
                error = "A username can only have letters, numbers, dot, dash and underscore.";
                return false;
            }
        }
        login = name + "@" + UsernameDomain;
        return true;
    }

    private static bool Check(string typed, string password, Action<bool, string> done, out string login)
    {
        string problem;
        if (!Resolve(typed, out login, out problem))
        {
            Finish(done, false, problem);
            return false;
        }
        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            Finish(done, false, "The password needs at least 6 characters.");
            return false;
        }
        return true;
    }

    private static void Run(IEnumerator routine)
    {
        if (_instance == null)
        {
            return;
        }
        _instance.StartCoroutine(routine);
    }

    private static void Finish(Action<bool, string> done, bool ok, string text)
    {
        SetStatus(text);
        if (done != null)
        {
            done(ok, text);
        }
    }

    // ------------------------------------------------------------------ the calls

    private IEnumerator SignUpRoutine(string email, string password, Action<bool, string> done)
    {
        if (_busy) { Finish(done, false, "Please wait a moment."); yield break; }
        _busy = true;
        SetStatus("Creating your account...");
        long code = 0;
        string text = null;
        yield return Send("POST", "/auth/v1/signup", JsonUtility.ToJson(new Credentials { email = email, password = password }), null, null,
            delegate (long c, string t) { code = c; text = t; });
        _busy = false;
        if (code < 200 || code >= 300)
        {
            Finish(done, false, Explain(code, text));
            yield break;
        }
        AuthResponse response = JsonUtility.FromJson<AuthResponse>(text);
        if (response != null && !string.IsNullOrEmpty(response.access_token))
        {
            StoreSession(response, email);
            Finish(done, true, "Account created. You are signed in.");
            Run(SyncRoutine(null));
        }
        else
        {
            Finish(done, true, "Account created. Please open the email we sent to " + email + " and confirm it, then press Sign in.");
        }
    }

    private IEnumerator SignInRoutine(string email, string password, Action<bool, string> done)
    {
        if (_busy) { Finish(done, false, "Please wait a moment."); yield break; }
        _busy = true;
        SetStatus("Signing in...");
        long code = 0;
        string text = null;
        yield return Send("POST", "/auth/v1/token?grant_type=password", JsonUtility.ToJson(new Credentials { email = email, password = password }), null, null,
            delegate (long c, string t) { code = c; text = t; });
        _busy = false;
        if (code < 200 || code >= 300)
        {
            Finish(done, false, Explain(code, text));
            yield break;
        }
        AuthResponse response = JsonUtility.FromJson<AuthResponse>(text);
        if (response == null || string.IsNullOrEmpty(response.access_token))
        {
            Finish(done, false, "Could not sign in. Please try again.");
            yield break;
        }
        StoreSession(response, email);
        SetStatus("Signed in. Getting your progress...");
        yield return SyncRoutine(done);
    }

    private IEnumerator ResetRoutine(string email, Action<bool, string> done)
    {
        if (_busy) { Finish(done, false, "Please wait a moment."); yield break; }
        _busy = true;
        SetStatus("Sending the email...");
        long code = 0;
        string text = null;
        yield return Send("POST", "/auth/v1/recover", JsonUtility.ToJson(new EmailBody { email = email }), null, null,
            delegate (long c, string t) { code = c; text = t; });
        _busy = false;
        if (code < 200 || code >= 300)
        {
            Finish(done, false, Explain(code, text));
            yield break;
        }
        Finish(done, true, "If this email has an account, a message to reset the password is on its way.");
    }

    private IEnumerator SyncRoutine(Action<bool, string> done)
    {
        if (_busy) { if (done != null) done(false, "Please wait a moment."); yield break; }
        _busy = true;
        SetStatus("Syncing your progress...");

        // a valid login token (refreshes it when it ran out)
        bool tokenOk = false;
        yield return EnsureToken(delegate (bool ok) { tokenOk = ok; });
        if (!tokenOk)
        {
            _busy = false;
            _retryAt = Time.unscaledTime + RetrySeconds;
            Finish(done, false, SignedIn ? "Could not reach the server. Your progress is kept on this device." : "Please sign in again.");
            yield break;
        }
        string access = SaveStore.GetString(AccessKey, string.Empty);
        string userId = SaveStore.GetString(UserKey, string.Empty);

        // 1. the online row of this player (row level security returns only the player's own row)
        long code = 0;
        string text = null;
        yield return Send("GET", "/rest/v1/progress?select=save,player,updated_at", string.Empty, access, null, delegate (long c, string t) { code = c; text = t; });
        if (code < 200 || code >= 300)
        {
            _busy = false;
            if (code == 0) _retryAt = Time.unscaledTime + RetrySeconds;
            Finish(done, false, Explain(code, text));
            yield break;
        }
        ProgressRows rows = JsonUtility.FromJson<ProgressRows>("{\"rows\":" + text + "}");
        ProgressRow remote = rows != null && rows.rows != null && rows.rows.Length > 0 ? rows.rows[0] : null;

        // 2. merge with this device and keep the result here
        string lastSync = SaveStore.GetString(LastSyncKey, string.Empty);
        CloudMerge.Result merged = CloudMerge.Merge(remote != null ? remote.save : null, remote != null ? remote.player : null,
            remote != null ? remote.updated_at : null, lastSync);
        CloudMerge.ApplyLocally(merged);

        // 3. upload the merged progress when it differs from the online copy
        string stamp = remote != null ? remote.updated_at : string.Empty;
        if (remote == null || merged.DiffersFromRemote)
        {
            var upload = new ProgressUpload { user_id = userId, save = merged.Save, player = merged.Player };
            long upCode = 0;
            string upText = null;
            var headers = new Dictionary<string, string> { { "Prefer", "resolution=merge-duplicates,return=representation" } };
            yield return Send("POST", "/rest/v1/progress?on_conflict=user_id", JsonUtility.ToJson(upload), access, headers,
                delegate (long c, string t) { upCode = c; upText = t; });
            if (upCode < 200 || upCode >= 300)
            {
                _busy = false;
                if (upCode == 0) _retryAt = Time.unscaledTime + RetrySeconds;
                Finish(done, false, Explain(upCode, upText));
                yield break;
            }
            ProgressRows saved = JsonUtility.FromJson<ProgressRows>("{\"rows\":" + upText + "}");
            if (saved != null && saved.rows != null && saved.rows.Length > 0)
            {
                stamp = saved.rows[0].updated_at;
            }
        }
        SaveStore.SetString(LastSyncKey, stamp ?? string.Empty);
        SaveStore.SetString(HashKey, CloudMerge.LocalHash());
        SaveStore.SetString(OwnerKey, userId);
        SaveStore.Save();
        _busy = false;
        _pendingSince = -1f;
        Finish(done, true, "Saved online at " + DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture) + ".");
    }

    private IEnumerator DeleteRoutine(Action<bool, string> done)
    {
        if (_busy) { Finish(done, false, "Please wait a moment."); yield break; }
        _busy = true;
        bool tokenOk = false;
        yield return EnsureToken(delegate (bool ok) { tokenOk = ok; });
        if (!tokenOk)
        {
            _busy = false;
            Finish(done, false, "Could not reach the server. Please try again.");
            yield break;
        }
        long code = 0;
        string text = null;
        string userId = SaveStore.GetString(UserKey, string.Empty);
        yield return Send("DELETE", "/rest/v1/progress?user_id=eq." + UnityWebRequest.EscapeURL(userId), string.Empty, SaveStore.GetString(AccessKey, string.Empty), null,
            delegate (long c, string t) { code = c; text = t; });
        _busy = false;
        if (code < 200 || code >= 300)
        {
            Finish(done, false, Explain(code, text));
            yield break;
        }
        SaveStore.SetString(LastSyncKey, string.Empty);
        SaveStore.SetString(HashKey, string.Empty);
        Finish(done, true, "Your online progress was deleted.");
    }

    /// <summary>Makes sure the stored access token is valid for at least a minute; refreshes it with the refresh token when needed.</summary>
    private IEnumerator EnsureToken(Action<bool> done)
    {
        long expires = 0;
        long.TryParse(SaveStore.GetString(ExpiresKey, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out expires);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!string.IsNullOrEmpty(SaveStore.GetString(AccessKey, string.Empty)) && expires - now > 60)
        {
            done(true);
            yield break;
        }
        string refresh = SaveStore.GetString(RefreshKey, string.Empty);
        if (string.IsNullOrEmpty(refresh))
        {
            done(false);
            yield break;
        }
        long code = 0;
        string text = null;
        yield return Send("POST", "/auth/v1/token?grant_type=refresh_token", JsonUtility.ToJson(new RefreshBody { refresh_token = refresh }), null, null,
            delegate (long c, string t) { code = c; text = t; });
        if (code >= 200 && code < 300)
        {
            AuthResponse response = JsonUtility.FromJson<AuthResponse>(text);
            if (response != null && !string.IsNullOrEmpty(response.access_token))
            {
                StoreSession(response, Email);
                done(true);
                yield break;
            }
        }
        if (code == 400 || code == 401 || code == 403)
        {
            ClearSession();   // the login is no longer valid (for example the password was changed)
        }
        done(false);
    }

    // ------------------------------------------------------------------ helpers

    private static void StoreSession(AuthResponse response, string email)
    {
        string id = response.user != null && !string.IsNullOrEmpty(response.user.id) ? response.user.id : response.id;
        PrepareDeviceFor(id);
        SaveStore.SetString(EmailKey, email);
        SaveStore.SetString(UserKey, id ?? string.Empty);
        SaveStore.SetString(AccessKey, response.access_token);
        SaveStore.SetString(RefreshKey, response.refresh_token ?? string.Empty);
        long expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, response.expires_in);
        SaveStore.SetString(ExpiresKey, expires.ToString(CultureInfo.InvariantCulture));
        SaveStore.Save();
        if (Changed != null)
        {
            Changed();
        }
    }

    private static void ClearSession()
    {
        foreach (string key in new[] { EmailKey, UserKey, AccessKey, RefreshKey, ExpiresKey, LastSyncKey, HashKey })
        {
            SaveStore.DeleteKey(key);
        }
        SaveStore.Save();
        if (_instance != null)
        {
            _instance._pendingSince = -1f;
            _instance._retryAt = -1f;
        }
        if (Changed != null)
        {
            Changed();
        }
    }

    /// <summary>One web call. done(statusCode, body); statusCode 0 means no connection.</summary>
    private IEnumerator Send(string method, string path, string body, string bearer, Dictionary<string, string> headers, Action<long, string> done)
    {
        using (var request = new UnityWebRequest(ProjectUrl + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            if (!string.IsNullOrEmpty(body))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("apikey", PublishableKey);
            request.SetRequestHeader("Authorization", "Bearer " + (string.IsNullOrEmpty(bearer) ? PublishableKey : bearer));
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> h in headers)
                {
                    request.SetRequestHeader(h.Key, h.Value);
                }
            }
            request.timeout = 20;
            yield return request.SendWebRequest();
            bool noConnection = request.result == UnityWebRequest.Result.ConnectionError;
            if (done != null)
            {
                done(noConnection ? 0 : request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
            }
        }
    }

    /// <summary>A sentence the player understands for a failed call.</summary>
    private static string Explain(long code, string body)
    {
        if (code == 0)
        {
            return "No internet connection. Your progress is kept on this device.";
        }
        AuthError error = null;
        try
        {
            error = JsonUtility.FromJson<AuthError>(body);
        }
        catch (Exception)
        {
        }
        string key = error != null ? (error.error_code ?? string.Empty) : string.Empty;
        string message = error != null ? (error.msg ?? error.message ?? error.error_description ?? string.Empty) : string.Empty;
        switch (key)
        {
            case "invalid_credentials": return "Wrong username, email or password.";
            case "email_not_confirmed": return "Please open the email we sent you and confirm it first.";
            case "user_already_exists":
            case "email_exists": return "This username or email already has an account. Try Sign in, or choose another username.";
            case "weak_password": return "That password is too easy to guess. Try a longer one.";
            case "email_address_invalid": return "That email address is not valid.";
            case "over_email_send_rate_limit":
            case "over_request_rate_limit": return "Too many tries. Please wait a few minutes.";
            case "email_address_not_authorized": return "This email can not get a message yet (the game's email sender is not set up).";
            case "signup_disabled": return "Creating accounts is turned off.";
        }
        if (code == 401 || code == 403)
        {
            return "Please sign in again.";
        }
        return string.IsNullOrEmpty(message) ? "Something went wrong (" + code + "). Please try again." : message;
    }
}
