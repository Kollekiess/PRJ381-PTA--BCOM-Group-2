# PRJ381-PTA--BCOM-Group-2

## BCSignVR

BCSignVR is our VR project for learning South African Sign Language. A user
will choose a lesson, watch a sign, try the gesture and receive feedback on
their attempt.

## My backend work

I am working on the lesson and feedback endpoints. The main backend and
database code have not been added yet, so I prepared drafts that can be updated
once the rest of the project is available.

- [Lesson and feedback API contract](docs/backend/lesson-feedback-api-contract.md)
- [OpenAPI draft](docs/backend/openapi/lesson-feedback-api.openapi.json)
- [Postman collection](docs/backend/postman/BCSignVR-Lesson-Feedback.postman_collection.json)
- [Simple validation script](scripts/validate-backend-contract.ps1)

To check the draft files, run:

```powershell
./scripts/validate-backend-contract.ps1
```

The routes and fields are not final yet. I will match them to the team's actual
backend, authentication and database code once it has been pushed.
