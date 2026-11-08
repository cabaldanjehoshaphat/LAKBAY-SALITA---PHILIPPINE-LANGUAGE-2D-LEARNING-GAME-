-- ============================================================================================================================
-- Lakbay Salita: online leaderboards
-- Purpose: creates the tables and database functions behind the Leaderboard tab of the game. Run this once in the Supabase dashboard, after Supabase_Setup.sql:
--          SQL Editor > New query > paste > Run. Running it again is safe.
--
-- How it protects players
--   * Row Level Security is on for every table and no table can be read directly. The game can only call the functions below, and only when signed in,
--     except get_public_board, the view-only board for guests.
--   * A board shows only players who chose "Show me on leaderboards" (off by default), and only their nickname, built-in avatar number, puzzle count and points.
--     The Gmail address is never exposed.
--   * The game never sends a finished score. It sends what happened (time, tries, hints) and score_of works the score out with the same rules as ScoreRules.cs,
--     and refuses impossible values. This blocks impossible results, but a skilled cheater could still send believable ones.
--   * delete_my_leaderboard_data removes everything of the player; the rows also go when the account is deleted.
-- Survival: the survival_runs table and its two functions are ready for the Survival mode, which is not built yet.
-- Classes: the game no longer has a My class tab. The class tables and functions below are unused; they stay so that running this file removes nothing from a live database.
-- If the score rules change, change score_of here and ScoreRules.cs in the game together.
-- ============================================================================================================================

create table if not exists public.player_cards (
  user_id       uuid primary key references auth.users (id) on delete cascade,
  nickname      text not null default 'Player',
  avatar        int  not null default 0,
  show_on_board boolean not null default false,
  updated_at    timestamptz not null default now()
);

create table if not exists public.scores (
  user_id     uuid not null references auth.users (id) on delete cascade,
  game        text not null check (game in ('wordle', 'crossword', 'wordsearch')),
  puzzle      text not null,
  language    smallint not null check (language between 0 and 2),
  week        date not null,                                  -- the Monday of the week; 2000-01-03 = an all-time record whose week is no longer kept
  score       int  not null,
  seconds     int  not null,
  achieved_at timestamptz not null default now(),
  primary key (user_id, game, puzzle, week)
);
create index if not exists scores_board_idx on public.scores (week, game, language);

create table if not exists public.classes (
  code       text primary key,
  name       text not null,
  owner      uuid not null references auth.users (id) on delete cascade,
  created_at timestamptz not null default now()
);

create table if not exists public.class_members (
  user_id    uuid primary key references auth.users (id) on delete cascade,   -- a player is in one class at a time
  class_code text not null references public.classes (code) on delete cascade
);

create table if not exists public.survival_runs (
  user_id        uuid not null references auth.users (id) on delete cascade,
  mode           text not null check (mode in ('free', 'daily')),
  day            date not null,                               -- today for a Daily Voyage; 2000-01-03 for the best free run
  language       smallint not null check (language between -1 and 2),   -- -1 = Journey (all three)
  rounds         int  not null,
  score          int  not null,
  longest_streak int  not null,
  achieved_at    timestamptz not null default now(),
  primary key (user_id, mode, day)
);

-- words that can not be used in a nickname (lower case, matched anywhere in the name); add more with: insert into public.blocked_words values ('word');
create table if not exists public.blocked_words (word text primary key);
insert into public.blocked_words (word) values
  ('admin'), ('moderator'), ('fuck'), ('shit'), ('bitch'), ('putang'), ('gago'), ('tanga'), ('bobo'), ('puta'), ('tarantado'), ('kupal'), ('pakyu')
on conflict do nothing;

alter table public.player_cards  enable row level security;
alter table public.scores        enable row level security;
alter table public.classes       enable row level security;
alter table public.class_members enable row level security;
alter table public.survival_runs enable row level security;
alter table public.blocked_words enable row level security;
-- no policies on purpose: the tables can only be reached through the functions below
revoke all on public.player_cards, public.scores, public.classes, public.class_members, public.survival_runs, public.blocked_words from anon, authenticated;

-- ---------------------------------------------------------------------------------------------------------------- score rules
create or replace function public.score_of(p_game text, p_seconds int, p_seconds_left int, p_tries_left int, p_hints int, p_checks int, p_reveals int, p_words int, p_meaning boolean)
returns int language plpgsql immutable as $$
declare
  total int;
begin
  if p_seconds is null or p_seconds < 0 or p_hints is null or p_hints < 0 then
    return null;
  end if;
  if p_game = 'wordle' then
    if p_seconds < 3 or p_hints > 2 or p_tries_left not between 0 and 6 or p_seconds_left not between 0 and 900 then return null; end if;   -- under 3 seconds is not a real solve
    total := 100 + 20 * p_tries_left + least(60, p_seconds_left / 5) - 15 * p_hints;
  elsif p_game = 'crossword' then
    if p_words not in (5, 8, 11) or p_hints > 3 or p_reveals not between 0 and 1 or p_checks not between 0 and 500 or p_seconds < p_words then return null; end if;
    total := 20 * p_words + greatest(0, least(60, (30 * p_words - p_seconds) / 5)) - 10 * p_hints - 5 * p_checks - 40 * p_reveals;
  elsif p_game = 'wordsearch' then
    if p_words not between 3 and 12 or p_hints > 3 or p_seconds < p_words then return null; end if;
    total := 15 * p_words + greatest(0, least(60, (20 * p_words - p_seconds) / 5)) - 10 * p_hints;
    if p_meaning then
      total := greatest(0, total) * 3 / 2;
    end if;
  else
    return null;
  end if;
  return greatest(0, total);
end
$$;

-- the Monday of the current week, in Philippine time
create or replace function public.current_monday() returns date language sql stable as $$
  select date_trunc('week', now() at time zone 'Asia/Manila')::date
$$;

-- ---------------------------------------------------------------------------------------------------------------- saving scores
create or replace function public.submit_scores(p_rows jsonb)
returns int language plpgsql security definer set search_path = public as $$
declare
  uid uuid := auth.uid();
  r jsonb;
  v_game text; v_puzzle text; v_week date; v_score int; v_seconds int; v_language int;
  saved int := 0;
begin
  if uid is null then
    raise exception 'not_signed_in' using errcode = '28000';
  end if;
  if jsonb_typeof(p_rows) <> 'array' or jsonb_array_length(p_rows) > 1000 then
    raise exception 'too_many_rows';
  end if;
  for r in select * from jsonb_array_elements(p_rows) loop
    v_game := r->>'game';
    v_puzzle := r->>'puzzle';
    if v_puzzle is null or v_puzzle !~ ('^' || coalesce(v_game, '') || ':[0-2]:[0-9]{1,2}:[0-9]{1,2}$') or (r->>'week') !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' then
      continue;
    end if;
    v_week := (r->>'week')::date;
    if extract(isodow from v_week) <> 1 or v_week > public.current_monday() + 7 then
      continue;
    end if;
    v_seconds := (r->>'seconds')::int;
    v_language := split_part(v_puzzle, ':', 2)::int;
    v_score := public.score_of(v_game, v_seconds, (r->>'seconds_left')::int, (r->>'tries_left')::int, (r->>'hints')::int, (r->>'checks')::int, (r->>'reveals')::int, (r->>'words')::int, (r->>'meaning')::boolean);
    if v_score is null then
      continue;
    end if;
    insert into public.scores (user_id, game, puzzle, language, week, score, seconds)
    values (uid, v_game, v_puzzle, v_language, v_week, v_score, v_seconds)
    on conflict (user_id, game, puzzle, week) do update
      set score = excluded.score, seconds = excluded.seconds, achieved_at = now()
      where excluded.score > public.scores.score;
    saved := saved + 1;
  end loop;
  return saved;
end
$$;

-- ---------------------------------------------------------------------------------------------------------------- reading a board
create or replace function public.get_board(p_scope text, p_game text, p_language int, p_class text)
returns table (rank bigint, nickname text, avatar int, puzzles bigint, points bigint, is_me boolean)
language plpgsql security definer set search_path = public stable as $$
declare
  uid uuid := auth.uid();
  v_week date;
begin
  if uid is null then
    raise exception 'not_signed_in' using errcode = '28000';
  end if;
  v_week := case p_scope when 'week' then public.current_monday() when 'last_week' then public.current_monday() - 7 else null end;
  if coalesce(p_class, '') <> '' and not exists (select 1 from public.class_members m where m.user_id = uid and m.class_code = p_class) then
    return;   -- only the members of a class can see its board
  end if;
  return query
  with best as (
    select s.user_id, s.puzzle, max(s.score) as score, max(s.achieved_at) as last_at
    from public.scores s
    where (p_scope = 'all' or s.week = v_week)
      and (p_game = 'all' or s.game = p_game)
      and (p_language < 0 or s.language = p_language)
    group by s.user_id, s.puzzle
  ), totals as (
    select b.user_id, count(*) as puzzles, sum(b.score)::bigint as points, max(b.last_at) as last_at from best b group by b.user_id
  ), ranked as (
    select row_number() over (order by t.points desc, t.last_at asc) as pos, c.user_id, c.nickname, c.avatar, t.puzzles, t.points
    from totals t
    join public.player_cards c on c.user_id = t.user_id and c.show_on_board
    where coalesce(p_class, '') = '' or exists (select 1 from public.class_members m where m.user_id = t.user_id and m.class_code = p_class)
  )
  select r.pos, r.nickname, r.avatar, r.puzzles, r.points, r.user_id = uid
  from ranked r
  where r.pos <= 50 or r.user_id = uid
  order by r.pos;
end
$$;

-- the same board for guests, view-only: the top 50 players who chose to be shown, and nothing that identifies anyone beyond the nickname and avatar
create or replace function public.get_public_board(p_scope text, p_game text, p_language int)
returns table (rank bigint, nickname text, avatar int, puzzles bigint, points bigint, is_me boolean)
language plpgsql security definer set search_path = public stable as $$
declare
  v_week date := case when p_scope = 'week' then public.current_monday() else null end;
begin
  if p_scope not in ('week', 'all') then
    return;
  end if;
  return query
  with best as (
    select s.user_id, s.puzzle, max(s.score) as score, max(s.achieved_at) as last_at
    from public.scores s
    where (p_scope = 'all' or s.week = v_week)
      and (p_game = 'all' or s.game = p_game)
      and (p_language < 0 or s.language = p_language)
    group by s.user_id, s.puzzle
  ), totals as (
    select b.user_id, count(*) as puzzles, sum(b.score)::bigint as points, max(b.last_at) as last_at from best b group by b.user_id
  )
  select row_number() over (order by t.points desc, t.last_at asc), c.nickname, c.avatar, t.puzzles, t.points, false
  from totals t
  join public.player_cards c on c.user_id = t.user_id and c.show_on_board
  order by 1
  limit 50;
end
$$;

-- ---------------------------------------------------------------------------------------------------------------- the public card
create or replace function public.get_my_card()
returns table (nickname text, avatar int, show_on_board boolean)
language sql security definer set search_path = public stable as $$
  select c.nickname, c.avatar, c.show_on_board from public.player_cards c where c.user_id = auth.uid()
$$;

create or replace function public.set_my_card(p_nickname text, p_avatar int, p_show boolean)
returns void language plpgsql security definer set search_path = public as $$
declare
  uid uuid := auth.uid();
  clean text := btrim(coalesce(p_nickname, ''));
begin
  if uid is null then
    raise exception 'not_signed_in' using errcode = '28000';
  end if;
  if char_length(clean) not between 2 and 16 or clean !~ '^[[:alnum:] ._-]+$'
     or exists (select 1 from public.blocked_words b where position(b.word in lower(clean)) > 0) then
    raise exception 'nickname_not_allowed';
  end if;
  insert into public.player_cards (user_id, nickname, avatar, show_on_board)
  values (uid, clean, greatest(0, least(coalesce(p_avatar, 0), 23)), coalesce(p_show, false))
  on conflict (user_id) do update set nickname = excluded.nickname, avatar = excluded.avatar, show_on_board = excluded.show_on_board, updated_at = now();
end
$$;

-- ---------------------------------------------------------------------------------------------------------------- classes
create or replace function public.get_my_class()
returns table (code text, name text, is_owner boolean)
language sql security definer set search_path = public stable as $$
  select c.code, c.name, c.owner = auth.uid() from public.class_members m join public.classes c on c.code = m.class_code where m.user_id = auth.uid()
$$;

create or replace function public.create_class(p_name text)
returns text language plpgsql security definer set search_path = public as $$
declare
  uid uuid := auth.uid();
  v_code text;
  clean text := btrim(coalesce(p_name, ''));
begin
  if uid is null then
    raise exception 'not_signed_in' using errcode = '28000';
  end if;
  if char_length(clean) not between 2 and 30 then
    raise exception 'class_name_not_allowed';
  end if;
  loop
    v_code := upper(substr(md5(random()::text || clock_timestamp()::text), 1, 6));
    exit when not exists (select 1 from public.classes c where c.code = v_code);
  end loop;
  insert into public.classes (code, name, owner) values (v_code, clean, uid);
  insert into public.class_members (user_id, class_code) values (uid, v_code)
  on conflict (user_id) do update set class_code = excluded.class_code;
  return v_code;
end
$$;

create or replace function public.join_class(p_code text)
returns boolean language plpgsql security definer set search_path = public as $$
declare
  uid uuid := auth.uid();
begin
  if uid is null then
    raise exception 'not_signed_in' using errcode = '28000';
  end if;
  if not exists (select 1 from public.classes c where c.code = p_code) then
    return false;
  end if;
  insert into public.class_members (user_id, class_code) values (uid, p_code)
  on conflict (user_id) do update set class_code = excluded.class_code;
  return true;
end
$$;

create or replace function public.leave_class()
returns void language sql security definer set search_path = public as $$
  delete from public.class_members where user_id = auth.uid()
$$;

-- ---------------------------------------------------------------------------------------------------------------- Survival (ready for the mode)
create or replace function public.submit_survival_run(p_mode text, p_language int, p_rounds int, p_score int, p_streak int)
returns void language plpgsql security definer set search_path = public as $$
declare
  uid uuid := auth.uid();
  v_day date := case when p_mode = 'daily' then (now() at time zone 'Asia/Manila')::date else date '2000-01-03' end;
begin
  if uid is null then
    raise exception 'not_signed_in' using errcode = '28000';
  end if;
  -- the most one correct round can give is 50 + 40 seconds left + 5 for each earlier correct answer
  if p_mode not in ('free', 'daily') or p_language not between -1 and 2 or p_rounds not between 0 and 500 or p_streak not between 0 and p_rounds
     or p_score not between 0 and (p_rounds * 90 + 5 * p_rounds * (p_rounds - 1) / 2) then
    raise exception 'run_not_allowed';
  end if;
  if p_mode = 'daily' then
    -- only the first try of the day counts
    insert into public.survival_runs (user_id, mode, day, language, rounds, score, longest_streak)
    values (uid, p_mode, v_day, p_language, p_rounds, p_score, p_streak)
    on conflict (user_id, mode, day) do nothing;
  else
    insert into public.survival_runs (user_id, mode, day, language, rounds, score, longest_streak)
    values (uid, p_mode, v_day, p_language, p_rounds, p_score, p_streak)
    on conflict (user_id, mode, day) do update
      set language = excluded.language, rounds = excluded.rounds, score = excluded.score, longest_streak = excluded.longest_streak, achieved_at = now()
      where excluded.score > public.survival_runs.score;
  end if;
end
$$;

create or replace function public.get_survival_board(p_scope text, p_language int)
returns table (rank bigint, nickname text, avatar int, puzzles bigint, points bigint, is_me boolean)
language plpgsql security definer set search_path = public stable as $$
declare
  uid uuid := auth.uid();
  v_day date := case when p_scope = 'daily' then (now() at time zone 'Asia/Manila')::date else date '2000-01-03' end;
begin
  if uid is null then
    raise exception 'not_signed_in' using errcode = '28000';
  end if;
  return query
  with ranked as (
    select row_number() over (order by s.score desc, s.rounds desc, s.achieved_at asc) as pos, c.user_id, c.nickname, c.avatar, s.rounds::bigint as rounds, s.score::bigint as score
    from public.survival_runs s
    join public.player_cards c on c.user_id = s.user_id and c.show_on_board
    where s.mode = case when p_scope = 'daily' then 'daily' else 'free' end and s.day = v_day and (p_language < 0 or s.language = p_language)
  )
  select r.pos, r.nickname, r.avatar, r.rounds, r.score, r.user_id = uid
  from ranked r
  where r.pos <= 50 or r.user_id = uid
  order by r.pos;
end
$$;

-- ---------------------------------------------------------------------------------------------------------------- removing a player's data
create or replace function public.delete_my_leaderboard_data()
returns void language plpgsql security definer set search_path = public as $$
declare
  uid uuid := auth.uid();
begin
  if uid is null then
    raise exception 'not_signed_in' using errcode = '28000';
  end if;
  delete from public.scores where user_id = uid;
  delete from public.survival_runs where user_id = uid;
  delete from public.class_members where user_id = uid;
  delete from public.classes where owner = uid;
  delete from public.player_cards where user_id = uid;
end
$$;

-- ---------------------------------------------------------------------------------------------------------------- who may call what: signed-in players only
revoke execute on function public.score_of(text, int, int, int, int, int, int, int, boolean), public.current_monday(),
  public.submit_scores(jsonb), public.get_board(text, text, int, text), public.get_my_card(), public.set_my_card(text, int, boolean), public.get_my_class(), public.create_class(text),
  public.join_class(text), public.leave_class(), public.submit_survival_run(text, int, int, int, int), public.get_survival_board(text, int), public.delete_my_leaderboard_data()
from public, anon;
grant execute on function
  public.submit_scores(jsonb), public.get_board(text, text, int, text), public.get_my_card(), public.set_my_card(text, int, boolean), public.get_my_class(), public.create_class(text),
  public.join_class(text), public.leave_class(), public.submit_survival_run(text, int, int, int, int), public.get_survival_board(text, int), public.delete_my_leaderboard_data()
to authenticated;

-- the one function guests may call
revoke execute on function public.get_public_board(text, text, int) from public;
grant execute on function public.get_public_board(text, text, int) to anon, authenticated;

-- make the new functions visible to the game at once
notify pgrst, 'reload schema';
