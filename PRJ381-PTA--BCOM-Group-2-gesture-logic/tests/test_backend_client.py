import json
import os
import subprocess
import tempfile
import threading
import unittest
from dataclasses import dataclass, replace
from pathlib import Path
from unittest.mock import patch

from backend_client import BackendClient, BackendError, FeedbackSender, feedback_payload, practice_pool


@dataclass
class Result:
    target: str = 'A'
    predicted: str = 'A'
    confidence: float = 0.9
    target_score: float = 0.9
    correct: bool = True
    message: str = 'Correct!'
    hint: str = ''


class ClientTests(unittest.TestCase):
    def test_pool_is_limited_to_trained_lesson_targets(self):
        lesson = {'targetGestures': ['A', 'B', 'Thank_You']}
        self.assertEqual(practice_pool(['A', 'B', 'C'], [], lesson), ['A', 'B'])
        for requested in [['C'], ['unknown']]:
            with self.assertRaises(BackendError):
                practice_pool(['A', 'B', 'C'], requested, lesson)
        with self.assertRaises(BackendError):
            practice_pool(['A'], [], {'targetGestures': ['Thank_You']})

    def test_transient_retry_keeps_id_timestamp_and_result(self):
        client = BackendClient('http://127.0.0.1', 'token')
        payload = feedback_payload('11111111-1111-4111-8111-111111111111', Result())
        with patch.object(client, '_request', side_effect=[BackendError('unavailable', 503), {'saved': True}]) as request, patch('backend_client.time.sleep'):
            self.assertEqual(client.save_feedback(payload), {'saved': True})
        self.assertEqual(request.call_count, 2)
        self.assertIs(request.call_args_list[0].args[2], request.call_args_list[1].args[2])

    def test_invalid_login_does_not_retry(self):
        client = BackendClient('http://127.0.0.1', 'expired')
        with patch.object(client, '_request', side_effect=BackendError('expired', 401)) as request:
            with self.assertRaises(BackendError):
                client.save_feedback({})
            self.assertEqual(request.call_count, 1)

    def test_saving_is_background_and_failed_payload_is_kept_without_token(self):
        client = BackendClient('http://127.0.0.1', 'secret-token')
        started, release = threading.Event(), threading.Event()
        def save(payload):
            started.set()
            release.wait(5)
            raise BackendError('offline')
        with patch.object(client, 'save_feedback', side_effect=save):
            sender = FeedbackSender(client, '11111111-1111-4111-8111-111111111111')
            sender.submit(Result())
            self.assertTrue(started.wait(2))
            self.assertFalse(sender.pending[0][1].done())
            release.set()
            with tempfile.TemporaryDirectory() as directory:
                self.assertEqual(sender.close(directory), 1)
                text = next(Path(directory).glob('*.feedback-pending.json')).read_text()
                self.assertNotIn('secret-token', text)
                self.assertEqual(json.loads(text)[0], sender.pending[0][0])

    def test_rejects_addresses_with_embedded_credentials(self):
        for url in ['ftp://localhost', 'http://user:password@localhost', 'http://localhost?token=abc']:
            with self.assertRaises(BackendError):
                BackendClient(url)


class ApiIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        fixture = Path(__file__).resolve().parents[2] / 'BackEnd' / 'test-support' / 'gesture-api.cjs'
        cls.server = subprocess.Popen([os.getenv('NODE_BINARY', 'node'), str(fixture)], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        cls.base = cls.server.stdout.readline().strip()
        if not cls.base.startswith('http://'):
            cls.server.terminate()
            raise RuntimeError('Integration server did not start: ' + cls.server.stderr.read())

    @classmethod
    def tearDownClass(cls):
        cls.server.terminate()
        cls.server.communicate(timeout=10)

    def test_login_lesson_correct_wrong_retry_and_reloaded_postgres_progress(self):
        client = BackendClient(self.base)
        with self.assertRaises(BackendError):
            client.lessons()
        client.login('learner@example.test', 'test-password')
        lesson = next(item for item in client.lessons() if item['title'] == 'Alphabet: A to M')
        self.assertEqual(client.lesson(lesson['id'])['targetGestures'][0], 'A')
        self.assertEqual(client.progress(lesson['id'])['highestScore'], 0)
        payload = feedback_payload(lesson['id'], Result())
        correct = client.save_feedback(payload)
        self.assertEqual(correct['updatedProgress']['highestScore'], 90)
        self.assertEqual(client.save_feedback(payload)['attemptId'], correct['attemptId'])
        wrong = replace(Result(), predicted='B', target_score=0.1, correct=False, message='That looks like B, not A.', hint='Curl your thumb more.')
        response = client.save_feedback(feedback_payload(lesson['id'], wrong))
        self.assertFalse(response['correct'])
        self.assertEqual(response['hint'], wrong.hint)
        self.assertEqual(client.progress(lesson['id'])['highestScore'], 90)
        with self.assertRaises(BackendError) as conflict:
            client.save_feedback({**payload, 'confidence': 0.8, 'target_score': 0.8})
        self.assertEqual(conflict.exception.status, 409)
        expired = BackendClient(self.base, 'expired')
        with self.assertRaises(BackendError) as auth:
            expired.save_feedback(payload)
        self.assertEqual(auth.exception.status, 401)


if __name__ == '__main__':
    unittest.main()
