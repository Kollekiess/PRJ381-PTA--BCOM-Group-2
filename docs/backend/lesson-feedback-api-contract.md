# Lesson and feedback API

My part handles lesson delivery and feedback results. It uses the Express backend,
Roebin's Supabase login, and Tiaan's existing database tables. Gesture recognition
and the Unity interface stay with their assigned team members.

## Setup

Use Node.js 22 or newer. From the repository root:

```powershell
cd BackEnd
npm ci
Copy-Item .env.example .env
```

Put the project's `SUPABASE_URL` and `SUPABASE_ANON_KEY` in `BackEnd/.env`.
Keep the real file out of Git. The backend does not need a service-role key.

The database needs the existing `20260908_schema.sql` migration and the new
`20261011000000_lesson_feedback.sql` migration, applied in that order by the
database owner. The new migration adds policies and a transaction function; it
does not replace the tables or lesson records. Apply it once through the team's
normal migration process. Existing live policies still need to be checked for
compatibility, since permissive policies can combine with these policies.

```powershell
npm test
npm start
```

The server uses port 3000 unless `PORT` is set. It loads `.env` from `BackEnd`
even when started from another folder. Missing settings give a setup message.

## Login

`POST /api/auth/register` and `POST /api/auth/login` use Roebin's implementation.
Login takes `{ "email": "...", "password": "..." }` and returns a `session`.
Use `session.access_token` in this header for every lesson/feedback call:

```text
Authorization: Bearer YOUR_ACCESS_TOKEN
```

Use a confirmed test account if the Supabase project requires email confirmation.
The backend verifies the token with Supabase and uses that user's ID. Each request
gets its own Supabase client so learner tokens do not get mixed together.

## Endpoints

| Method | Route | Result |
| --- | --- | --- |
| GET | `/api/lessons` | `{ "lessons": [...] }`, ordered by difficulty and title |
| GET | `/api/lessons/{lessonId}` | One lesson, or 404 |
| GET | `/api/lessons/{lessonId}/progress` | The logged-in learner's progress |
| POST | `/api/feedback` | Saved attempt, feedback and current progress |

Lesson IDs are database UUIDs. A lesson contains `id`, `title`, `category`,
`difficulty`, `targetGestures` and `createdAt`. Database values such as `Letters_A`
and `Letter_B` are returned as `A` and `B`, matching the classifier labels.
Word identifiers such as `Thank_You` are preserved.

The database has no demonstration-asset field yet, so the response does not invent
an animation or video URL. Unity must currently choose the matching reference asset.

Progress contains `lessonId`, `completed`, `highestScore` and `lastAttempted`.
Before the first attempt these are false, 0 and null. The API reads the existing
completion value; it does not decide that one successful sign finishes a whole
lesson. The team still needs to define its lesson-completion rule.

## Submit feedback

The gesture engine's `FeedbackResult` supplies `target`, `predicted`, `confidence`,
`target_score`, `correct`, `message` and `hint`. Add the selected lesson ID,
an attempt ID and the time:

```json
{
  "attemptId": "33333333-3333-4333-8333-333333333333",
  "lessonId": "11111111-1111-4111-8111-111111111111",
  "target": "A",
  "predicted": "A",
  "confidence": 0.9,
  "target_score": 0.9,
  "correct": true,
  "message": "Correct!",
  "hint": "",
  "attemptedAt": "2026-10-11T12:00:00Z"
}
```

Use a real lesson ID from `GET /api/lessons`. `attemptId` is optional, but clients
should generate a UUID for each new attempt and reuse it, with the same saved
fields, if a network request needs retrying. The backend generates an ID if omitted.

Scores must be numbers between 0 and 1. `target_score` cannot exceed the prediction
confidence; if the target is the predicted sign, their scores must agree. A result
marked correct must predict the target. The target must belong to the selected
lesson. The timestamp must be a valid ISO date/time with a timezone. A request
cannot supply a user ID. Optional message/hint values are limited to 300 characters.

The backend returns 201 for a new attempt or 200 for an identical persisted retry:

```json
{
  "attemptId": "33333333-3333-4333-8333-333333333333",
  "gestureId": "A",
  "predicted": "A",
  "confidence": 0.9,
  "target_score": 0.9,
  "correct": true,
  "feedbackMessage": "Correct!",
  "hint": "",
  "updatedProgress": {
    "lessonId": "11111111-1111-4111-8111-111111111111",
    "completed": false,
    "highestScore": 90,
    "lastAttempted": "2026-10-11T12:00:00Z"
  }
}
```

The backend does not run recognition again. It accepts the recognition component's
result and returns its message and hint. Confidence is not a measured accuracy
percentage. To use the existing schema, `user_sessions.accuracy` stores
`target_score * 100`, and `user_lessons.highest_score` stores the best such attempt
score for that lesson. This is not the overall lesson success rate.

The database function creates a learner's profile if missing, records the attempt
and updates progress in one transaction. An error rolls back all three changes.
Lower scores and older attempts cannot reduce the best score or move the last
attempt time backwards. Existing completion is preserved.

The current session table cannot store predicted sign, correctness, message or
hint, so those fields are returned but not persisted. A retry checks the saved
lesson, target, score and timestamp. Use the same complete payload on retries.

## Errors

Errors follow the existing backend format: `{ "error": "message" }`.

| Status | Meaning |
| --- | --- |
| 400 | Invalid JSON, ID or feedback fields |
| 401 | Missing, expired or invalid login token |
| 404 | Lesson or route not found |
| 409 | An attempt ID was reused with different saved data |
| 413 | Request larger than 32 KB |
| 503 | Login or database service unavailable, or configuration missing |

Database details and keys are not returned to the client.

## Testing and Unity handoff

`npm test` covers the HTTP routes, validation, authentication failures, user
isolation, retries and progress. A PostgreSQL test runs both migrations and checks
the actual permissions, transaction rollback and score updates. The tests use
local fixtures, not the team's hosted database.

Import `postman/BCSignVR-Lesson-Feedback.postman_collection.json`. Set `email` and
`password` to a test account. Login stores the access token; listing lessons stores
a lesson ID. The feedback request creates an attempt ID and timestamp once so
resending it tests retry handling. Clear both collection variables for a new attempt.

For the final integration check, login against the real project, retrieve a lesson,
submit a real recognizer result, then reload progress. Test a wrong attempt too.
Unity needs to send the bearer token and display the returned feedback/hint.
On a separate headset or phone, use the backend computer's reachable address
rather than `localhost`, and test that network connection before the demo.

Hosted Supabase and Unity/headset integration have not been verified yet. A live
check needs the team's settings, migration deployment and test account.

## Files

| File | Purpose |
| --- | --- |
| `BackEnd/server.js` | Loads settings and starts the server |
| `BackEnd/app.js` | Mounts auth/API routes and handles request errors |
| `BackEnd/supabase.js` | Creates a client using the learner's token |
| `BackEnd/lessons.js` | Validates requests and returns lessons/feedback/progress |
| `BackEnd/lesson-store.js` | Reads the existing tables and calls the save function |
| `BackEnd/.env.example` | Settings template |
| `BackEnd/test/` | Route, client and PostgreSQL tests |
| `supabase/migrations/20261011000000_lesson_feedback.sql` | Policies and atomic save function |
| `BackEnd/package.json` and lockfile | Run/test commands and test dependency |
| `docs/backend/openapi/` | Current endpoint/response specification |
| `docs/backend/postman/` | Manual requests for the Unity handoff |
| `scripts/validate-backend-contract.ps1` | Checks the API documents |
