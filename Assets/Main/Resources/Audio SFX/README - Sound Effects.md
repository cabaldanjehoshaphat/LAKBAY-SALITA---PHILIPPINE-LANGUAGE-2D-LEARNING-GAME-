# Audio SFX — Lakbay Salita sound effects

**Purpose of this folder:** holds the 25 short sound effects for button presses, typing and game events. They live in a
`Resources` folder so code can load them by name without wiring each clip in every scene, for example
`Resources.Load<AudioClip>("Audio SFX/ui_click")`. That's the same way the background music in `Resources/Audio Soundtrack` loads.

**Status:** the **button sounds are connected** (`Scripts/Audio/SoundEffects.cs`): every button in the game plays `ui_click`
(or `ui_back`, `ui_open`, `ui_close`, `hint`, and the Wordle keypad keys `key_tap_1-3`, `key_delete`, `key_enter`), and the main-menu
music chip plays `toggle_on` / `toggle_off`. Not connected yet: typing on a physical keyboard, the game-event sounds (Wordle tile reveal,
word found, win, time up, timer ticks...) and the Sound effects switch in the pause window. The rest of this file describes where
those sounds are meant to go and how they are planned to be connected.

**Where they came from:** synthesized in code by `SFX Ideas/make_sfx.py` at the project root, using the same bamboo
marimba, kulintang gong, bandurria pluck and bell tones as the background music, so music and effects sound like one
game. To change a sound, edit that script, run it, and copy the new `.wav` over the one here (keep the file name).
`SFX Ideas/index.html` is a listening page with a typing demo, a Word Search drag demo and the pause-window switches.

---

## What each sound is for

### Buttons and menus (every screen)
| File | Use |
|---|---|
| `ui_click` | Any button tap: menu cards, Play, Library, category and level cards, Profile |
| `ui_back` | Back arrows and leaving a screen |
| `ui_open` | Opening a panel: pause window, dialogs, word definition popup |
| `ui_close` | Closing a panel or resuming from pause |
| `toggle_on` | A Music or Sound effects switch turned **on** (also the main-menu music chip) |
| `toggle_off` | A Music or Sound effects switch turned **off** |

### Keyboard and keypad (Wordle, Crossword, Word Search)
| File | Use |
|---|---|
| `key_tap_1`, `key_tap_2`, `key_tap_3` | A letter typed, from the on-screen keypad **or** a physical keyboard. Pick one of the three at random each time so fast typing doesn't sound robotic. |
| `key_delete` | Backspace or erasing a letter |
| `key_enter` | Submitting a guess or answer |
| `word_invalid` | Guess is too short or not in the dictionary (plays with the row shake) |

### Wordle
| File | Use |
|---|---|
| `tile_flip` | Each tile turning over during the reveal (follow the reveal stagger in `WordleWordVerifier`) |
| `tile_correct` | Tile turns green: right letter, right place |
| `tile_present` | Tile turns yellow: letter is in the word, wrong place |
| `tile_absent` | Tile turns gray: letter is not in the word |

### Crossword
| File | Use |
|---|---|
| `cw_cell_select` | Tapping or selecting a crossword square |
| `cw_word_complete` | A whole across/down word filled in correctly |

### Word Search
| File | Use |
|---|---|
| `ws_select` | Each new letter while dragging. Raise `AudioSource.pitch` by about 2 semitones per letter (`Mathf.Pow(2, n * 2 / 12f)`) so the selection "climbs". |
| `ws_found` | Selection matches a hidden word |
| `ws_wrong` | Selection released on something that isn't a word |

### All games
| File | Use |
|---|---|
| `hint` | Using a hint |
| `timer_tick` | Once per second during the last 10 seconds of the timer |
| `win_jingle` | Puzzle solved / level complete |
| `time_up` | Timer ran out or no guesses left |

---

## Plan for connecting them (summary)

1. **One sound player for the whole game:** a `SoundEffects` script that creates itself at startup and lives across scenes,
   like `Scripts/Audio/BackgroundMusic.cs`. It loads clips from this folder by name and plays them with
   `AudioSource.PlayOneShot`, so overlapping sounds don't cut each other off. It gets a saved **Sound effects on/off**
   setting (PlayerPrefs `SfxMuted`), separate from the music setting.
2. **Button clicks everywhere:** play `ui_click` for every UI `Button` without editing each scene, by adding a small click hook
   to buttons when a scene loads. Back buttons use `ui_back` instead.
3. **Keyboard and keypad:** call the key sounds from the games' existing input code (for example
   `WordleKeyButton` / `WordleKeyboardTyper` for Wordle, and the matching input scripts in Crossword and Word Search),
   so the on-screen keypad and the physical keyboard make the same sounds.
4. **Game events:** call the game sounds where each event already happens in the game controllers (tile reveal results,
   word found, word complete, hint, last-10-seconds timer, win, time up).
5. **Pause window in every game:** add two switches, **Music** (uses `BackgroundMusic.SetMuted`) and **Sound effects** (uses the
   new `SoundEffects` on/off setting). Each switch plays `toggle_on` / `toggle_off`. Opening and closing the pause window
   plays `ui_open` / `ui_close`.
6. **Import settings** (already applied to the clips here): mono, ADPCM compression, *Decompress On Load*. These are short sounds that need to
   start instantly with no delay, which suits that setting better than streaming.
