# PRJ381-PTA--BCOM-Group-2

## BCSignVR

Our project uses VR to help learners practise South African Sign Language.
The learner chooses a lesson, sees a sign, tries it and gets feedback.

## My backend work

The lesson and feedback endpoints are in `BackEnd`. They use the existing
Supabase tables and Roebin's authentication routes. Attempts and best scores
are saved together, and progress is linked to the logged-in learner.

From `BackEnd`, run `npm ci`, copy `.env.example` to `.env`, fill in the Supabase
settings, and run `npm start`. Use Node.js 22 or newer. The database owner also
needs to apply the new lesson-feedback migration before saving attempts.

- [Setup, endpoints and testing](docs/backend/lesson-feedback-api-contract.md)
- [OpenAPI specification](docs/backend/openapi/lesson-feedback-api.openapi.json)
- [Postman collection](docs/backend/postman/BCSignVR-Lesson-Feedback.postman_collection.json)

Run `npm test` in `BackEnd` for the local API and database tests. From the
repository root, run `./scripts/validate-backend-contract.ps1` to check the docs.

The local tests pass. The hosted database and Unity still need an integration
check using the team's settings. Lesson completion follows the team's existing
rules; submitting one correct sign does not automatically complete a lesson.
