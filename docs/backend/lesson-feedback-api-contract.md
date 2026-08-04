# Lesson Delivery and Feedback API Contract

Owner: Dewald Allers  
Status: Provisional contract; implementation blocked by missing shared source

## Purpose

This document defines the smallest useful contract for Dewald's assigned
backend work:

1. Deliver lessons to the Unity client.
2. Accept the result of a gesture comparison.
3. Return understandable feedback.
4. Connect to the team's existing authentication, session, progress, and
   database code once those components are available.

The routes and fields below are proposals derived from the current project
brief. They are not final database schemas and must be reconciled with the
shared implementation before production code is added.

## Ownership boundaries

### Dewald owns

- Lesson-delivery routes and controller/service logic.
- Feedback request validation and response formatting.
- Calling existing lesson, progress, authentication, and session interfaces.
- API tests and Unity-facing request/response documentation.

### Dewald does not own

- Authentication or token creation.
- Session lifecycle or session logging infrastructure.
- MongoDB connection management or final collection schemas.
- Hand-tracking capture or gesture-comparison algorithms.
- Unity UI implementation.

## Proposed endpoints

| Method | Route | Purpose | Authentication |
| --- | --- | --- | --- |
| `GET` | `/api/lessons` | List lessons available to the learner | Confirm with Roebin |
| `GET` | `/api/lessons/{lessonId}` | Deliver one lesson and its demonstration information | Confirm with Roebin |
| `GET` | `/api/lessons/{lessonId}/progress` | Return the authenticated learner's progress | Required |
| `POST` | `/api/feedback` | Save a comparison result and return feedback | Required |

If the shared backend already uses different route names, these routes must be
renamed to match it rather than introducing a second convention.

## Proposed data contract

### Lesson

The draft lesson response contains only information needed to select and
deliver a lesson:

```json
{
  "id": "lesson-id",
  "title": "SASL Letter A",
  "difficulty": "beginner",
  "sign": "A",
  "instructions": "Follow the demonstrated hand position.",
  "demonstrationReference": "asset-reference"
}
```

The final names and optional fields must come from Tiaan's lesson structure and
the Unity client's requirements. Expected gesture data should not be exposed to
the client unless the gesture team explicitly requires it.

### Feedback request

The backend should accept the output of the gesture-comparison component, not
perform hand-tracking or gesture recognition itself:

```json
{
  "lessonId": "lesson-id",
  "gestureId": "A",
  "accuracy": 85,
  "errors": ["Thumb position incorrect"],
  "attemptedAt": "2026-08-04T12:00:00Z"
}
```

Rules:

- `lessonId`, `gestureId`, `accuracy`, and `attemptedAt` are required in this
  draft.
- `accuracy` is proposed as a number from 0 to 100.
- `errors` is optional and defaults to an empty list.
- The authenticated user ID must come from authentication middleware. The API
  must not trust a client-supplied user ID.
- Session identification should use Roebin's session interface rather than a
  duplicate session field or collection.

### Feedback response

```json
{
  "correct": true,
  "accuracy": 85,
  "feedbackMessage": "Good attempt. Adjust your thumb position.",
  "lessonCompleted": true,
  "updatedProgress": {
    "lessonId": "lesson-id",
    "completed": true,
    "bestAccuracy": 85,
    "attemptCount": 1
  }
}
```

The threshold used for `correct` and `lessonCompleted` must come from the
lesson/comparison design. It must not be hard-coded until the team agrees on a
rule.

## Validation and errors

The eventual implementation should use the backend's existing error format.
Until that exists, the draft format is:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "The feedback request is invalid.",
    "details": ["accuracy must be between 0 and 100"]
  }
}
```

Expected status codes:

| Status | Meaning |
| --- | --- |
| `200` | Lesson or progress returned successfully |
| `201` | Feedback attempt accepted and recorded |
| `400` | Request validation failed |
| `401` | Authentication is missing or invalid |
| `404` | Lesson does not exist |
| `500` | Unexpected server or database failure |

## Minimum test cases

1. List lessons successfully.
2. Deliver an existing lesson.
3. Return `404` for an unknown lesson.
4. Return progress for the authenticated learner only.
5. Accept a valid gesture-comparison result.
6. Reject missing required feedback fields with `400`.
7. Reject an accuracy below 0 or above 100.
8. Reject protected requests without authentication.
9. Record progress and session information through existing team interfaces.
10. Return a controlled error when data access fails.

## Integration checklist

Before implementation, inspect the shared code and replace every provisional
decision below with repository evidence:

- [ ] Confirm backend language, framework, and folder conventions.
- [ ] Confirm route prefix and API versioning convention.
- [ ] Map `Lesson` to Tiaan's actual model or data-access result.
- [ ] Map progress updates to Tiaan's actual data-access interface.
- [ ] Use Roebin's authentication middleware and authenticated-user property.
- [ ] Use Roebin's session logging interface.
- [ ] Match Zander's actual gesture-comparison output.
- [ ] Match Unity's serialization and response requirements.
- [ ] Reuse the backend's validation and error-handling utilities.
- [ ] Add tests using the repository's chosen test framework.

## Definition of done

Dewald's part is complete when:

- Unity can retrieve a lesson through the backend.
- An authenticated gesture attempt can be submitted.
- The attempt is validated and passed to existing data/session interfaces.
- Unity receives stable feedback and progress data.
- Failure cases return consistent errors.
- Automated tests and API usage examples pass.

