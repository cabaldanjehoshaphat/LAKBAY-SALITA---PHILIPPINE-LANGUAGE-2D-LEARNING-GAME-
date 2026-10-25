// LakbayAuth.jslib
// Purpose: lets the WebGL (browser) game sign in with Google. Google does not allow its sign-in page inside a frame (the game runs inside a frame on
// itch.io), so the game opens it in a small pop-up window. When the player has chosen a Gmail account, Supabase sends the pop-up to the callback page
// (WebAuth/callback.html, published with GitHub Pages), which sends the login tokens back to the game with window.postMessage and closes itself.
// This file receives that message and passes it to the game: CloudSave.OnAuthFragment (the C# object is called "CloudSave").
// Used by CloudSave.SignInWithGoogle. Only the browser build uses it; the Windows game uses a local listener instead (GoogleLoopback.cs).
mergeInto(LibraryManager.library, {

  // Opens the pop-up (url) and starts listening for the answer of the callback page. allowedOrigin is the web address of the callback page, for example
  // "https://name.github.io". Returns 1 when the pop-up opened and 0 when the browser blocked it.
  Lakbay_OpenAuthPopup: function (urlPtr, originPtr) {
    var url = UTF8ToString(urlPtr);
    window.__lakbayAuthOrigin = UTF8ToString(originPtr);
    if (!window.__lakbayAuthListener) {
      window.__lakbayAuthListener = function (e) {
        if (!e || !e.data || e.data.type !== 'lakbay-auth') return;
        if (e.origin !== window.__lakbayAuthOrigin && e.origin !== window.location.origin) return;   // only the callback page (or the game's own page) may answer
        var send = (typeof SendMessage === 'function') ? SendMessage : ((typeof Module !== 'undefined' && Module.SendMessage) ? Module.SendMessage : null);
        if (send) { send('CloudSave', 'OnAuthFragment', String(e.data.hash || '')); }
        try { if (window.__lakbayPopup && !window.__lakbayPopup.closed) { window.__lakbayPopup.close(); } } catch (x) {}
      };
      window.addEventListener('message', window.__lakbayAuthListener);
    }
    var w = null;
    try { w = window.open(url, 'lakbay-google-signin', 'width=520,height=720'); } catch (x) { w = null; }
    window.__lakbayPopup = w;
    return w ? 1 : 0;
  },

  // Closes the pop-up (when the player cancels in the game).
  Lakbay_CloseAuthPopup: function () {
    try { if (window.__lakbayPopup && !window.__lakbayPopup.closed) { window.__lakbayPopup.close(); } } catch (x) {}
    window.__lakbayPopup = null;
  }
});
