# "Log in using Gmail": what you have to set up (about 20 minutes)

The game side is finished. The button works as soon as Google is turned on in Supabase. Until then the game says
"Gmail sign in is not turned on yet". You need three things: a Google sign-in app, the Supabase settings, and a tiny public page for the browser version.

## 1. Google: create the sign-in app (Google Cloud Console)
Use the Gmail account that will own the app.
1. Go to **https://console.cloud.google.com** and accept the terms. **Create project**: name `lakbay-salita`.
2. Menu **APIs & Services > OAuth consent screen** (may be called "Google Auth Platform"):
   - User type **External**, app name **Lakbay Salita**, your Gmail as support email and developer contact.
   - Scopes: only the basic ones (**openid, email, profile**). These do not need Google's review.
   - **Publish the app** ("In production"). While it stays in "Testing", only the test users you list can log in.
3. **APIs & Services > Credentials > Create credentials > OAuth client ID**:
   - Application type **Web application**, name `lakbay-salita-web`.
   - **Authorized redirect URIs** (exactly): `https://qislgffezdzjolyofvsh.supabase.co/auth/v1/callback`
   - Press **Create**. Copy the **Client ID** and the **Client secret**. The secret is private: do not share it in a chat, in GitHub or in the game.

## 2. Supabase (your project lakbay-salita)
1. **Authentication > Sign In / Providers > Google**: switch it **on**, paste the Client ID and the Client secret, **Save**.
2. **Authentication > URL Configuration > Redirect URLs**: add these two:
   - `http://127.0.0.1:*/**`   (the Windows game: a tiny listener on the player's own computer)
   - `https://cabaldanjehoshaphat.github.io/LAKBAY-SALITA---PHILIPPINE-LANGUAGE-2D-LEARNING-GAME-/callback.html`   (the browser version, see 3)
3. Check it: `https://qislgffezdzjolyofvsh.supabase.co/auth/v1/settings` must show `"google": true` under `external`. (The game checks this itself.)

## 3. The callback page of the browser version (GitHub Pages, free)
The browser game opens Google in a pop-up. When the player is done, Supabase sends the pop-up to a page that hands the login back to the game and
closes. That page is `WebAuth/callback.html` in this project; it must be public on the internet. In a terminal inside the project folder:

```
git switch --orphan gh-pages
git rm -rf . --quiet
git checkout sub -- WebAuth/callback.html
git mv WebAuth/callback.html callback.html
git commit -m "Callback page for Log in using Gmail"
git push origin gh-pages
git switch sub
```
Then on GitHub: repository **Settings > Pages > Build and deployment > Source: Deploy from a branch > gh-pages / (root) > Save**. After a minute the page is at
`https://cabaldanjehoshaphat.github.io/LAKBAY-SALITA---PHILIPPINE-LANGUAGE-2D-LEARNING-GAME-/callback.html`
(the same address as in `CloudSave.WebCallbackUrl`). If you use another address, change that constant and the Supabase redirect URL.
The page stores nothing and sends nothing anywhere except back to the game window that opened it.

## 4. Test
- **Windows / editor:** Profile > **Log in using Gmail**. The web browser opens, choose the Gmail account, the tab says "You are signed in", and the game shows
  "Signed in as ...@gmail.com". On another device: log in with the same Gmail and the progress comes back.
- **Browser version:** rebuild WebGL (Tools > Build), upload the zip to itch.io, press **Log in using Gmail**. Allow the pop-up if the browser asks.

## Notes
- **Forgot password** does not exist any more: nobody types a password in the game. If a player loses access to the Gmail account, Google handles that.
- Only the Gmail address is saved with the progress (plus the progress itself). Say so on the itch.io page and add a short privacy note.
- **Log out account** forgets the login on that device (the progress stays on the device). **Delete my online data** removes the online copy.
- The older username / password code is still in `CloudSave.cs` (SignIn, SignUp) but no screen uses it any more.
