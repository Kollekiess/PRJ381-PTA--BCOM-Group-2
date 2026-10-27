const path = require('node:path');
const fs = require('node:fs');
const { DatabaseSync } = require('node:sqlite');
const express = require('express');
const { createAuth } = require('./auth');

const app = express();
const PORT = Number(process.env.PORT || 3000);
const dataDir = process.env.DATA_DIR || path.join(__dirname, 'data');
fs.mkdirSync(dataDir, { recursive: true });
const database = new DatabaseSync(path.join(dataDir, 'bcsignvr.sqlite'));
database.exec(`
  PRAGMA foreign_keys = ON;
  PRAGMA journal_mode = WAL;
  CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    email TEXT NOT NULL UNIQUE,
    password_salt TEXT NOT NULL,
    password_hash TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
  );
  CREATE TABLE IF NOT EXISTS lessons (
    id INTEGER PRIMARY KEY,
    title TEXT NOT NULL,
    sign_label TEXT NOT NULL
  );
  CREATE TABLE IF NOT EXISTS user_lessons (
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    lesson_id INTEGER NOT NULL REFERENCES lessons(id),
    best_score INTEGER NOT NULL DEFAULT 0,
    attempts INTEGER NOT NULL DEFAULT 0,
    completed_at TEXT,
    PRIMARY KEY (user_id, lesson_id)
  );
  CREATE TABLE IF NOT EXISTS user_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    lesson_id INTEGER NOT NULL REFERENCES lessons(id),
    score INTEGER NOT NULL,
    completed_at TEXT NOT NULL
  );
  CREATE TABLE IF NOT EXISTS user_stats (
    user_id INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    total_lessons INTEGER NOT NULL DEFAULT 0,
    average_score REAL NOT NULL DEFAULT 0,
    updated_at TEXT NOT NULL
  );
`);
const lessonCount = database.prepare('SELECT COUNT(*) AS count FROM lessons').get().count;
if (!lessonCount) {
  const addLesson = database.prepare('INSERT INTO lessons (id, title, sign_label) VALUES (?, ?, ?)');
  [['Hello', 'HELLO'], ['Thank you', 'THANK YOU'], ['Please', 'PLEASE']].forEach((row, index) => addLesson.run(index + 1, ...row));
}

app.use(express.json({ limit: '20kb' }));
app.use(express.static(path.join(__dirname, 'public')));
app.get('/api/health', (req, res) => res.json({ ok: true, database: 'sqlite', persistent: true }));
app.use(createAuth(database));
const sourceFiles = { auth: path.join(__dirname, 'auth.js'), database: path.join(__dirname, 'server.js') };
app.get('/api/source/:name', (req, res) => {
  const file = sourceFiles[req.params.name];
  if (!file) return res.status(404).json({ error: 'Source file not found.' });
  res.json({ name: path.basename(file), source: fs.readFileSync(file, 'utf8') });
});
app.get('*wildcard', (req, res) => res.sendFile(path.join(__dirname, 'public', 'index.html')));

const server = app.listen(PORT, '127.0.0.1', () => {
  console.log(`BCSignVR learning demo ready at http://127.0.0.1:${PORT}`);
  console.log(`Persistent SQLite database: ${path.join(dataDir, 'bcsignvr.sqlite')}`);
});

function shutdown() {
  server.close(() => {
    database.close();
    process.exit(0);
  });
}
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
