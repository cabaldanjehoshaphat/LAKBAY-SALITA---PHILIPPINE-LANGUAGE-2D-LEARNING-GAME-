-- ============================================================================================================================
-- Lakbay Salita: cloud save with Supabase
-- Purpose: creates the table that keeps each player's game progress online, so that a player who signs in with their email on a NEW
--          device gets the progress of the old device back. Run this once in the Supabase dashboard: SQL Editor > New query > paste > Run.
--
-- How the pieces fit together
--   * The email accounts live in Supabase Authentication (the built-in table auth.users: id, email, password hash, confirmation state).
--     The game never sees or stores the password; Supabase Auth handles sign up, sign in, "forgot password" and confirmation emails.
--   * The game progress lives in the table public.progress below: ONE row per player, linked to the account by user_id.
--       save   = the entries of the game's save.json as {"key": "value", ...}   (solved puzzles, learned phrases, favorites, streak, ...)
--       player = the player's profile (player_data.json): name, avatar, solved counts, learned words, join date
--   * Row Level Security (RLS) means a signed-in player can read and write ONLY their own row. The "anon / publishable" key that is put in
--     the game is public by design; RLS is what keeps other players' rows private.
--   * NEVER put the "service_role" (secret) key in the game. Only the Project URL and the anon / publishable key go into the game.
-- ============================================================================================================================

create table if not exists public.progress (
  user_id    uuid primary key references auth.users (id) on delete cascade,   -- the account; the row is deleted with the account
  save       jsonb not null default '{}'::jsonb,                              -- save.json entries (settings of this device are not included)
  player     jsonb not null default '{}'::jsonb,                              -- player_data.json
  updated_at timestamptz not null default now()                               -- when this row was last written (used to merge)
);

alter table public.progress enable row level security;

-- a player can only see and change their own row
create policy "progress: read own row"   on public.progress for select using (auth.uid() = user_id);
create policy "progress: insert own row" on public.progress for insert with check (auth.uid() = user_id);
create policy "progress: update own row" on public.progress for update using (auth.uid() = user_id) with check (auth.uid() = user_id);
create policy "progress: delete own row" on public.progress for delete using (auth.uid() = user_id);

-- Data API permissions. If "Automatically expose new tables" was turned OFF when the project was created (recommended), the table is not
-- reachable until it is opened on purpose, and only to signed-in players ("authenticated"), never to visitors who are not signed in ("anon").
-- (Harmless if that option was left on.)
revoke all on public.progress from anon;
grant select, insert, update, delete on public.progress to authenticated;

-- keep updated_at correct on every update (the game does not have to send it)
create or replace function public.progress_touch_updated_at()
returns trigger language plpgsql as $$
begin
  new.updated_at = now();
  return new;
end
$$;

drop trigger if exists progress_touch on public.progress;
create trigger progress_touch before update on public.progress
  for each row execute function public.progress_touch_updated_at();

-- Check afterwards (optional): Table Editor should show "progress" with a lock icon ("RLS enabled").
