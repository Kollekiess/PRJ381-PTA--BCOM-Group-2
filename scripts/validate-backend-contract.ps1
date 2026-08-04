$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$openApiPath = Join-Path $repositoryRoot "docs/backend/openapi/lesson-feedback-api.openapi.json"
$postmanPath = Join-Path $repositoryRoot "docs/backend/postman/BCSignVR-Lesson-Feedback.postman_collection.json"

function Assert-ContractValue {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$openApi = Get-Content -Raw -LiteralPath $openApiPath | ConvertFrom-Json
$postman = Get-Content -Raw -LiteralPath $postmanPath | ConvertFrom-Json

Assert-ContractValue ($openApi.openapi -eq "3.0.3") "OpenAPI version must be 3.0.3."
Assert-ContractValue ($null -ne $openApi.paths.'/api/lessons'.get) "Missing GET /api/lessons."
Assert-ContractValue ($null -ne $openApi.paths.'/api/lessons/{lessonId}'.get) "Missing GET /api/lessons/{lessonId}."
Assert-ContractValue ($null -ne $openApi.paths.'/api/lessons/{lessonId}/progress'.get) "Missing GET /api/lessons/{lessonId}/progress."
Assert-ContractValue ($null -ne $openApi.paths.'/api/feedback'.post) "Missing POST /api/feedback."

$feedbackRequired = @($openApi.components.schemas.FeedbackRequest.required)
foreach ($field in @("lessonId", "gestureId", "accuracy", "attemptedAt")) {
    Assert-ContractValue ($feedbackRequired -contains $field) "FeedbackRequest must require '$field'."
}

$requestCount = 0
$feedbackBody = $null
foreach ($folder in $postman.item) {
    $requestCount += @($folder.item).Count

    foreach ($requestItem in $folder.item) {
        if ($requestItem.name -eq "Submit gesture result") {
            $feedbackBody = $requestItem.request.body.raw
        }
    }
}

Assert-ContractValue ($requestCount -eq 4) "Postman collection must contain four draft requests."
Assert-ContractValue ($null -ne $feedbackBody) "Postman collection must include a feedback request body."

$feedbackBody.Replace("{{lessonId}}", "lesson-id") | ConvertFrom-Json | Out-Null

Write-Output "Backend contract validation passed."
Write-Output "Validated OpenAPI paths: 4"
Write-Output "Validated Postman requests: $requestCount"
