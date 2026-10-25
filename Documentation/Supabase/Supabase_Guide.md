# Lakbay Salita: email accounts and cloud progress with Supabase

Goal: a player signs up with an email, plays, and later opens the game on **another device** (another computer, or the browser version on
another browser), signs in, and finds the progress again.

## 1. What you create in Supabase (about 15 minutes)

1. Go to **https://supabase.com** and press **Start your project**. Sign up (with GitHub or with an email) and confirm your email.
2. **New organization** (the name does not matter, for example "Lakbay Salita"), plan **Free**.
3. **New project**
   - Name: `lakbay-salita`
   - Database password: choose a strong one and **save it** in a password manager (the game does not need it)
   - Region: the closest one (for the Philippines usually **Southeast Asia (Singapore)**)
   - Press **Create new project** and wait about 2 minutes.
4. **Run the setup SQL:** left menu **SQL Editor > New query**, paste the whole file `Documentation/Supabase/Supabase_Setup.sql`, press **Run**.
   It should say "Success. No rows returned". Under **Table Editor** you should now see the table `progress`.
5. **Sign-in settings:** left menu **Authentication > Providers > Email**
   - Keep **Email** enabled.
   - **Confirm email:** while you test, you can turn it **off** so that accounts work at once. For the public game, leave it **on**.
   - **Authentication > URL Configuration > Site URL:** set it to your game page, `https://jehobc.itch.io/lakbay-salita`.
6. **Copy two values for the game:** left menu **Project Settings > API** (or "API Keys")
   - **Project URL**, like `https://abcdefgh.supabase.co`
   - the **anon / publishable key** (a long text). This key is meant to be public: it is safe inside the game because of the row-level security
     rules in the SQL file.
   - **Do not use or share the `service_role` / secret key.** Never put it in the game, in GitHub, or in a chat.

Then send me the **Project URL** and the **anon / publishable key**, and I will connect the game.

### Important about emails (check this on the Supabase pages when you set up)
Supabase's built-in email sender is meant for testing: it is limited to a few emails per hour, and it may only send to addresses of your own
Supabase team. For real players (confirmation mail and "forgot password" mail) you need your own **SMTP service** (Authentication > SMTP Settings),
for example a free tier of Resend, Brevo or similar. Limits and rules change, so read the current Supabase documentation for "custom SMTP".
Until then, keep **Confirm email** off and offer sign-in with email and password only.

## 2. Where things are stored

| What | Where |
|---|---|
| Email address, password (as a hash), confirmed or not | Supabase **Authentication** (the table `auth.users`; you see it under Authentication > Users) |
| Game progress of that player | table `public.progress`, one row per player (`user_id` links to the account) |
| This device's own settings (window size, music volume) | stay on the device, they are **not** synced |

## 3. What the game does (the scenario)

**First device (old device)**
1. Player opens **Profile > Account**, presses **Create account**, types email and password.
2. The game sends them to Supabase Auth (`/auth/v1/signup`) and gets a **session** (access token + refresh token). The password is not stored in the game.
3. The game uploads the progress (`save.json` entries and `player_data.json`) to the player's row. Later, it uploads again after changes
   (a few seconds after a puzzle is won, and when the player presses **Sync now**).

**New device**
1. Player opens the game, goes to **Profile > Account > Sign in**, and types the same email and password.
2. The game downloads the player's row and **merges** it with whatever this device has, then saves it locally. The Profile, the Continue card,
   the solved counts and the day streak now show the old progress.

**Merge rules** (so nothing is lost when both devices were used):
- Solved puzzles (Wordle, Crossword, Word Search), learned phrases, learned words, favorites: **union** (everything from both).
- Best streak: the **larger** one. Current streak and last play day: the one with the **newer** last play day.
- Name and avatar: from the side that was **changed most recently**.
- Window size, music and sound settings: **not synced**.

**Other buttons:** **Sign out** (forgets the session on this device; the local progress stays), **Forgot password** (Supabase sends a reset mail, needs SMTP),
**Delete my online data** (removes the player's row).

## 4. Game side (what I will build once I have the URL and key)

- `SupabaseClient`: small class using `UnityWebRequest` (works in the Windows game and in WebGL; the official Supabase SDKs are not needed).
  Calls: sign up, sign in with password, refresh the token, password reset, sign out, read my row, write my row.
- `CloudSave`: merges and uploads/downloads using `SaveStore` and `PlayerDatabase`; keeps the session in `SaveStore` keys that are not synced.
- An **Account** window in the Profile screen: email, password (hidden characters), buttons, and a status line ("Signed in as ...", "Saved online
  at 14:32", "No internet: progress is kept on this device").
- The game keeps working offline; it syncs again when the network is back.

## 5. Privacy (please read)

- An email address is personal data. Your players may be students or children. Add a short privacy note on the itch.io page and in the game:
  what you store (email, progress), why (so progress can move to another device), who can see it (only you as the project owner), and how to
  delete it.
- The Philippines' Data Privacy Act (RA 10173) applies. This is not legal advice; ask your teacher or school if you plan to use it in a class.
- The itch.io text "Nothing is sent anywhere" must be changed to say that progress is saved online **only when the player creates an account**.
- Collect only what you need. Do not ask for names, ages or phone numbers.
- Supabase's free projects can be paused after a period of inactivity. Check the current rules in the Supabase documentation.

## 6. Checklist

- [ ] Supabase project created
- [ ] `Supabase_Setup.sql` run, table `progress` visible with RLS on
- [ ] Email provider enabled; Site URL set to the itch.io page
- [ ] Project URL and anon / publishable key sent to me (never the secret key)
- [ ] Decide: email confirmation on or off, and whether SMTP will be set up
- [ ] Privacy note written
