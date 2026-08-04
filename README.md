# PRJ381-PTA--BCOM-Group-2

BCSignVR is a university group project exploring an immersive VR tutor for
learning South African Sign Language through real-time interaction.

## Dewald's backend work

Dewald owns the proposed lesson-delivery and feedback API contract. The
contract is deliberately framework-independent until the shared backend,
authentication middleware, session logger, MongoDB structures, and Unity API
client are committed.

- [Lesson and feedback API contract](docs/backend/lesson-feedback-api-contract.md)
- [Provisional OpenAPI document](docs/backend/openapi/lesson-feedback-api.openapi.json)
- [Postman collection](docs/backend/postman/BCSignVR-Lesson-Feedback.postman_collection.json)
- [Contract validation script](scripts/validate-backend-contract.ps1)

Run the contract validation from the repository root:

```powershell
./scripts/validate-backend-contract.ps1
```

These artifacts are proposals, not evidence of an implemented backend. Route
names and fields must be reconciled with the team's actual code before the API
is implemented.
