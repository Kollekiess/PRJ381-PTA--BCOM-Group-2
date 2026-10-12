# Hand recognition and lesson feedback

Chantel's `AI-models-trained` upload from 7 October is combined into
`feature/dewald-lesson-feedback-api`, preserving her commit. The recognition code
and trained files live in `PRJ381-PTA--BCOM-Group-2-gesture-logic`.
No Unity scenes, assets, packages or project settings were changed for this connection.

## What is connected

`practice.py` can now log in through the Express API, load a selected database
lesson, show its existing best score, and practise its trained letters. Each sign
accepted after the recognizer's hold period sends the actual `FeedbackResult`
to `POST /api/feedback`. Correct and incorrect accepted signs are both saved.
The payload includes the lesson UUID, a new attempt UUID and a UTC timestamp.
The backend uses the login token to identify the learner.

Saving runs on a background worker. Temporary failures retry the same payload
up to three total attempts. Invalid login or input does not retry. The terminal
shows saved scores and failures. On exit, unsaved payloads are kept in a
`data/*.feedback-pending.json` file without the login token; these are ignored by
Git. Recovery must use the same learner account and the original payload, not
generate another attempt ID. There is no automatic cross-session replay.

No hand, an uncertain prediction, a skipped letter or a timeout does not generate
a scored gesture attempt. These still contribute to the existing local session
summary where applicable. Local practice remains available without a backend URL.

The trained classifier covers A–Z using one hand's landmarks. Although tracking
can detect two hands, practice currently evaluates the first detected hand. It
does not recognise word lessons or two-hand/motion sequences. Connected practice
rejects requested letters outside the selected lesson and rejects word-only lessons.
Recognition confidence is not measured accuracy or overall lesson completion.

## Run the connected webcam demo

First the database owner applies the lesson-feedback migration. Configure and
start `BackEnd` as described in [the API setup](lesson-feedback-api-contract.md).
Use a confirmed learner account. In another terminal, from the repository root:

```powershell
cd PRJ381-PTA--BCOM-Group-2-gesture-logic
python -m venv .venv
.venv\Scripts\python.exe -m pip install -r requirements-practice.txt
.venv\Scripts\python.exe practice.py --backend-url http://localhost:3000
```

The script prompts for email/password and a lesson number. The password is hidden
and is not placed in command history or files. Choose an alphabet lesson.
An existing login token can instead be supplied through `BCSIGNVR_ACCESS_TOKEN`;
`BCSIGNVR_EMAIL` and `BCSIGNVR_BACKEND_URL` are optional environment settings.
The Python practice client does not need Supabase keys or a service-role key.
It uses the API rather than the old unused `database/supabase.py` helper.

To select a lesson directly and limit practice to particular letters:

```powershell
.venv\Scripts\python.exe practice.py --backend-url http://localhost:3000 --lesson-id YOUR_LESSON_UUID A B C
```

Use a real UUID from the lesson API and letters within that lesson. Run from this
folder because the existing recognizer resolves its models/data relative to it.
Use the backend computer's reachable address when practice runs on another PC.
Hold a sign for about one second; S skips, Q/Escape ends the session.

The original requirements file omitted TensorFlow and pandas, which the feedback
engine imports. `requirements-practice.txt` provides a separate runtime for this
demo and avoids installing two different OpenCV packages. Training/evaluation
scripts have additional dependencies and are outside this runtime.

## Checks and remaining work

From `BackEnd`, install dependencies with `npm ci` and run `npm test`. From the
recognition folder, run `python -m unittest discover -s tests -v` (Node must be on
PATH). The eight connection tests include the actual Express routes and both
PostgreSQL migrations, with Supabase login/transport replaced by a local test
learner. Camera-loop tests use simulated recognition results.

The eight connection tests and the existing 19 backend tests pass locally.
Actual TensorFlow inference and webcam recognition were not run here: the model
runtime download stalled. The separate practice requirements resolve their
package metadata, but a complete runtime installation remains to be verified.
The selected package releases are listed on [TensorFlow's package page](https://pypi.org/project/tensorflow/2.20.0/),
[MediaPipe's package page](https://pypi.org/project/mediapipe/0.10.35/) and
[OpenCV's package page](https://pypi.org/project/opencv-python/4.12.0.88/).

The live demo still needs the hosted migration, real login and webcam testing.
Her committed evaluation report records 77.45% held-out accuracy, with uneven
results across letters; test the chosen demo letters on the actual webcam.
The new connection does not retrain or alter her classifier or feedback rules.

When the replacement VR project is uploaded, its owner needs to connect lesson
selection, matching sign demonstration assets, hand input, feedback display and
saved progress. This webcam-to-backend demo does not yet deliver recognition
results into a running Unity scene. On-headset recognition needs a separate
decision about the hand input and how to run or reach the Python recognizer.
