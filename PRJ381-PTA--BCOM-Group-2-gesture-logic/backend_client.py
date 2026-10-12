"""Send accepted recognizer results to the lesson API using the learner's login."""

import json
import time
from concurrent.futures import ThreadPoolExecutor
from dataclasses import asdict
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, HTTPRedirectHandler
from uuid import UUID, uuid4


class BackendError(Exception):
    def __init__(self, message, status=None):
        super().__init__(message)
        self.status = status


class NoRedirects(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class BackendClient:
    def __init__(self, base_url, token=None, timeout=3):
        url = urlsplit(base_url)
        if url.scheme not in ('http', 'https') or not url.hostname or url.username or url.password or url.query or url.fragment:
            raise BackendError('Use a backend http(s) address without credentials or query parameters.')
        self.base_url = base_url.rstrip('/')
        self.token = token
        self.timeout = timeout
        self.opener = build_opener(NoRedirects())

    def _request(self, method, path, body=None, authenticated=True):
        headers = {'Content-Type': 'application/json'}
        if authenticated:
            if not self.token:
                raise BackendError('Login is required.', 401)
            headers['Authorization'] = 'Bearer ' + self.token
        request = Request(self.base_url + path, data=None if body is None else json.dumps(body, allow_nan=False).encode(),
                          headers=headers, method=method)
        try:
            with self.opener.open(request, timeout=self.timeout) as response:
                return json.load(response)
        except HTTPError as error:
            error.close()
            messages = {400: 'Request rejected. Check the lesson and feedback fields.',
                        401: 'Login expired or invalid. Log in again.',
                        404: 'Lesson or API route not found.',
                        409: 'Attempt ID conflicts with a saved result.'}
            raise BackendError(messages.get(error.code, 'Backend request failed.'), error.code) from None
        except (URLError, TimeoutError, OSError):
            raise BackendError('Could not reach the backend.') from None
        except (ValueError, UnicodeError):
            raise BackendError('Backend returned an invalid response.') from None

    def login(self, email, password):
        response = self._request('POST', '/api/auth/login', {'email': email, 'password': password}, authenticated=False)
        self.token = (response.get('session') or {}).get('access_token')
        if not self.token:
            raise BackendError('Login did not return an access token.')

    def lessons(self):
        return self._request('GET', '/api/lessons')['lessons']

    def lesson(self, lesson_id):
        return self._request('GET', '/api/lessons/' + str(UUID(lesson_id)))

    def progress(self, lesson_id):
        return self._request('GET', '/api/lessons/' + str(UUID(lesson_id)) + '/progress')

    def save_feedback(self, payload):
        # A timeout may happen after the database commits. Keep the entire payload
        # unchanged so retrying cannot create another attempt.
        for attempt in range(3):
            try:
                return self._request('POST', '/api/feedback', payload)
            except BackendError as error:
                transient = error.status is None or error.status == 429 or error.status >= 500
                if not transient or attempt == 2:
                    raise
                time.sleep(0.25 * (attempt + 1))


def feedback_payload(lesson_id, result):
    payload = asdict(result)
    payload.update(lessonId=str(UUID(lesson_id)), attemptId=str(uuid4()),
                   attemptedAt=datetime.now(timezone.utc).isoformat(timespec='milliseconds'))
    return payload


def practice_pool(labels, requested, lesson=None):
    available = set(labels)
    if lesson is not None:
        available &= set(lesson['targetGestures'])
    pool = list(dict.fromkeys(letter.upper() for letter in requested)) if requested else [letter for letter in labels if letter in available]
    if not pool or any(letter not in available for letter in pool):
        raise BackendError('Choose trained letters that belong to the selected lesson. This model cannot practise word lessons.')
    return pool


class FeedbackSender:
    """Keep network calls off the camera loop and retain any unsaved attempts."""
    def __init__(self, client, lesson_id):
        self.client = client
        self.lesson_id = lesson_id
        self.worker = ThreadPoolExecutor(max_workers=1)
        self.pending = []

    def submit(self, result):
        payload = feedback_payload(self.lesson_id, result)
        self.pending.append((payload, self.worker.submit(self._save, payload)))

    def _save(self, payload):
        try:
            response = self.client.save_feedback(payload)
            print(f"Saved {payload['target']}: best score {response['updatedProgress']['highestScore']:g}")
            return True
        except BackendError as error:
            print(f"Attempt was not saved: {error}")
            return False

    def close(self, directory='data'):
        self.worker.shutdown(wait=True)
        failed = []
        for payload, future in self.pending:
            try:
                saved = future.result()
            except Exception:
                saved = False
            if not saved:
                failed.append(payload)
        if failed:
            path = Path(directory) / (str(uuid4()) + '.feedback-pending.json')
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(failed, indent=2), encoding='utf-8')
            print(f'{len(failed)} unsaved attempt(s) kept in {path}. No login token is stored.')
        print(f'Backend saved {len(self.pending) - len(failed)} of {len(self.pending)} attempts.')
        return len(failed)
