from __future__ import annotations

import base64
import hashlib
import hmac
import json
import os
import time


ITERATIONS = 310_000


def hash_password(password: str) -> str:
    salt = os.urandom(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, ITERATIONS)
    return f"pbkdf2_sha256${ITERATIONS}${_b64(salt)}${_b64(digest)}"


def verify_password(password: str, stored: str) -> bool:
    try:
        algorithm, iterations, salt, expected = stored.split("$", 3)
        if algorithm != "pbkdf2_sha256":
            return False
        actual = hashlib.pbkdf2_hmac(
            "sha256", password.encode(), _unb64(salt), int(iterations)
        )
        return hmac.compare_digest(actual, _unb64(expected))
    except (ValueError, TypeError):
        return False


def issue_token(user_id: int, role: str, secret: bytes, ttl_seconds: int = 86_400) -> str:
    payload = {"sub": user_id, "role": role, "exp": int(time.time()) + ttl_seconds}
    body = _b64(json.dumps(payload, separators=(",", ":")).encode())
    signature = _b64(hmac.new(secret, body.encode(), hashlib.sha256).digest())
    return f"{body}.{signature}"


def read_token(token: str, secret: bytes) -> dict | None:
    try:
        body, signature = token.split(".", 1)
        expected = hmac.new(secret, body.encode(), hashlib.sha256).digest()
        if not hmac.compare_digest(expected, _unb64(signature)):
            return None
        payload = json.loads(_unb64(body))
        return payload if int(payload["exp"]) >= int(time.time()) else None
    except (ValueError, KeyError, TypeError, json.JSONDecodeError):
        return None


def _b64(value: bytes) -> str:
    return base64.urlsafe_b64encode(value).rstrip(b"=").decode()


def _unb64(value: str) -> bytes:
    return base64.urlsafe_b64decode(value + "=" * (-len(value) % 4))

