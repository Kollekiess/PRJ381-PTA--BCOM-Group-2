import json
import sqlite3
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from pathlib import Path

from bcsignvr.server import create_server
from bcsignvr.database import Database


class ApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory()
        cls.server = create_server(port=0, database_path=Path(cls.temp.name) / "test.sqlite3", secret=b"test-secret")
        cls.base = f"http://127.0.0.1:{cls.server.server_address[1]}"
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.temp.cleanup()

    def request(self, method, path, payload=None, token=None):
        body = json.dumps(payload).encode() if payload is not None else None
        headers = {"Content-Type": "application/json"}
        if token:
            headers["Authorization"] = f"Bearer {token}"
        request = urllib.request.Request(self.base + path, data=body, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request) as response:
                return response.status, json.loads(response.read() or b"{}")
        except urllib.error.HTTPError as error:
            return error.code, json.loads(error.read())

    def test_complete_user_journey(self):
        status, registered = self.request("POST", "/api/auth/register", {"name": "Test Learner", "email": "learner@example.org", "password": "correct-horse"})
        self.assertEqual(201, status)
        token = registered["token"]

        status, lessons = self.request("GET", "/api/lessons")
        self.assertEqual(200, status)
        self.assertGreaterEqual(len(lessons["lessons"]), 30)
        self.assertIn("alphabet-a", {lesson["id"] for lesson in lessons["lessons"]})

        status, saved = self.request("POST", "/api/sessions", {"lessonId": "prototype-open", "accuracy": 0.92, "durationSeconds": 12.4, "mistakes": 1, "completed": True, "inputSource": "webcam"}, token)
        self.assertEqual(201, status)
        self.assertTrue(saved["saved"])

        status, progress = self.request("GET", "/api/progress", token=token)
        self.assertEqual(200, status)
        self.assertEqual(1, progress["summary"]["completed"])
        self.assertAlmostEqual(0.92, progress["summary"]["averageAccuracy"])
        self.assertEqual("webcam", progress["sessions"][0]["input_source"])

    def test_rejects_short_password_and_unauthenticated_progress(self):
        status, _ = self.request("POST", "/api/auth/register", {"name": "A", "email": "a@example.org", "password": "short"})
        self.assertEqual(400, status)
        status, _ = self.request("GET", "/api/progress")
        self.assertEqual(401, status)

    def register(self, email):
        status, registered = self.request("POST", "/api/auth/register", {"name": "Queue Tester", "email": email, "password": "correct-horse"})
        self.assertEqual(201, status)
        return registered["token"]

    def test_retried_session_id_is_saved_once_and_scoped_to_the_learner(self):
        first_user = self.register("retry-first@example.org")
        second_user = self.register("retry-second@example.org")
        attempt = {"id": "offline-attempt-123", "lessonId": "prototype-open", "accuracy": .8,
                   "durationSeconds": 12, "mistakes": 1, "completed": True}
        status, first = self.request("POST", "/api/sessions", attempt, first_user)
        self.assertEqual(201, status)
        self.assertFalse(first["replayed"])
        status, retried = self.request("POST", "/api/sessions", {**attempt, "accuracy": 1}, first_user)
        self.assertEqual(200, status)
        self.assertEqual(first["id"], retried["id"])
        self.assertTrue(retried["saved"])
        self.assertTrue(retried["replayed"])
        _, progress = self.request("GET", "/api/progress", token=first_user)
        self.assertEqual(1, progress["summary"]["attempts"])
        self.assertEqual(.8, progress["sessions"][0]["accuracy"])
        self.assertEqual(attempt["id"], progress["sessions"][0]["client_id"])
        status, other = self.request("POST", "/api/sessions", attempt, second_user)
        self.assertEqual(201, status)
        self.assertNotEqual(first["id"], other["id"])

    def test_reference_alphabet_attempt_can_be_synced(self):
        token = self.register("alphabet@example.org")
        status, saved = self.request("POST", "/api/sessions", {
            "id": "alphabet-attempt-a",
            "lessonId": "alphabet-a",
            "accuracy": .81,
            "durationSeconds": 6.0,
            "mistakes": 1,
            "completed": True,
            "inputSource": "webcam-sasl-research",
        }, token)
        self.assertEqual(201, status)
        self.assertTrue(saved["saved"])

    def test_invalid_session_ids_nonfinite_metrics_and_nonobject_json_are_rejected(self):
        token = self.register("validation@example.org")
        attempt = {"lessonId": "prototype-open", "accuracy": .8, "durationSeconds": 12}
        for session_id in ("", [], 42, "x" * 129, "contains spaces"):
            with self.subTest(session_id=session_id):
                status, _ = self.request("POST", "/api/sessions", {**attempt, "id": session_id}, token)
                self.assertEqual(400, status)
        for metric in ("accuracy", "durationSeconds"):
            for value in (float("nan"), float("inf"), float("-inf")):
                with self.subTest(metric=metric, value=value):
                    status, _ = self.request("POST", "/api/sessions", {**attempt, metric: value}, token)
                    self.assertEqual(400, status)
        for body in ([], "not an object", 123):
            with self.subTest(body=body):
                status, _ = self.request("POST", "/api/sessions", body, token)
                self.assertEqual(400, status)
        _, progress = self.request("GET", "/api/progress", token=token)
        self.assertEqual(0, progress["summary"]["attempts"])


class DatabaseMigrationTests(unittest.TestCase):
    def test_existing_database_migrates_without_losing_session_history(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "old.sqlite3"
            database = Database(path)
            user_id = database.execute("INSERT INTO users(name,email,password_hash) VALUES (?,?,?)", ("Legacy", "legacy@example.org", "unused"))
            database.save_session(user_id, None, "prototype-open", .7, 8, 0, 1, "webcam")
            # Recreate the old sessions schema with its original row, without client_id.
            with sqlite3.connect(path) as connection:
                connection.executescript("""
                    DROP INDEX idx_sessions_user_client;
                    ALTER TABLE sessions DROP COLUMN client_id;
                """)
            migrated = Database(path)
            self.assertEqual(1, len(migrated.all("SELECT * FROM sessions")))
            self.assertEqual("webcam", migrated.one("SELECT input_source FROM sessions")["input_source"])
            saved_id, created = migrated.save_session(user_id, "new-offline-id", "prototype-open", .9, 9, 0, 1)
            self.assertTrue(created)
            retry_id, created = migrated.save_session(user_id, "new-offline-id", "prototype-open", .9, 9, 0, 1)
            self.assertFalse(created)
            self.assertEqual(saved_id, retry_id)
            self.assertEqual(2, len(migrated.all("SELECT * FROM sessions")))


if __name__ == "__main__":
    unittest.main()

