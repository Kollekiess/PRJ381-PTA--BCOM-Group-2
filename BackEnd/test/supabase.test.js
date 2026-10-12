const { test } = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const { createUserClient } = require('../supabase');

test('Supabase requests keep each learner token separate and use the current schema', async t => {
    const requests = [];
    const server = http.createServer(async (req, res) => {
        let body = '';
        for await (const chunk of req) body += chunk;
        requests.push({ url: req.url, token: req.headers.authorization, body: body ? JSON.parse(body) : null });
        res.setHeader('Content-Type', 'application/json');
        if (req.url === '/auth/v1/user') {
            res.end(JSON.stringify({ id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', email: 'test@example.invalid' }));
        } else if (req.url.startsWith('/rest/v1/lessons')) {
            res.end('[]');
        } else {
            res.end(JSON.stringify({ attemptId: '33333333-3333-4333-8333-333333333333', duplicate: false, progress: {} }));
        }
    });
    server.listen(0, '127.0.0.1');
    await new Promise(resolve => server.once('listening', resolve));
    const oldUrl = process.env.SUPABASE_URL;
    const oldKey = process.env.SUPABASE_ANON_KEY;
    process.env.SUPABASE_URL = `http://127.0.0.1:${server.address().port}`;
    process.env.SUPABASE_ANON_KEY = 'local-test-key';
    t.after(async () => {
        if (oldUrl === undefined) delete process.env.SUPABASE_URL; else process.env.SUPABASE_URL = oldUrl;
        if (oldKey === undefined) delete process.env.SUPABASE_ANON_KEY; else process.env.SUPABASE_ANON_KEY = oldKey;
        await new Promise(resolve => server.close(resolve));
    });

    const a = createUserClient('learner-a');
    const b = createUserClient('learner-b');
    assert.equal((await a.auth.getUser('learner-a')).data.user.email, 'test@example.invalid');
    await b.from('lessons').select('id,title');
    await a.rpc('record_lesson_feedback', { p_gesture_id: 'A', p_score: 90 });
    assert.deepEqual(requests.map(req => req.token), ['Bearer learner-a', 'Bearer learner-b', 'Bearer learner-a']);
    assert.ok(requests[1].url.startsWith('/rest/v1/lessons?select='));
    assert.equal(requests[2].url, '/rest/v1/rpc/record_lesson_feedback');
    assert.deepEqual(requests[2].body, { p_gesture_id: 'A', p_score: 90 });
});
