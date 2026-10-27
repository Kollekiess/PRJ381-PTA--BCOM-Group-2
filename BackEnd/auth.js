const crypto = require('node:crypto');

const sessions = new Map();
const TOKEN_TTL_MS = 7 * 24 * 60 * 60 * 1000;

function hashPassword(password, salt = crypto.randomBytes(16).toString('hex')) {
  const hash = crypto.scryptSync(password, salt, 64).toString('hex');
  return { salt, hash };
}

function verifyPassword(password, salt, expectedHash) {
  const actual = crypto.scryptSync(password, salt, 64);
  const expected = Buffer.from(expectedHash, 'hex');
  return actual.length === expected.length && crypto.timingSafeEqual(actual, expected);
}

function createAuth(database) {
  function issueToken(userId) {
    const token = crypto.randomBytes(32).toString('base64url');
    sessions.set(token, { userId, expiresAt: Date.now() + TOKEN_TTL_MS });
    return token;
  }

  function requireUser(req, res, next) {
    const token = (req.get('authorization') || '').replace(/^Bearer\s+/i, '');
    const session = sessions.get(token);
    if (!session || session.expiresAt <= Date.now()) {
      if (session) sessions.delete(token);
      return res.status(401).json({ error: 'Please sign in to continue.' });
    }
    req.userId = session.userId;
    next();
  }

  const router = require('express').Router();
  router.post('/api/register', (req, res) => {
    const email = String(req.body.email || '').trim().toLowerCase();
    const password = String(req.body.password || '');
    if (!/^\S+@\S+\.\S+$/.test(email) || password.length < 8) {
      return res.status(400).json({ error: 'Enter a valid email and a password with at least 8 characters.' });
    }
    if (database.prepare('SELECT id FROM users WHERE email = ?').get(email)) {
      return res.status(409).json({ error: 'An account with that email already exists.' });
    }
    const { salt, hash } = hashPassword(password);
    const result = database.prepare('INSERT INTO users (email, password_salt, password_hash) VALUES (?, ?, ?)').run(email, salt, hash);
    const token = issueToken(Number(result.lastInsertRowid));
    return res.status(201).json({ token, user: { id: Number(result.lastInsertRowid), email } });
  });

  router.post('/api/login', (req, res) => {
    const email = String(req.body.email || '').trim().toLowerCase();
    const password = String(req.body.password || '');
    const user = database.prepare('SELECT id, email, password_salt, password_hash FROM users WHERE email = ?').get(email);
    if (!user || !verifyPassword(password, user.password_salt, user.password_hash)) {
      return res.status(401).json({ error: 'Email or password is incorrect.' });
    }
    return res.json({ token: issueToken(user.id), user: { id: user.id, email: user.email } });
  });

  router.post('/api/logout', requireUser, (req, res) => {
    sessions.delete((req.get('authorization') || '').replace(/^Bearer\s+/i, ''));
    res.json({ ok: true });
  });

  router.get('/api/me', requireUser, (req, res) => {
    const user = database.prepare('SELECT id, email, created_at FROM users WHERE id = ?').get(req.userId);
    res.json({ user });
  });

  router.get('/api/lessons', requireUser, (req, res) => {
    const lessons = database.prepare(`
      SELECT l.id, l.title, l.sign_label,
        COALESCE(ul.best_score, 0) AS best_score,
        COALESCE(ul.attempts, 0) AS attempts,
        ul.completed_at
      FROM lessons l LEFT JOIN user_lessons ul ON ul.lesson_id = l.id AND ul.user_id = ?
      ORDER BY l.id
    `).all(req.userId);
    res.json({ lessons });
  });

  router.post('/api/lessons/:id/complete', requireUser, (req, res) => {
    const lessonId = Number(req.params.id);
    const score = Number(req.body.score);
    const lesson = database.prepare('SELECT id FROM lessons WHERE id = ?').get(lessonId);
    if (!lesson || !Number.isFinite(score) || score < 0 || score > 100) {
      return res.status(400).json({ error: 'Choose a lesson and a score from 0 to 100.' });
    }
    const now = new Date().toISOString();
    database.exec('BEGIN IMMEDIATE');
    try {
      database.prepare(`INSERT INTO user_lessons (user_id, lesson_id, best_score, attempts, completed_at)
        VALUES (?, ?, ?, 1, ?)
        ON CONFLICT(user_id, lesson_id) DO UPDATE SET
          best_score = MAX(user_lessons.best_score, excluded.best_score),
          attempts = user_lessons.attempts + 1, completed_at = excluded.completed_at`).run(req.userId, lessonId, score, now);
      database.prepare('INSERT INTO user_sessions (user_id, lesson_id, score, completed_at) VALUES (?, ?, ?, ?)').run(req.userId, lessonId, score, now);
      database.prepare(`INSERT INTO user_stats (user_id, total_lessons, average_score, updated_at)
        VALUES (?, 1, ?, ?) ON CONFLICT(user_id) DO UPDATE SET
        total_lessons = user_stats.total_lessons + 1,
        average_score = (SELECT AVG(score) FROM user_sessions WHERE user_id = excluded.user_id),
        updated_at = excluded.updated_at`).run(req.userId, score, now);
    database.exec('COMMIT');
    } catch (error) {
      database.exec('ROLLBACK');
      throw error;
    }
    res.status(201).json({ ok: true, lessonId, score, completedAt: now });
  });

  router.get('/api/progress', requireUser, (req, res) => {
    const stats = database.prepare('SELECT total_lessons, ROUND(average_score, 1) AS average_score FROM user_stats WHERE user_id = ?').get(req.userId) || { total_lessons: 0, average_score: 0 };
    const recent = database.prepare(`SELECT l.title, s.score, s.completed_at
      FROM user_sessions s JOIN lessons l ON l.id = s.lesson_id
      WHERE s.user_id = ? ORDER BY s.id DESC LIMIT 5`).all(req.userId);
    res.json({ stats, recent });
  });

  return router;
}

module.exports = { createAuth };
