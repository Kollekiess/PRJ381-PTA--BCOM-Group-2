-- Access needed by the lesson API. Existing tables and lesson data stay in place.
grant select on public.lessons to authenticated;
grant select, insert on public.profiles to authenticated;
grant select, insert on public.user_sessions to authenticated;
grant select, insert, update on public.user_lessons to authenticated;

create policy lesson_api_read_lessons on public.lessons
    for select to authenticated using (true);
create policy lesson_api_read_profile on public.profiles
    for select to authenticated using (id = auth.uid());
create policy lesson_api_create_profile on public.profiles
    for insert to authenticated with check (id = auth.uid());
create policy lesson_api_read_sessions on public.user_sessions
    for select to authenticated using (user_id = auth.uid());
create policy lesson_api_create_session on public.user_sessions
    for insert to authenticated with check (user_id = auth.uid());
create policy lesson_api_read_progress on public.user_lessons
    for select to authenticated using (user_id = auth.uid());
create policy lesson_api_create_progress on public.user_lessons
    for insert to authenticated with check (user_id = auth.uid());
create policy lesson_api_update_progress on public.user_lessons
    for update to authenticated using (user_id = auth.uid())
    with check (user_id = auth.uid());

create or replace function public.record_lesson_feedback(
    p_attempt_id uuid,
    p_lesson_id uuid,
    p_gesture_id text,
    p_score double precision,
    p_attempted_at timestamptz
) returns jsonb
language plpgsql
security invoker
set search_path = public
as $$
declare
    learner uuid := auth.uid();
    saved_attempt public.user_sessions%rowtype;
    saved_progress public.user_lessons%rowtype;
    duplicate boolean := false;
begin
    if learner is null then
        raise exception 'Login required' using errcode = '42501';
    end if;
    if p_attempt_id is null or p_attempted_at is null or p_score is null
       or p_score < 0 or p_score > 100 then
        raise exception 'Invalid attempt' using errcode = '22023';
    end if;
    if not exists (
        select 1 from public.lessons l,
        lateral jsonb_array_elements_text(l.target_gestures) as gesture(value)
        where l.id = p_lesson_id and
            case when gesture.value ~* '^(Letters?_)?[A-Z]$'
                then upper(regexp_replace(gesture.value, '^Letters?_', '', 'i'))
                else gesture.value end = p_gesture_id
    ) then
        raise exception 'Gesture is not in this lesson' using errcode = '22023';
    end if;

    -- Supabase signup creates auth.users; this ensures its matching profile exists.
    insert into public.profiles(id) values (learner) on conflict (id) do nothing;

    insert into public.user_sessions(id, user_id, lesson_id, gesture_id, accuracy, attempted_at)
    values (p_attempt_id, learner, p_lesson_id, p_gesture_id, p_score, p_attempted_at)
    on conflict (id) do nothing
    returning * into saved_attempt;

    if saved_attempt.id is null then
        duplicate := true;
        select * into saved_attempt from public.user_sessions
            where id = p_attempt_id and user_id = learner;
        if saved_attempt.id is null
           or saved_attempt.lesson_id <> p_lesson_id
           or saved_attempt.gesture_id <> p_gesture_id
           or saved_attempt.accuracy <> p_score
           or saved_attempt.attempted_at <> p_attempted_at then
            raise exception 'Attempt ID already used' using errcode = '22023';
        end if;
    end if;

    insert into public.user_lessons(user_id, lesson_id, highest_score, last_attempted)
    values (learner, p_lesson_id, p_score, p_attempted_at)
    on conflict (user_id, lesson_id) do update
    set highest_score = greatest(user_lessons.highest_score, excluded.highest_score),
        last_attempted = greatest(user_lessons.last_attempted, excluded.last_attempted)
    returning * into saved_progress;

    -- Completion stays under the team's existing lesson-completion rules.
    return jsonb_build_object(
        'attemptId', saved_attempt.id,
        'duplicate', duplicate,
        'progress', jsonb_build_object(
            'lesson_id', saved_progress.lesson_id,
            'completed', saved_progress.completed,
            'highest_score', saved_progress.highest_score,
            'last_attempted', saved_progress.last_attempted
        )
    );
end;
$$;

revoke all on function public.record_lesson_feedback(uuid, uuid, text, double precision, timestamptz) from public;
grant execute on function public.record_lesson_feedback(uuid, uuid, text, double precision, timestamptz) to authenticated;
