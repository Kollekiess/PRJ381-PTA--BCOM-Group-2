create table if not exists public.profiles
(
  id uuid primary key references auth.users(id) on delete cascade,
  username text,
  created_at timestamp with time zone default timezone('utc'::text, now()) not null,
  current_level int default 1
);

create table if not exists public.lessons
(
  id uuid default gen_random_uuid() primary key,
  title text not null,
  category text not null check (category in ('Letters', 'Words', 'Emergency')),
  difficulty int default 1,
  target_gestures jsonb default '[]'::jsonb,
  created_at timestamp with time zone default timezone('utc'::text, now()) not null
);

create table if not exists public.user_lessons
(
  id uuid default gen_random_uuid() primary key,
  user_id uuid references public.profiles(id) on delete cascade not null,
  lesson_id uuid references public.lessons(id) on delete cascade not null,
  completed boolean default false,
  highest_score float default 0.0,
  last_attempted timestamp with time zone default timezone('utc'::text, now()) not null,
  unique(user_id, lesson_id)
);

create table if not exists public.user_stats
(
  user_id uuid references public.profiles(id) on delete cascade primary key,
  total_xp int default 0,
  streak_days int default 0,
  total_practice_time_seconds int default 0,
  last_active_date date default current_date
);

create table if not exists public.user_sessions 
(
  id uuid default gen_random_uuid() primary key,
  user_id uuid references public.profiles(id) on delete cascade not null,
  lesson_id uuid references public.lessons(id) on delete cascade not null,
  gesture_id text not null,
  accuracy float default 0.0,
  attempted_at timestamp with time zone default timezone('utc'::text, now()) not null
);

do $$
begin
  if not exists (
    select 1
    from pg_constraint
    where conname = 'lessons_title_key'
  ) then
    alter table public.lessons
      add constraint lessons_title_key unique (title);
  end if;
end $$;

insert into public.lessons(title, category, difficulty, target_gestures)
values
  ('Alphabet: A to M','Letters', 1, '["Letters_A", "Letter_B", "Letter_C", "Letter_D", "Letter_E", "Letter_F", "Letter_G", "Letter_H", "Letter_I", "Letter_J", "Letter_K", "Letter_L", "Letter_M"]'::jsonb),
  ('Alphabet: N to Z','Letters', 1, '["Letters_N", "Letter_O", "Letter_P", "Letter_Q", "Letter_R", "Letter_S", "Letter_T", "Letter_U", "Letter_V", "Letter_W", "Letter_X", "Letter_Y", "Letter_Z"]'::jsonb),
  ('Essential Questions', 'Words', 2, '["Who", "What", "Where", "When", "Why", "How"]'::jsonb),
  ('Basic Greeting', 'Words', 2,'["Hello", "Goodbye", "Please", "Thank_You", "Nice_To_Meet_You"]'::jsonb),
  ('Medical and Urgent Help', 'Emergency',3,'["Help", "Doctor", "Hurt", "Asthma", "Diabetic", "Medicine"]'::jsonb),
  ('Safety and Authorities', 'Emergency',3,'["Police", "Fire", "Danger", "Safe", "Stop"]'::jsonb)
on conflict (title) do nothing;

alter table public.profiles enable row level security;
alter table public.lessons enable row level security;
alter table public.user_lessons enable row level security;
alter table public.user_stats enable row level security;
alter table public.user_sessions enable row level security;