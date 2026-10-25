# Lakbay Salita on itch.io: upload steps and page text

## 1. Make the zip
Unity menu: **Tools > Build > Prepare WebGL settings** (once), **Tools > Build > Switch to WebGL**, **Tools > Build > Build WebGL**
(the result is the folder `Builds/WebGL`), then **Tools > Build > Switch back to Windows**.
Zip the *content* of `Builds/WebGL` so that `index.html` is at the top of the zip. The ready zip of the last build is
`Builds/Lakbay-Salita-WebGL.zip` (about 27 MB).

## 2. Create the page (on itch.io, with your own account)
1. Log in to itch.io, open **Dashboard > Create new project**.
2. **Title:** Lakbay Salita   **Project URL:** lakbay-salita (or any free name)
3. **Kind of project:** HTML.
4. **Uploads:** upload `Lakbay-Salita-WebGL.zip` and tick **This file will be played in the browser**.
5. **Embed options:** Viewport dimensions **1280 x 720**; tick **Fullscreen button**; leave "Mobile friendly" off for now
   (the game is made for a mouse and a keyboard); under "Frame options" you may tick **Automatically start on page load** off
   (a click is needed anyway before the browser plays sound).
6. **Visibility:** set it to **Draft** first, open the page with the private link, play it, then switch to **Public**.
7. Add screenshots (the intro, the Main menu, Wordle, Crossword, Word Search, the Profile) and a cover image 630 x 500.

## 3. Page text (copy and paste)
**Short description:** A cozy word-learning game for Cebuano, Ilonggo and Tagalog.

**Description:**
Lakbay Salita ("Journey through words") helps you learn Cebuano, Ilonggo (Hiligaynon) and Tagalog through play.

- **Wordle-style word guessing** with a short, friendly description of each word
- **Crosswords** in three sizes
- **Word Search** (tap a word in the list to read what it means)
- **Phrasebook** with real sentences, word by word
- **Library** with more than 63,000 words
- A day streak, a profile with 24 chibi avatars, and a Continue button that takes you back to your last puzzle

Your progress is saved in your own browser, on your own computer. Nothing is sent anywhere.

**How to play:** press the Run button, click or press a key to skip the intro, then press Play, choose a game and a language.
Type with your keyboard or click the on-screen keys.

**Note:** this is an early version. The words were chosen from dictionary files and are still being checked by native speakers.
If you find a wrong word, please tell us in the comments!

**Tags:** word, puzzle, educational, language, philippines, cebuano, tagalog, wordle, crossword, casual
**Genre:** Educational (or Puzzle)   **Platforms:** HTML5 (and Windows, later)   **Made with:** Unity

## 4. Things to know
- Saving uses the browser's storage, so a player who clears their browser data loses the progress, and progress does not move
  between browsers or computers.
- The Windows-only parts are hidden in the browser version: the Exit button, the window sizes, and "Choose a picture from your computer".
- The first time a player opens the page, the download is about 27 MB.
- Credits to add on the page: the music and sound effects (check their licences), the dictionary data sources (check their licences).
- "Wordle" is a trademark of The New York Times Company. Consider renaming that mode before the page is public.
