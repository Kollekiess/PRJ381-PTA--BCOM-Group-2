// Local integration fixture: real Express routes and PostgreSQL migrations,
// with login and Supabase transport replaced by a single test learner.
const { PGlite } = require('@electric-sql/pglite');
const express = require('express');
const fs = require('node:fs/promises');
const path = require('node:path');
const { createApp } = require('../app');

async function start() {
    const db = new PGlite();
    const user = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
    await db.exec(`create role authenticated; create role anon; create schema auth;
        create table auth.users(id uuid primary key);
        create function auth.uid() returns uuid language sql stable as
        $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
        grant usage on schema auth, public to authenticated, anon;
        insert into auth.users values ('${user}');`);
    for (const file of ['20260908_schema.sql', '20261011000000_lesson_feedback.sql']) {
        await db.exec(await fs.readFile(path.join(__dirname, '../../supabase/migrations', file), 'utf8'));
    }
    await db.exec(`set role authenticated; select set_config('request.jwt.claim.sub', '${user}', false);`);
    const client = {
        auth: { getUser: async token => token === 'test-learner' ? { data: { user: { id: user } } } : { error: { status: 401 } } },
        from(table) {
            if (!['lessons', 'user_lessons'].includes(table)) throw new Error('Unexpected table');
            const filters = [];
            const query = {
                select() { return query; }, order() { return query; },
                eq(key, value) { filters.push([key, value]); return query; },
                async maybeSingle() { const result = await execute(); return { ...result, data: result.data?.[0] || null }; },
                then(resolve, reject) { return execute().then(resolve, reject); }
            };
            async function execute() {
                const rows = (await db.query(`select * from ${table}`)).rows;
                return { data: rows.filter(row => filters.every(([key, value]) => row[key] === value)) };
            }
            return query;
        },
        async rpc(name, args) {
            if (name !== 'record_lesson_feedback') throw new Error('Unexpected function');
            try {
                const result = await db.query('select public.record_lesson_feedback($1,$2,$3,$4,$5) as result',
                    [args.p_attempt_id, args.p_lesson_id, args.p_gesture_id, args.p_score, args.p_attempted_at]);
                return { data: result.rows[0].result };
            } catch (error) { return { error: { code: error.code } }; }
        }
    };
    const authRoutes = express.Router();
    authRoutes.post('/login', (req, res) => {
        if (req.body.email !== 'learner@example.test' || req.body.password !== 'test-password') {
            return res.status(400).json({ error: 'invalid credentials' });
        }
        res.json({ session: { access_token: 'test-learner' } });
    });
    const server = createApp({ authRoutes, createUserClient: () => client }).listen(0, '127.0.0.1');
    server.on('listening', () => console.log(`http://127.0.0.1:${server.address().port}`));
}
start().catch(error => { console.error(error); process.exit(1); });
