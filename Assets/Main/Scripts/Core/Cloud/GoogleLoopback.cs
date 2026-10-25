#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

/// <summary>
/// GoogleLoopback
/// The Windows game (and the Unity editor) signs in with Google through the player's normal web browser: the game opens the Google sign-in page, and
/// after the player picked a Gmail account the browser comes back to a tiny web server that lives only inside the game, on this computer
/// (http://127.0.0.1:port/callback). The page it serves reads the login tokens from the address and hands them to the game; then it tells the
/// player to close the tab. Nothing outside the computer ever talks to this server, and it stops as soon as the sign-in is done or cancelled.
/// Used by CloudSave.SignInWithGoogle. The browser version uses a pop-up and the callback page instead (LakbayAuth.jslib).
/// Start() returns the port it listens on; TryTake() returns the answer once; Stop() closes everything.
/// </summary>
public static class GoogleLoopback
{
    private const int FirstPort = 53127;
    private const int PortCount = 10;

    private static HttpListener _listener;
    private static Thread _thread;
    private static readonly ConcurrentQueue<string> Results = new ConcurrentQueue<string>();

    private const string Page = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Lakbay Salita</title></head>" +
        "<body style=\"font-family:Arial,sans-serif;background:#fdf8ee;color:#3b2a1a;text-align:center;padding:60px 20px\">" +
        "<h1 style=\"margin:0 0 10px\">Lakbay Salita</h1><h2 id=\"m\">Finishing your sign in...</h2>" +
        "<p id=\"s\" style=\"color:#6b5237\"></p><script>" +
        "var d=(location.search||'').replace(/^\\?/,'')+'&'+(location.hash||'').replace(/^#/,'');" +
        "var bad=/(^|&)error=/.test(d);" +
        "fetch('/token',{method:'POST',body:d}).then(function(){" +
        "document.getElementById('m').textContent=bad?'Sign in did not finish.':'You are signed in.';" +
        "document.getElementById('s').textContent=bad?'Go back to the game to see why, and try again.':'You can close this tab and go back to the game.';" +
        "}).catch(function(){document.getElementById('m').textContent='Please go back to the game and try again.';});" +
        "</script></body></html>";

    /// <summary>Starts the local listener; returns its port, or 0 when none of the ports could be used.</summary>
    public static int Start()
    {
        Stop();
        for (int i = 0; i < PortCount; i++)
        {
            int port = FirstPort + i;
            var listener = new HttpListener();
            try
            {
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
            }
            catch (Exception)
            {
                try { listener.Close(); } catch (Exception) { }
                continue;
            }
            _listener = listener;
            _thread = new Thread(Loop) { IsBackground = true, Name = "GoogleLoopback" };
            _thread.Start();
            return port;
        }
        return 0;
    }

    private static void Loop()
    {
        HttpListener listener = _listener;
        while (listener != null && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (Exception)
            {
                return;   // stopped
            }
            try
            {
                string path = context.Request.Url.AbsolutePath;
                if (context.Request.HttpMethod == "POST" && path == "/token")
                {
                    string body;
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        body = reader.ReadToEnd();
                    }
                    Results.Enqueue(body);
                    Reply(context, "text/plain", "ok");
                }
                else
                {
                    Reply(context, "text/html; charset=utf-8", Page);
                }
            }
            catch (Exception)
            {
                try { context.Response.Abort(); } catch (Exception) { }
            }
        }
    }

    private static void Reply(HttpListenerContext context, string type, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        context.Response.ContentType = type;
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Close();
    }

    /// <summary>The answer of the browser ("query&amp;fragment" of the callback address), once; false when nothing has come yet.</summary>
    public static bool TryTake(out string payload)
    {
        return Results.TryDequeue(out payload);
    }

    /// <summary>Stops the listener and forgets any answer that was not taken.</summary>
    public static void Stop()
    {
        HttpListener listener = _listener;
        _listener = null;
        _thread = null;
        if (listener != null)
        {
            try { listener.Stop(); listener.Close(); } catch (Exception) { }
        }
        string ignored;
        while (Results.TryDequeue(out ignored)) { }
    }
}
#endif
