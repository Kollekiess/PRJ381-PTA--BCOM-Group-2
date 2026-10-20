from __future__ import annotations

import json
import sqlite3
from contextlib import contextmanager
from pathlib import Path


SCHEMA = """
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE COLLATE NOCASE,
    password_hash TEXT NOT NULL,
    role TEXT NOT NULL DEFAULT 'learner' CHECK(role IN ('learner','instructor','admin')),
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS lessons (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    category TEXT NOT NULL,
    difficulty TEXT NOT NULL,
    description TEXT NOT NULL,
    gesture_labels TEXT NOT NULL,
    content_status TEXT NOT NULL DEFAULT 'prototype',
    source_url TEXT,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    client_id TEXT,
    lesson_id TEXT NOT NULL REFERENCES lessons(id),
    accuracy REAL NOT NULL CHECK(accuracy >= 0 AND accuracy <= 1),
    duration_seconds REAL NOT NULL CHECK(duration_seconds >= 0),
    mistakes INTEGER NOT NULL DEFAULT 0 CHECK(mistakes >= 0),
    completed INTEGER NOT NULL DEFAULT 1 CHECK(completed IN (0,1)),
    input_source TEXT NOT NULL DEFAULT 'unknown',
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_sessions_user_created ON sessions(user_id, created_at DESC);
"""

SEED_LESSONS = (
    (
        "prototype-open",
        "Tracking Warm-up: Open Hand",
        "tracking",
        "beginner",
        "Validate that all finger joints are visible before beginning a SASL lesson.",
        json.dumps(["DEMO_OPEN"]),
        "prototype",
        None,
    ),
    (
        "prototype-fist",
        "Tracking Warm-up: Closed Hand",
        "tracking",
        "beginner",
        "Validate finger curl tracking. This is a systems check, not a certified SASL sign.",
        json.dumps(["DEMO_FIST"]),
        "prototype",
        None,
    ),
    (
        "prototype-point",
        "Tracking Warm-up: Index Point",
        "tracking",
        "beginner",
        "Validate an isolated index-finger pose before reviewed SASL content is added.",
        json.dumps(["DEMO_POINT"]),
        "prototype",
        None,
    ),
)


def _reference_lesson_seeds():
    """Return stable lesson rows for the local alphabet/reference curriculum."""
    alphabet = [
        (f"alphabet-{letter.lower()}", f"SASL fingerspelling: {letter}",
         "alphabet", "beginner",
         f"Practise the NID SASL fingerspelling reference for letter {letter}.",
         json.dumps([f"TEAM_SASL_{letter}"]), "reference",
         "https://nid.org.za/resources/")
        for letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    ]
    alphabet.append((
        "basic-greeting", "Beginner greeting practice", "phrases", "beginner",
        "Watch the attributed reference and practise the movement. This is visual practice until a reviewed phrase model is available.",
        json.dumps(["REFERENCE_GREETING"]), "reference", "https://nid.org.za/resources/"
    ))
    alphabet.extend((
        ("phrase-fingerspell-hello", "Fingerspell: HELLO", "phrases", "beginner",
         "Practise the letters H-E-L-L-O one at a time. This is a fingerspelling pathway, not a validated SASL word-sign recognizer.",
         json.dumps(["TEAM_SASL_H", "TEAM_SASL_E", "TEAM_SASL_L", "TEAM_SASL_L", "TEAM_SASL_O"]), "reference", "https://nid.org.za/resources/"),
        ("phrase-fingerspell-how-are-you", "Fingerspell: HOW ARE YOU", "phrases", "beginner",
         "Practise the letters in HOW ARE YOU one at a time. This is a fingerspelling pathway, not a validated SASL phrase-sign recognizer.",
         json.dumps(["TEAM_SASL_H", "TEAM_SASL_O", "TEAM_SASL_W", "TEAM_SASL_A", "TEAM_SASL_R", "TEAM_SASL_E", "TEAM_SASL_Y", "TEAM_SASL_O", "TEAM_SASL_U"]), "reference", "https://nid.org.za/resources/"),
    ))
    alphabet.extend(
        (f"research-sasl-{slug}", title, "research", "intermediate",
         "Research movement practice from the University of the Free State collection; regional variation and teaching review are still required.",
         json.dumps([f"RESEARCH_SASL_{slug.upper().replace('-', '_')}"]), "research",
         "https://doi.org/10.38140/ufs.29376494.v2")
        for slug, title in (("cape-town", "Cape Town"), ("bloemfontein", "Bloemfontein"), ("pretoria", "Pretoria"))
    )
    return tuple(alphabet)


class Database:
    def __init__(self, path: str | Path):
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.initialize()

    @contextmanager
    def connect(self):
        connection = sqlite3.connect(self.path, timeout=5)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA foreign_keys = ON")
        try:
            yield connection
            connection.commit()
        finally:
            connection.close()

    def initialize(self):
        with self.connect() as connection:
            connection.executescript(SCHEMA)
            columns = {row["name"] for row in connection.execute("PRAGMA table_info(sessions)")}
            if "client_id" not in columns:
                connection.execute("ALTER TABLE sessions ADD COLUMN client_id TEXT")
            if "input_source" not in columns:
                connection.execute("ALTER TABLE sessions ADD COLUMN input_source TEXT NOT NULL DEFAULT 'unknown'")
            # NULL preserves the existing API for clients that do not send an id.
            connection.execute(
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_sessions_user_client ON sessions(user_id, client_id)"
            )
            connection.executemany(
                """INSERT OR IGNORE INTO lessons
                   (id,title,category,difficulty,description,gesture_labels,content_status,source_url)
                   VALUES (?,?,?,?,?,?,?,?)""",
                SEED_LESSONS + _reference_lesson_seeds(),
            )

    def one(self, sql: str, parameters=()):
        with self.connect() as connection:
            return connection.execute(sql, parameters).fetchone()

    def all(self, sql: str, parameters=()):
        with self.connect() as connection:
            return connection.execute(sql, parameters).fetchall()

    def execute(self, sql: str, parameters=()):
        with self.connect() as connection:
            cursor = connection.execute(sql, parameters)
            return cursor.lastrowid

    def save_session(self, user_id, client_id, lesson_id, accuracy, duration, mistakes, completed,
                     input_source="unknown"):
        """Return (server id, newly created), including safe concurrent upload retries."""
        with self.connect() as connection:
            try:
                cursor = connection.execute(
                    """INSERT INTO sessions
                       (user_id,client_id,lesson_id,accuracy,duration_seconds,mistakes,completed,input_source)
                       VALUES (?,?,?,?,?,?,?,?)""",
                    (user_id, client_id, lesson_id, accuracy, duration, mistakes, completed, input_source),
                )
                return cursor.lastrowid, True
            except sqlite3.IntegrityError:
                if client_id is not None:
                    existing = connection.execute(
                        "SELECT id FROM sessions WHERE user_id=? AND client_id=?", (user_id, client_id)
                    ).fetchone()
                    if existing:
                        return existing["id"], False
                raise

