const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { PGlite } = require('@electric-sql/pglite');

const USER_A = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const USER_B = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const ATTEMPT = '33333333-3333-4333-8333-333333333333';
const OTHER = '44444444-4444-4444-8444-444444444444';

test('lesson migration, permissions and save transaction work in PostgreSQL', async t => {
    const db = new PGlite();
    t.after(() => db.close());
    // These fixtures supply the auth schema that hosted Supabase normally creates.
    await db.exec(`
        create role authenticated;
        create role anon;
        create schema auth;
        create table auth.users(id uuid primary key);
        create function auth.uid() returns uuid language sql stable as
        $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
        grant usage on schema auth, public to authenticated, anon;
        insert into auth.users values ('${USER_A}'), ('${USER_B}');
    `);
    const migrations = path.join(__dirname, '../../supabase/migrations');
    await db.exec(await fs.readFile(path.join(migrations, '20260908_schema.sql'), 'utf8'));
    await db.exec(await fs.readFile(path.join(migrations, '20261011000000_lesson_feedback.sql'), 'utf8'));
    const lessonId = (await db.query("select id from lessons where title = 'Alphabet: A to M'")).rows[0].id;
    await db.exec(`set role authenticated; select set_config('request.jwt.claim.sub', '${USER_A}', false);`);

    async function save({ id = ATTEMPT, letter = 'A', score = 90, time = '2026-10-11T12:00:00Z' } = {}) {
        const result = await db.query(
            'select public.record_lesson_feedback($1, $2, $3, $4, $5) as result',
            [id, lessonId, letter, score, time]
        );
        return result.rows[0].result;
    }

    await t.test('authenticated users can read seeded lessons and save progress', async () => {
        assert.equal((await db.query('select * from lessons')).rows.length, 6);
        const saved = await save();
        assert.equal(saved.attemptId, ATTEMPT);
        assert.equal(saved.duplicate, false);
        assert.equal(saved.progress.highest_score, 90);
        assert.equal(saved.progress.completed, false);
        assert.equal((await db.query('select id from profiles')).rows[0].id, USER_A);
        assert.equal((await db.query('select accuracy from user_sessions')).rows[0].accuracy, 90);
    });

    await t.test('retrying an attempt saves exactly one row and rejects changed data', async () => {
        assert.equal((await save()).duplicate, true);
        assert.equal((await db.query('select * from user_sessions')).rows.length, 1);
        await assert.rejects(save({ score: 80 }), error => error.code === '22023');
        assert.equal((await db.query('select highest_score from user_lessons')).rows[0].highest_score, 90);
    });

    await t.test('lower or older results preserve the best score, last attempt and completion', async () => {
        await db.exec('update user_lessons set completed = true');
        const saved = await save({ id: OTHER, score: 30, time: '2026-10-10T12:00:00Z' });
        assert.equal(saved.progress.highest_score, 90);
        assert.equal(saved.progress.completed, true);
        assert.equal(Date.parse(saved.progress.last_attempted), Date.parse('2026-10-11T12:00:00Z'));
    });

    await t.test('invalid scores and unrelated signs are rejected by the database too', async () => {
        await assert.rejects(save({ id: USER_A, score: -1 }), error => error.code === '22023');
        await assert.rejects(save({ id: USER_A, letter: 'Z' }), error => error.code === '22023');
        assert.equal((await db.query('select * from user_sessions')).rows.length, 2);
    });

    await t.test('another learner cannot read, overwrite or reuse the first learner records', async () => {
        await db.exec(`select set_config('request.jwt.claim.sub', '${USER_B}', false);`);
        assert.equal((await db.query('select * from user_sessions')).rows.length, 0);
        assert.equal((await db.query('select * from user_lessons')).rows.length, 0);
        assert.equal((await db.query('update user_lessons set highest_score = 100 returning *')).rows.length, 0);
        await assert.rejects(save(), error => error.code === '22023');
        await assert.rejects(db.query(
            'insert into user_sessions(user_id, lesson_id, gesture_id) values ($1, $2, $3)',
            [USER_A, lessonId, 'A']
        ), error => error.code === '42501');
    });

    await t.test('a progress failure rolls back the attempt and profile together', async () => {
        await db.exec('reset role; alter table user_lessons add constraint test_score_limit check (highest_score < 95); set role authenticated;');
        await assert.rejects(save({ id: USER_B, score: 99 }), error => error.code === '23514');
        assert.equal((await db.query('select * from user_sessions')).rows.length, 0);
        assert.equal((await db.query('select * from profiles')).rows.length, 0);
    });

    await t.test('signed-out callers cannot execute the save function', async () => {
        await db.exec("reset role; set role anon; select set_config('request.jwt.claim.sub', '', false);");
        await assert.rejects(save({ id: USER_B }), error => error.code === '42501');
    });
});
