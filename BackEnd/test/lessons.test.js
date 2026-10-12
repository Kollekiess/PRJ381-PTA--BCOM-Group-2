const { test } = require('node:test');
const assert = require('node:assert/strict');
const express = require('express');
const { createApp } = require('../app');

const LESSON = '11111111-1111-4111-8111-111111111111';
const OTHER = '22222222-2222-4222-8222-222222222222';
const ATTEMPT = '33333333-3333-4333-8333-333333333333';
const USER_A = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const USER_B = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const lesson = { id: LESSON, title: 'Alphabet', category: 'Letters', difficulty: 1,
    target_gestures: ['Letters_A', 'Letter_B'], created_at: '2026-10-11T12:00:00Z' };

// The API tests use a database double; the SQL transaction is tested separately.
function database() {
    const state = { progress: [], attempts: new Map(), failed: false, unavailableAuth: false };
    function createUserClient(token) {
        const userId = token === 'user-b' ? USER_B : USER_A;
        return {
            auth: { async getUser() {
                if (state.unavailableAuth) throw new Error('Network unavailable');
                return token.startsWith('user-') ? { data: { user: { id: userId } } }
                    : { data: {}, error: { status: 401 } };
            } },
            from(table) {
                const filters = [];
                const query = {
                    select() { return query; },
                    order() { return query; },
                    eq(key, value) { filters.push([key, value]); return query; },
                    async maybeSingle() { return result(true); },
                    then(resolve, reject) { return Promise.resolve(result(false)).then(resolve, reject); }
                };
                function result(single) {
                    if (state.failed) return { data: null, error: { message: 'private database details' } };
                    const rows = table === 'lessons' ? [lesson] : state.progress;
                    const matches = rows.filter(row => filters.every(([key, value]) => row[key] === value));
                    return { data: single ? matches[0] || null : matches, error: null };
                }
                return query;
            },
            async rpc(name, args) {
                assert.equal(name, 'record_lesson_feedback');
                if (state.failed) return { error: { message: 'private database details' } };
                const old = state.attempts.get(args.p_attempt_id);
                if (old && (old.userId !== userId || JSON.stringify(old.args) !== JSON.stringify(args))) {
                    return { error: { code: '22023' } };
                }
                state.attempts.set(args.p_attempt_id, { userId, args });
                let progress = state.progress.find(row => row.user_id === userId && row.lesson_id === args.p_lesson_id);
                if (!progress) {
                    progress = { user_id: userId, lesson_id: args.p_lesson_id, completed: false, highest_score: 0 };
                    state.progress.push(progress);
                }
                progress.highest_score = Math.max(progress.highest_score, args.p_score);
                progress.last_attempted = args.p_attempted_at;
                return { data: { attemptId: args.p_attempt_id, duplicate: !!old, progress } };
            }
        };
    }
    return { state, createUserClient };
}

async function setup(t) {
    const db = database();
    const authRoutes = express.Router();
    authRoutes.post('/login', (req, res) => res.json({ session: { access_token: 'user-a' } }));
    const app = createApp({ authRoutes, createUserClient: db.createUserClient });
    const server = app.listen(0, '127.0.0.1');
    await new Promise(resolve => server.once('listening', resolve));
    t.after(() => new Promise(resolve => server.close(resolve)));
    const base = `http://127.0.0.1:${server.address().port}`;
    async function request(path, { token = 'user-a', method = 'GET', body, raw } = {}) {
        const headers = {};
        if (token) headers.Authorization = `Bearer ${token}`;
        if (body !== undefined || raw !== undefined) headers['Content-Type'] = 'application/json';
        const response = await fetch(base + path, {
            method, headers, body: raw ?? (body === undefined ? undefined : JSON.stringify(body))
        });
        return { status: response.status, body: await response.json() };
    }
    return { ...db, request };
}

function feedback(changes = {}) {
    return { attemptId: ATTEMPT, lessonId: LESSON, target: 'A', predicted: 'A',
        confidence: 0.9, target_score: 0.9, correct: true, message: 'Correct!', hint: '',
        attemptedAt: '2026-10-11T12:00:00Z', ...changes };
}

test('auth router is mounted and lessons normalize database letter names', async t => {
    const { request } = await setup(t);
    assert.equal((await request('/api/auth/login', { method: 'POST', body: {} })).status, 200);
    const list = await request('/api/lessons');
    assert.equal(list.status, 200);
    assert.deepEqual(list.body.lessons[0].targetGestures, ['A', 'B']);
    const detail = await request(`/api/lessons/${LESSON}`);
    assert.equal(detail.body.category, 'Letters');
    assert.equal(detail.body.difficulty, 1);
});

test('missing and expired tokens cannot read or write learner data', async t => {
    const { request, state } = await setup(t);
    for (const token of [null, 'expired']) {
        assert.equal((await request('/api/lessons', { token })).status, 401);
        assert.equal((await request('/api/feedback', { token, method: 'POST', body: feedback() })).status, 401);
    }
    assert.equal(state.attempts.size, 0);
});

test('unknown lessons return 404 and malformed IDs return 400', async t => {
    const { request } = await setup(t);
    assert.equal((await request(`/api/lessons/${OTHER}`)).status, 404);
    assert.equal((await request(`/api/lessons/${OTHER}/progress`)).status, 404);
    assert.equal((await request('/api/lessons/not-an-id')).status, 400);
    assert.equal((await request('/api/feedback', { method: 'POST', body: feedback({ lessonId: OTHER }) })).status, 404);
});

test('a new learner gets empty progress, then saves and reloads a real result', async t => {
    const { request, state } = await setup(t);
    const before = await request(`/api/lessons/${LESSON}/progress`);
    assert.deepEqual(before.body, { lessonId: LESSON, completed: false, highestScore: 0, lastAttempted: null });
    const saved = await request('/api/feedback', { method: 'POST', body: feedback() });
    assert.equal(saved.status, 201);
    assert.equal(saved.body.correct, true);
    assert.equal(saved.body.updatedProgress.highestScore, 90);
    assert.equal(saved.body.feedbackMessage, 'Correct!');
    assert.equal(state.attempts.get(ATTEMPT).userId, USER_A);
    const after = await request(`/api/lessons/${LESSON}/progress`);
    assert.equal(after.body.highestScore, 90);
});

test('progress is scoped to the verified user and cannot use a body user ID', async t => {
    const { request } = await setup(t);
    await request('/api/feedback', { method: 'POST', body: feedback() });
    const other = await request(`/api/lessons/${LESSON}/progress`, { token: 'user-b' });
    assert.equal(other.body.highestScore, 0);
    const spoof = await request('/api/feedback', { method: 'POST', body: feedback({ userId: USER_B }) });
    assert.equal(spoof.status, 400);
});

test('wrong attempts return the recognizer hint without lowering best score or clearing completion', async t => {
    const { request, state } = await setup(t);
    await request('/api/feedback', { method: 'POST', body: feedback() });
    state.progress[0].completed = true;
    const wrong = await request('/api/feedback', { method: 'POST', body: feedback({
        attemptId: OTHER, predicted: 'B', correct: false, target_score: 0.1,
        message: 'That looks like B, not A.', hint: 'Curl your thumb more.'
    }) });
    assert.equal(wrong.status, 201);
    assert.equal(wrong.body.correct, false);
    assert.equal(wrong.body.hint, 'Curl your thumb more.');
    assert.equal(wrong.body.updatedProgress.highestScore, 90);
    assert.equal(wrong.body.updatedProgress.completed, true);
});

test('retries do not duplicate attempts and conflicting IDs return 409', async t => {
    const { request, state } = await setup(t);
    await request('/api/feedback', { method: 'POST', body: feedback() });
    assert.equal((await request('/api/feedback', { method: 'POST', body: feedback() })).status, 200);
    assert.equal(state.attempts.size, 1);
    assert.equal((await request('/api/feedback', { method: 'POST', body: feedback({ confidence: 0.8, target_score: 0.8 }) })).status, 409);
    assert.equal((await request('/api/feedback', { token: 'user-b', method: 'POST', body: feedback() })).status, 409);
});

test('invalid feedback and gestures outside the lesson never reach persistence', async t => {
    const { request, state } = await setup(t);
    for (const body of [null, [], {}, feedback({ confidence: 2 }), feedback({ target_score: -1 }),
        feedback({ confidence: '0.9' }), feedback({ correct: 'true' }),
        feedback({ target_score: 0.1 }), feedback({ lessonId: [LESSON] }),
        feedback({ predicted: 'B', correct: true }), feedback({ attemptedAt: '2026-10-11' }),
        feedback({ attemptedAt: '2026-13-99T00:00:00Z' }), feedback({ target: 'Z', predicted: 'Z' }),
        feedback({ attemptedAt: '2026-02-31T00:00:00Z' }),
        feedback({ hint: 'x'.repeat(301) }), feedback({ attemptId: 'invalid' })]) {
        const response = await request('/api/feedback', { method: 'POST', body });
        assert.equal(response.status, 400, JSON.stringify(body));
    }
    assert.equal(state.attempts.size, 0);
});

test('database and authentication outages return controlled errors', async t => {
    const { request, state } = await setup(t);
    state.failed = true;
    const failed = await request('/api/lessons');
    assert.equal(failed.status, 503);
    assert.equal(JSON.stringify(failed.body).includes('private'), false);
    state.failed = false;
    state.unavailableAuth = true;
    assert.equal((await request('/api/lessons')).status, 503);
});

test('invalid JSON and oversized input get useful errors', async t => {
    const { request } = await setup(t);
    assert.equal((await request('/api/feedback', { method: 'POST', raw: '{bad' })).status, 400);
    assert.equal((await request('/api/feedback', { method: 'POST', body: { text: 'x'.repeat(40000) } })).status, 413);
});
