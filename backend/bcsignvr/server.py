from __future__ import annotations

import json
import math
import os
import re
from dataclasses import dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse

from .database import Database
from .security import hash_password, issue_token, read_token, verify_password


EMAIL = re.compile(r"^[^\s@]+@[^\s@]+\.[^\s@]+$")
CLIENT_SESSION_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$")
DEFAULT_DB = Path(__file__).resolve().parents[1] / "data" / "bcsignvr.sqlite3"


@dataclass
class App:
    database: Database
    secret: bytes


class Handler(BaseHTTPRequestHandler):
    app: App
    server_version = "BCSignVR/0.1"

    def do_OPTIONS(self):
        self._send(204, None)

    def do_GET(self):
        route = urlparse(self.path).path
        if route == "/health":
            return self._send(200, {"status": "ok", "database": "sqlite"})
        if route == "/api/lessons":
            rows = self.app.database.all(
                "SELECT id,title,category,difficulty,description,gesture_labels,content_status,source_url FROM lessons ORDER BY id"
            )
            return self._send(200, {"lessons": [self._lesson(row) for row in rows]})
        if route == "/api/progress":
            user = self._require_user()
            if not user:
                return
            rows = self.app.database.all(
                """SELECT s.id,s.client_id,s.lesson_id,l.title,s.accuracy,s.duration_seconds,s.mistakes,s.completed,s.input_source,s.created_at
                   FROM sessions s JOIN lessons l ON l.id=s.lesson_id
                   WHERE s.user_id=? ORDER BY s.created_at DESC LIMIT 50""",
                (user["sub"],),
            )
            completed = sum(int(row["completed"]) for row in rows)
            average = sum(float(row["accuracy"]) for row in rows) / len(rows) if rows else 0
            return self._send(
                200,
                {
                    "summary": {"attempts": len(rows), "completed": completed, "averageAccuracy": round(average, 4)},
                    "sessions": [dict(row) for row in rows],
                },
            )
        return self._send(404, {"error": "Route not found"})

    def do_POST(self):
        route = urlparse(self.path).path
        body = self._read_json()
        if body is None:
            return
        if route == "/api/auth/register":
            name = str(body.get("name", "")).strip()
            email = str(body.get("email", "")).strip().lower()
            password = str(body.get("password", ""))
            if not name or not EMAIL.match(email) or len(password) < 8:
                return self._send(400, {"error": "Name, valid email, and an 8+ character password are required"})
            try:
                user_id = self.app.database.execute(
                    "INSERT INTO users(name,email,password_hash) VALUES (?,?,?)",
                    (name[:80], email, hash_password(password)),
                )
            except Exception as error:
                if "UNIQUE" in str(error):
                    return self._send(409, {"error": "Email already registered"})
                raise
            token = issue_token(user_id, "learner", self.app.secret)
            return self._send(201, {"token": token, "user": {"id": user_id, "name": name, "email": email, "role": "learner"}})
        if route == "/api/auth/login":
            email = str(body.get("email", "")).strip().lower()
            password = str(body.get("password", ""))
            row = self.app.database.one("SELECT id,name,email,password_hash,role FROM users WHERE email=?", (email,))
            if not row or not verify_password(password, row["password_hash"]):
                return self._send(401, {"error": "Invalid email or password"})
            token = issue_token(row["id"], row["role"], self.app.secret)
            return self._send(200, {"token": token, "user": {key: row[key] for key in ("id", "name", "email", "role")}})
        if route == "/api/sessions":
            user = self._require_user()
            if not user:
                return
            client_id = body.get("id")
            if client_id is not None and (not isinstance(client_id, str) or not CLIENT_SESSION_ID.fullmatch(client_id)):
                return self._send(400, {"error": "Session id must be a 1-128 character identifier"})
            lesson_id = str(body.get("lessonId", ""))
            lesson = self.app.database.one("SELECT id FROM lessons WHERE id=?", (lesson_id,))
            if not lesson:
                return self._send(400, {"error": "Unknown lesson"})
            try:
                accuracy = float(body.get("accuracy", -1))
                duration = float(body.get("durationSeconds", -1))
                mistakes = int(body.get("mistakes", 0))
            except (TypeError, ValueError, OverflowError):
                return self._send(400, {"error": "Invalid session metrics"})
            if not math.isfinite(accuracy) or not math.isfinite(duration) or not 0 <= accuracy <= 1 or duration < 0 or mistakes < 0:
                return self._send(400, {"error": "Session metrics are outside allowed ranges"})
            input_source = str(body.get("inputSource", "unknown")).strip()
            if not input_source or len(input_source) > 40:
                return self._send(400, {"error": "inputSource must be a non-empty short label"})
            session_id, created = self.app.database.save_session(
                user["sub"], client_id, lesson_id, accuracy, duration, mistakes,
                int(bool(body.get("completed", True))), input_source
            )
            return self._send(201 if created else 200, {"id": session_id, "saved": True, "replayed": not created})
        return self._send(404, {"error": "Route not found"})

    def _require_user(self):
        header = self.headers.get("Authorization", "")
        token = header[7:] if header.startswith("Bearer ") else ""
        payload = read_token(token, self.app.secret)
        if not payload:
            self._send(401, {"error": "A valid bearer token is required"})
        return payload

    def _read_json(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if length < 0:
                self._send(400, {"error": "Invalid request length"})
                return None
            if length > 1_000_000:
                self._send(413, {"error": "Request is too large"})
                return None
            body = json.loads(self.rfile.read(length) or b"{}")
            if not isinstance(body, dict):
                self._send(400, {"error": "Expected a JSON object"})
                return None
            return body
        except (ValueError, json.JSONDecodeError):
            self._send(400, {"error": "Expected valid JSON"})
            return None

    def _send(self, status, payload):
        body = b"" if payload is None else json.dumps(payload, separators=(",", ":")).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "Authorization, Content-Type")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.end_headers()
        if body:
            self.wfile.write(body)

    @staticmethod
    def _lesson(row):
        lesson = dict(row)
        lesson["gesture_labels"] = json.loads(lesson["gesture_labels"])
        return lesson

    def log_message(self, format, *args):
        print(f"{self.address_string()} - {format % args}")


def create_server(host="127.0.0.1", port=8787, database_path=DEFAULT_DB, secret=None):
    app = App(Database(database_path), secret or os.environ.get("BCSIGNVR_SECRET", "local-development-secret-change-me").encode())
    handler = type("ConfiguredHandler", (Handler,), {"app": app})
    return ThreadingHTTPServer((host, port), handler)


def main():
    server = create_server(host=os.environ.get("BCSIGNVR_HOST", "127.0.0.1"), port=int(os.environ.get("BCSIGNVR_PORT", "8787")))
    print(f"BCSignVR API listening on http://{server.server_address[0]}:{server.server_address[1]}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()

