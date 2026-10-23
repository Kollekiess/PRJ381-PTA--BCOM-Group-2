using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;

namespace BCSignVR
{
    public sealed class BCSignVRApp : MonoBehaviour
    {
        public enum InputMode { Camera, VR, Demo }
        enum Page { Home, Practice, Results, Progress, Help, Sasl, Input, SaslPractice, Alphabet, Basics, Fingerspell }
        static readonly string[] LessonIds = { "prototype-open", "prototype-fist", "prototype-point" };
        static readonly string[] Titles = { "Open your hand", "Make a gentle fist", "Point your index finger" };
        static readonly string[] Labels = { "DEMO_OPEN", "DEMO_FIST", "DEMO_POINT" };
        static readonly string[] Instructions = { "Spread all five fingers. Face your palm towards the camera.",
            "Curl your fingers into your palm. Rest your thumb across them.",
            "Keep your index finger straight. Curl the other fingers in." };

        readonly HandPoseFrame current = new();
        readonly GestureEvaluator evaluator = new();
        readonly SimulatedHandPoseProvider simulated = new();
        readonly OpenXRHandPoseProvider openXr = new();
        readonly List<float> sessionScores = new();
        WebcamHandPoseProvider webcam;
        LocalSessionStore history;
        SaslReferenceLibrary references;
        readonly HandPoseFrame researchReference = new();
        int researchIndex;
        float researchStarted;
        bool slowReplay;
        Transform board;
        Transform content;
        TextMesh connectionText, feedbackText, detailsText, matchText, trackingText, demoPoseText;
        TextMesh successCountdown;
        Transform successPrompt;
        Transform successModalRoot;
        Transform progressFill;
        BoardButton nextButton;
        HandSkeletonView targetView, liveView;
        GameObject practiceExamplePicture, practiceLearnerPicture;
        GameObject targetPicture;
        WebcamPreviewView webcamPreview;
        InputMode inputMode = InputMode.Camera;
        Page page;
        int lessonIndex, mistakes;
        float lessonStarted, matchHeld, replayStarted;
        bool lessonCompleted, syncing, savingFailed;
        float successDeadline;
        float successMatch;
        string token, saveStatus = "Saved on this Mac";
        bool apiConnected;
        bool remoteProgressLoaded;
        int remoteCompleted;
        float remoteAverage;
        bool preview;
        TutorAvatar tutor;
        int alphabetIndex;
        int alphabetMistakes;
        TextMesh alphabetStatus;
        string fingerspellPhrase = "HELLO";
        string fingerspellLetters = "HELLO";
        int fingerspellIndex;
        int fingerspellMistakes;
        float fingerspellStarted, fingerspellHeld;
        readonly List<float> fingerspellScores = new();

        void Awake()
        {
            Application.targetFrameRate = 72;
            QualitySettings.antiAliasing = 8;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            preview = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BCSIGNVR_SCREENSHOT"));
            ClassroomFactory.Build();
            tutor = FindFirstObjectByType<TutorAvatar>();
            BuildCamera();
            history = new LocalSessionStore();
            references = new SaslReferenceLibrary();
            webcam = new WebcamHandPoseProvider();
            for (var i = 0; i < Labels.Length; i++)
                evaluator.AddTemplate(Labels[i], SimulatedHandPoseProvider.Template((SimulatedHandPoseProvider.DemoPose)i));
            BuildShell();
            ShowHome();
            StartCoroutine(ConnectBackend());
            if (preview) StartCoroutine(CapturePreview(Environment.GetEnvironmentVariable("BCSIGNVR_SCREENSHOT")));
        }

        void BuildShell()
        {
            ClassroomUi.Panel("Chalkboard frame", null, new Vector3(.65f, 2.65f, 4.96f),
                new Vector2(7.55f, 5.05f), new Color(.13f, .31f, .25f), .28f, true);
            board = ClassroomUi.Panel("Learning board", null, new Vector3(.65f, 2.65f, 4.9f),
                new Vector2(7.2f, 4.7f), Color.white, .26f, true).transform;
            ClassroomUi.Label("BC sign club", board, -3.13f, 1.92f, .28f, ClassroomUi.DeepBlue);
            ClassroomUi.Label("SOUTH AFRICAN SIGN LANGUAGE", board, -3.10f, 1.62f, .105f, ClassroomUi.Muted);
            connectionText = ClassroomUi.Label("Connecting...", board, 3.08f, 1.90f, .125f,
                ClassroomUi.Muted, TextAnchor.MiddleRight, 2.6f);
            ClassroomUi.Panel("Header divider", board, new Vector3(0, 1.40f, -.025f), new Vector2(6.30f, .012f), ClassroomUi.Line, .006f);
            ClassroomUi.Button("Classroom", board, -2.40f, 1.10f, 1.42f, ShowHome);
            ClassroomUi.Button("Explore signs", board, -.84f, 1.10f, 1.42f, ShowSasl);
            ClassroomUi.Button("My progress", board, .76f, 1.10f, 1.52f, ShowProgress);
            ClassroomUi.Button("?  Help", board, 2.56f, 1.10f, 1.12f, ShowHelp);
            var footer = new GameObject("Comfort and controls").transform;
            footer.position = new Vector3(.65f, .04f, 4.5f);
            ClassroomUi.Label("Click to choose  /  Tab + Enter  /  VR: look at a button and pinch", footer,
                0, 0, .12f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 7.3f);
        }

        void BeginPage(Page next)
        {
            page = next;
            if (content != null) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
            if (successModalRoot != null) { Destroy(successModalRoot.gameObject); successModalRoot = null; }
            webcamPreview?.Dispose();
            webcamPreview = null;
            content = new GameObject(next.ToString()).transform; content.SetParent(board, false);
            content.localPosition = new Vector3(0, 0, -.10f);
            targetView = null; liveView = null; feedbackText = detailsText = matchText = trackingText = demoPoseText = null;
            practiceExamplePicture = practiceLearnerPicture = null;
            targetPicture = null;
            successCountdown = null; successPrompt = null;
            progressFill = null; nextButton = null;
            matchHeld = 0;
        }

        void ShowHome()
        {
            BeginPage(Page.Home);
            tutor?.Say("Welcome! Let's learn one hand shape at a time.", true);
            ClassroomUi.Label("A little practice.\nA new way to connect.", content, -3.08f, .28f, .31f,
                ClassroomUi.Ink, TextAnchor.MiddleLeft, 3.85f);
            ClassroomUi.Label("Welcome to your signing classroom.\nWatch Bob's example, then try it at your pace.",
                content, -3.06f, -.34f, .155f, ClassroomUi.Muted, TextAnchor.MiddleLeft, 3.85f);
            var tile = ClassroomUi.Panel("Warm-up lesson", content, new Vector3(-1.05f, -1.13f, 0),
                new Vector2(4.05f, .85f), new Color(.94f, .98f, 1f), .15f, true).transform;
            ClassroomUi.Label("01", tile, -1.74f, .06f, .24f, ClassroomUi.Blue);
            ClassroomUi.Label("SASL alphabet", tile, -1.23f, .15f, .20f, ClassroomUi.Ink);
            ClassroomUi.Label($"{history.AlphabetPractisedCount} / 26 practised  /  one at a time", tile, -1.23f, -.16f, .12f, ClassroomUi.Muted, maxWidth: 2.7f);
            ClassroomUi.Button(history.AlphabetPractisedCount == 0 ? "Start alphabet  >" : "Continue alphabet  >",
                content, -.90f, -1.83f, 2.45f, () => ShowAlphabet(Mathf.Min(history.AlphabetPractisedCount, 25)), true);
            var handCard = ClassroomUi.Panel("Hand welcome", content, new Vector3(2.13f, -.17f, 0),
                new Vector2(1.95f, 1.87f), new Color(.91f, .96f, .99f), .20f, true).transform;
            ClassroomUi.Label("Next: greetings", handCard, 0, .65f, .145f, ClassroomUi.DeepBlue, TextAnchor.MiddleCenter, 1.75f);
            ClassroomUi.Picture("Welcome cartoon hand", "CartoonHandOpen", handCard,
                new Vector3(0, -.02f, -.12f), new Vector2(1.10f, 1.10f));
            ClassroomUi.Button("Preview greeting", handCard, 0, -.64f, 1.58f, ShowGreeting, height: .31f);
            ClassroomUi.Label("Choose your input", content, 1.23f, -1.43f, .13f, ClassroomUi.Muted);
            ClassroomUi.Button(ModeName(), content, 2.13f, -1.82f, 1.95f, CycleMode);
            ClassroomUi.Label("Camera stays on your Mac.", content, 2.13f, -2.14f, .105f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 1.95f);
        }

        void ShowAlphabet(int index)
        {
            alphabetIndex = Mathf.Clamp(index, 0, 25);
            alphabetMistakes = 0;
            lessonStarted = Time.time;
            BeginPage(Page.Alphabet);
            var letter = ((char)('A' + alphabetIndex)).ToString();
            tutor?.Say($"This is letter {letter}. Copy the handshape, then show me.", true);
            ClassroomUi.Label($"Letter {letter}", content, -3.06f, .61f, .29f, ClassroomUi.Ink);
            ClassroomUi.Label($"{alphabetIndex + 1} / 26", content, 3.06f, .61f, .18f, ClassroomUi.Blue, TextAnchor.MiddleRight);
            ClassroomUi.Label("Study the fingers, thumb and palm direction. Keep your whole hand in view.",
                content, -3.06f, .25f, .14f, ClassroomUi.Muted, maxWidth: 6.1f);
            var example = ClassroomUi.Panel("Official alphabet reference", content, new Vector3(-1.67f, -.69f, 0),
                new Vector2(2.83f, 1.52f), new Color(.94f, .98f, 1f), .16f, true).transform;
            var learner = ClassroomUi.Panel("Learner camera hand", content, new Vector3(1.67f, -.69f, 0),
                new Vector2(2.83f, 1.52f), new Color(.95f, .98f, .97f), .16f, true).transform;
            ClassroomUi.Label("BOB'S EXAMPLE  /  RIGHT HAND", example, -1.17f, .51f, .105f, ClassroomUi.DeepBlue, maxWidth: 2.55f);
            // Use the verified NID/SASL glyph, re-rendered as Bob's peach cartoon
            // hand. This keeps every A–Z pose accurate without showing a photo.
            ClassroomUi.Picture("Bob cartoon alphabet hand", "CartoonHand" + letter, example,
                new Vector3(0, -.08f, -.12f), new Vector2(1.38f, 1.38f));
            trackingText = ClassroomUi.Label("TRACKED HAND  /  RIGHT", learner, -1.17f, .51f, .105f, ClassroomUi.Green, maxWidth: 2.55f);
            liveView = new HandSkeletonView("Alphabet learner hand", content, new Color(.93f, .68f, .57f), true);
            alphabetStatus = ClassroomUi.Label("Show Bob your RIGHT hand to begin.", content, -3.05f, -1.64f, .15f,
                ClassroomUi.Ink, maxWidth: 5.25f);
            ClassroomUi.Button("<  Previous", content, -2.42f, -2.08f, 1.35f,
                () => ShowAlphabet(Mathf.Max(0, alphabetIndex - 1)), height: .35f);
            ClassroomUi.Button("Camera warm-up", content, -.67f, -2.08f, 1.65f, StartWarmup, height: .35f);
            ClassroomUi.Button(alphabetIndex == 25 ? "Finish alphabet" : "Practised  /  next  >", content,
                1.75f, -2.08f, 2.15f, RecordAlphabetAndNext, true, .35f);
            ClassroomUi.Label("Reference: National Institute for the Deaf", content, 3.05f, -1.72f, .09f,
                ClassroomUi.Muted, TextAnchor.MiddleRight, 3.0f);
        }

        void UpdateAlphabet()
        {
            var letter = ((char)('A' + alphabetIndex)).ToString();
            current.tracked = false;
            TryGetInputHand(current);
            webcamPreview?.SetBase64(inputMode == InputMode.Camera ? webcam.PreviewJpeg : "");
            liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.19f, -.20f)),
                Quaternion.Euler(0, 180, 0), .90f);
            trackingText.text = current.tracked ? inputMode == InputMode.Demo ? "YOUR DEMO HAND  /  RIGHT" : inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR VR HAND  /  RIGHT  /  LIVE" : inputMode == InputMode.Camera && !webcam.IsReceiving ?
                "CAMERA  /  WAITING  /  RIGHT HAND" : "YOUR RIGHT HAND  /  NOT VISIBLE";
            var expected = "TEAM_SASL_" + letter;
            var recognized = inputMode == InputMode.Camera ? webcam.RecognizedLabel : "";
            if (current.tracked && recognized == expected)
                alphabetStatus.text = $"Bob sees {letter}  /  research match {webcam.RecognitionConfidence:P0}. Compare the example, then save practice.";
            else if (current.tracked && recognized.StartsWith("TEAM_SASL_"))
                alphabetStatus.text = $"Bob sees {recognized.Replace("TEAM_SASL_", "")}. Adjust your hand to match {letter}, then try again.";
            else alphabetStatus.text = current.tracked
                ? "Hand visible. Compare it with Bob's example, then choose Practised."
                : inputMode == InputMode.Camera ? webcam.IsReceiving ? "Camera ready. Show your RIGHT hand to the webcam." :
                "Camera waiting. Allow Camera access to continue." : "Raise your RIGHT hand in front of you.";
        }

        void RecordAlphabetAndNext()
        {
            if (inputMode != InputMode.Demo && !current.tracked)
            {
                alphabetStatus.text = "Bob needs to see your full hand before saving this step.";
                tutor?.Say("Bring your whole hand into view first.", true);
                return;
            }
            var letter = ((char)('A' + alphabetIndex)).ToString();
            var expected = "TEAM_SASL_" + letter;
            if (inputMode == InputMode.Camera && current.tracked && webcam.RecognizedLabel.StartsWith("TEAM_SASL_") &&
                webcam.RecognizedLabel != expected && webcam.RecognitionConfidence >= .60f)
            {
                alphabetMistakes++;
                var observed = webcam.RecognizedLabel.Replace("TEAM_SASL_", "");
                var tip = AlphabetTip(letter, observed);
                alphabetStatus.text = $"Not quite {letter}. Bob sees {observed}. {tip}";
                tutor?.CoachCorrection();
                tutor?.Say(tip, true);
                return;
            }
            var accuracy = inputMode == InputMode.Camera && webcam.RecognizedLabel == "TEAM_SASL_" + letter
                ? webcam.RecognitionConfidence : 0f;
            try
            {
                history.Add(new LocalSessionRecord { lessonId = "alphabet-" + letter.ToLowerInvariant(), accuracy = accuracy,
                    durationSeconds = Mathf.Max(0, Time.time - lessonStarted), mistakes = alphabetMistakes, completed = true,
                    // Reference practice is intentionally local-only; a confident
                    // webcam match is a scored attempt and must enter the upload
                    // queue so the seeded alphabet lesson can be recorded.
                    synced = false, inputSource = accuracy > 0 ? "webcam-sasl-research" : "reference" });
                saveStatus = "Alphabet practice saved on this Mac";
                if (apiConnected) StartCoroutine(SyncPending());
            }
            catch (Exception error) { Debug.LogWarning(error.Message); }
            if (alphabetIndex == 25) ShowHome(); else ShowAlphabet(alphabetIndex + 1);
        }

        static string AlphabetTip(string expected, string observed)
        {
            switch (expected)
            {
                case "K": return "Try lowering your middle finger a little and keep your index finger clearly raised.";
                case "M": return "Tuck your thumb under the three curled fingers, then hold the knuckles together.";
                case "N": return "Keep the thumb tucked under two fingers and relax the other fingers into the same row.";
                case "R": return "Cross the index and middle fingers neatly, with the other fingers curled into the palm.";
                case "T": return "Place the thumb across the index finger and keep the remaining fingers curled.";
                case "U": return "Keep the index and middle fingers together and straight, with the other fingers curled.";
                case "V": return "Separate the index and middle fingers into a clear V, keeping the other fingers curled.";
                case "W": return "Raise three fingers together and keep the thumb and little finger tucked down.";
                case "Y": return "Extend the thumb and little finger while curling the three middle fingers into your palm.";
                default: return $"Bob sees {observed}; compare your {expected} with the example and check finger height, thumb placement and palm direction.";
            }
        }

        void ShowGreeting()
        {
            lessonStarted = Time.time;
            BeginPage(Page.Basics);
            tutor?.Say("Watch the greeting movement, then try it yourself.", true);
            ClassroomUi.Label("Greeting someone", content, -3.06f, .61f, .29f, ClassroomUi.Ink);
            ClassroomUi.Label("BEGINNER PHRASES  /  1", content, 3.06f, .61f, .12f, ClassroomUi.Blue, TextAnchor.MiddleRight);
            ClassroomUi.Label("Visual practice: movement, face and body position all carry meaning in SASL.",
                content, -3.06f, .25f, .13f, ClassroomUi.Muted, maxWidth: 6.1f);
            var example = ClassroomUi.Panel("Greeting reference", content, new Vector3(-1.67f, -.68f, 0),
                new Vector2(2.83f, 1.55f), new Color(.94f, .98f, 1f), .16f, true).transform;
            var learner = ClassroomUi.Panel("Greeting learner", content, new Vector3(1.67f, -.68f, 0),
                new Vector2(2.83f, 1.55f), new Color(.95f, .98f, .97f), .16f, true).transform;
            ClassroomUi.Label("WATCH THE MOVEMENT", example, -1.17f, .52f, .105f, ClassroomUi.DeepBlue);
            ClassroomUi.Picture("Bob cartoon greeting hand", "CartoonHandGreeting", example,
                new Vector3(0, -.13f, -.05f), new Vector2(1.22f, 1.06f));
            trackingText = ClassroomUi.Label("TRACKED HAND  /  RIGHT", learner, -1.17f, .52f, .095f, ClassroomUi.Green, maxWidth: 2.55f);
            liveView = new HandSkeletonView("Greeting learner hand", content, new Color(.93f, .68f, .57f), true);
            alphabetStatus = ClassroomUi.Label("Show your RIGHT hand and copy the greeting movement.", content,
                -3.05f, -1.64f, .15f, ClassroomUi.Ink, maxWidth: 4.5f);
            ClassroomUi.Button("<  Classroom", content, -2.45f, -2.08f, 1.35f, ShowHome, height: .35f);
            ClassroomUi.Button("Spell HELLO", content, -.95f, -2.08f, 1.35f, () => ShowFingerspell("HELLO"), true, .35f);
            ClassroomUi.Button("Spell HOW ARE YOU", content, .65f, -2.08f, 1.80f, () => ShowFingerspell("HOW ARE YOU"), height: .35f);
            ClassroomUi.Button("Save practice", content, 2.55f, -2.08f, 1.25f, RecordGreeting, height: .35f);
            ClassroomUi.Label("Reference: National Institute for the Deaf", content, 3.05f, -1.72f, .09f,
                ClassroomUi.Muted, TextAnchor.MiddleRight, 3f);
        }

        void ShowFingerspell(string phrase)
        {
            fingerspellPhrase = string.IsNullOrWhiteSpace(phrase) ? "HELLO" : phrase.ToUpperInvariant();
            fingerspellLetters = fingerspellPhrase.Replace(" ", "");
            fingerspellIndex = 0;
            fingerspellMistakes = 0;
            fingerspellHeld = 0;
            fingerspellScores.Clear();
            fingerspellStarted = Time.time;
            BeginPage(Page.Fingerspell);
            tutor?.Say($"Let's fingerspell {fingerspellPhrase}. One letter at a time, nice and clear.", true);
            RenderFingerspellPage();
        }

        void RenderFingerspellPage()
        {
            var letter = fingerspellLetters[fingerspellIndex].ToString();
            ClassroomUi.Label($"Fingerspell  {fingerspellPhrase}", content, -3.06f, .61f, .29f, ClassroomUi.Ink);
            ClassroomUi.Label($"LETTER {fingerspellIndex + 1} / {fingerspellLetters.Length}", content, 3.06f, .61f, .15f, ClassroomUi.Blue, TextAnchor.MiddleRight);
            ClassroomUi.Label("This is fingerspelling practice, not a claim that the English spelling replaces a SASL word sign.",
                content, -3.06f, .25f, .12f, ClassroomUi.Muted, maxWidth: 6.2f);
            var example = ClassroomUi.Panel("Fingerspelling example", content, new Vector3(-1.67f, -.69f, 0),
                new Vector2(2.83f, 1.52f), new Color(.94f, .98f, 1f), .16f, true).transform;
            var learner = ClassroomUi.Panel("Learner webcam", content, new Vector3(1.67f, -.69f, 0),
                new Vector2(2.83f, 1.52f), new Color(.95f, .98f, .97f), .16f, true).transform;
            ClassroomUi.Label("BOB'S EXAMPLE  /  RIGHT HAND", example, -1.17f, .51f, .105f, ClassroomUi.DeepBlue, maxWidth: 2.55f);
            ClassroomUi.Picture("Bob cartoon fingerspell hand", "CartoonHand" + letter, example,
                new Vector3(0, -.05f, -.12f), new Vector2(1.38f, 1.38f));
            trackingText = ClassroomUi.Label("TRACKED HAND  /  RIGHT", learner, -1.17f, .51f, .105f, ClassroomUi.Green, maxWidth: 2.55f);
            liveView = new HandSkeletonView("Phrase learner hand", content, new Color(.93f, .68f, .57f), true);
            alphabetStatus = ClassroomUi.Label($"Show the handshape for {letter}.", content, -3.05f, -1.64f, .15f,
                ClassroomUi.Ink, maxWidth: 6.1f);
            ClassroomUi.Button("<  Greeting", content, -2.45f, -2.08f, 1.45f, ShowGreeting, height: .35f);
            ClassroomUi.Button("Check letter", content, .55f, -2.08f, 1.55f, CheckFingerspellLetter, true, .35f);
            ClassroomUi.Button("Skip letter", content, 2.30f, -2.08f, 1.35f, SkipFingerspellLetter, height: .35f);
        }

        void UpdateFingerspell()
        {
            var expected = "TEAM_SASL_" + fingerspellLetters[fingerspellIndex];
            current.tracked = false;
            TryGetInputHand(current);
            webcamPreview?.SetBase64(inputMode == InputMode.Camera ? webcam.PreviewJpeg : "");
            liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.19f, -.20f)),
                Quaternion.Euler(0, 180, 0), .90f);
            trackingText.text = current.tracked ? inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR HAND  /  RIGHT  /  LIVE" :
                inputMode == InputMode.Camera && !webcam.IsReceiving ? "CAMERA  /  WAITING  /  RIGHT HAND" : "YOUR RIGHT HAND  /  NOT VISIBLE";
            if (current.tracked && inputMode == InputMode.Camera && webcam.RecognizedLabel == expected)
            {
                fingerspellHeld += Time.deltaTime;
                alphabetStatus.text = $"Bob sees {fingerspellLetters[fingerspellIndex]}  /  {webcam.RecognitionConfidence:P0}. Hold it steady, then check.";
                tutor?.Say("That handshape is looking good. Hold it steady.");
            }
            else
            {
                fingerspellHeld = 0;
                alphabetStatus.text = current.tracked ? "Hand visible. Compare your fingers with Bob's example." :
                    inputMode == InputMode.Camera ? "Show your RIGHT hand to the webcam." : "Raise your RIGHT hand in front of you.";
                if (current.tracked && inputMode == InputMode.Camera && webcam.RecognizedLabel.StartsWith("TEAM_SASL_") && webcam.RecognitionConfidence >= .60f)
                {
                    var observed = webcam.RecognizedLabel.Replace("TEAM_SASL_", "");
                    tutor?.Say(AlphabetTip(fingerspellLetters[fingerspellIndex].ToString(), observed));
                }
            }
        }

        void CheckFingerspellLetter()
        {
            var expected = "TEAM_SASL_" + fingerspellLetters[fingerspellIndex];
            var correct = inputMode == InputMode.Camera && current.tracked && webcam.RecognizedLabel == expected && webcam.RecognitionConfidence >= .60f;
            if (inputMode == InputMode.Demo) correct = current.tracked;
            if (!correct)
            {
                fingerspellMistakes++;
                var observed = webcam.RecognizedLabel.StartsWith("TEAM_SASL_") ? webcam.RecognizedLabel.Replace("TEAM_SASL_", "") : "unknown";
                var tip = observed == "unknown" ? "Show the whole hand, then try the letter again." : AlphabetTip(fingerspellLetters[fingerspellIndex].ToString(), observed);
                alphabetStatus.text = tip;
                tutor?.Say(tip, true);
                return;
            }
            fingerspellScores.Add(inputMode == InputMode.Camera ? webcam.RecognitionConfidence : .75f);
            fingerspellHeld = 0;
            if (++fingerspellIndex < fingerspellLetters.Length) RenderFingerspellPage();
            else CompleteFingerspellPhrase();
        }

        void SkipFingerspellLetter()
        {
            fingerspellMistakes++;
            fingerspellScores.Add(0);
            if (++fingerspellIndex < fingerspellLetters.Length) RenderFingerspellPage();
            else CompleteFingerspellPhrase();
        }

        void CompleteFingerspellPhrase()
        {
            var average = 0f;
            foreach (var score in fingerspellScores) average += score;
            average /= Mathf.Max(1, fingerspellScores.Count);
            try
            {
                history.Add(new LocalSessionRecord { lessonId = "phrase-fingerspell-" + fingerspellPhrase.ToLowerInvariant().Replace(" ", "-"),
                    accuracy = Mathf.Clamp01(average), durationSeconds = Mathf.Max(0, Time.time - fingerspellStarted),
                    mistakes = fingerspellMistakes, completed = true, synced = false,
                    inputSource = inputMode == InputMode.Camera ? "webcam-sasl-fingerspelling" : inputMode == InputMode.VR ? "openxr-fingerspelling" : "demo-fingerspelling" });
                saveStatus = "Phrase practice saved on this Mac";
                if (apiConnected) StartCoroutine(SyncPending());
            }
            catch (Exception error) { savingFailed = true; Debug.LogWarning(error.Message); }
            BeginPage(Page.Results);
            tutor?.Say($"You completed fingerspelling {fingerspellPhrase}! Next, we can add reviewed SASL word signs.", true);
            ClassroomUi.Label("Phrase practice complete", content, 0, .52f, .34f, ClassroomUi.DeepBlue, TextAnchor.MiddleCenter);
            ClassroomUi.Label($"{fingerspellPhrase}  /  {fingerspellScores.Count} letters", content, 0, .08f, .20f, ClassroomUi.Ink, TextAnchor.MiddleCenter);
            ClassroomUi.Label($"Practice accuracy  {average:P0}", content, 0, -.35f, .17f, ClassroomUi.Muted, TextAnchor.MiddleCenter);
            ClassroomUi.Label("This saved a fingerspelling session. It does not replace a reviewed SASL phrase sign.", content,
                0, -.82f, .12f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 6.0f);
            ClassroomUi.Button("Back to classroom", content, 0, -1.75f, 2.15f, ShowHome, true);
        }

        void UpdateBasics()
        {
            current.tracked = false;
            TryGetInputHand(current);
            webcamPreview?.SetBase64(inputMode == InputMode.Camera ? webcam.PreviewJpeg : "");
            liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.18f, -.20f)),
                Quaternion.Euler(0, 180, 0), .90f);
            trackingText.text = current.tracked ? inputMode == InputMode.Demo ? "YOUR DEMO HAND  /  RIGHT" : inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR VR HAND  /  RIGHT  /  LIVE" : inputMode == InputMode.Camera && !webcam.IsReceiving ?
                "CAMERA  /  WAITING  /  RIGHT HAND" : "YOUR RIGHT HAND  /  NOT VISIBLE";
            var reviewedGreeting = inputMode == InputMode.Camera && current.tracked &&
                webcam.RecognizedLabel == "REVIEWED_SASL_HELLO" && webcam.RecognitionConfidence >= .60f;
            alphabetStatus.text = reviewedGreeting ? $"Bob recognises the reviewed SASL greeting  /  {webcam.RecognitionConfidence:P0}. You can save a scored attempt."
                : current.tracked ? "Hand visible. Copy the movement slowly and clearly."
                : inputMode == InputMode.Camera && !webcam.IsReceiving ? "Camera is not sending frames yet. Allow Camera access." :
                "Show your RIGHT hand to the webcam.";
            if (reviewedGreeting) tutor?.Say("That matches the reviewed SASL greeting. Nice work!", true);
        }

        void RecordGreeting()
        {
            if (inputMode != InputMode.Demo && !current.tracked)
            {
                alphabetStatus.text = "Bob needs to see your hand before saving this lesson.";
                tutor?.Say("Bring your whole hand into view first.", true);
                return;
            }
            var reviewed = inputMode == InputMode.Camera && webcam.RecognizedLabel == "REVIEWED_SASL_HELLO" && webcam.RecognitionConfidence >= .60f;
            var practiceSource = reviewed ? "webcam-sasl-reviewed" : inputMode == InputMode.Camera ? "webcam-visual-practice" :
                inputMode == InputMode.VR ? "vr-visual-practice" : "demo-visual-practice";
            history.Add(new LocalSessionRecord { lessonId = "basic-greeting", accuracy = reviewed ? webcam.RecognitionConfidence : 0,
                durationSeconds = Mathf.Max(0, Time.time - lessonStarted), mistakes = 0, completed = true,
                synced = false, inputSource = practiceSource });
            if (apiConnected) StartCoroutine(SyncPending());
            tutor?.Say("Great practice! We will add more reviewed phrases next.", true);
            ShowHome();
        }

        void CycleMode()
        {
            BeginPage(Page.Input);
            ClassroomUi.Label("How would you like to practise?", content, -3.06f, .55f, .29f, ClassroomUi.Ink);
            ClassroomUi.Label("Choose one input. You can change it between sessions.", content, -3.06f, .16f, .155f, ClassroomUi.Muted);
            var names = new[] { "Webcam", "VR hands", "Try a demo" };
            var descriptions = new[] { "Use your Mac's camera.\nKeep your full hand in view.",
                "Use the simulator or headset.\nFollow your tracked hands.", "Use the on-screen controls.\nExplore without a camera." };
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var x = (i - 1) * 2.09f;
                var tile = ClassroomUi.Panel(names[i], content, new Vector3(x, -.70f, 0), new Vector2(1.94f, 1.42f),
                    ClassroomUi.Paper, .16f, true).transform;
                ClassroomUi.Label(names[i], tile, 0, .40f, .23f, ClassroomUi.DeepBlue, TextAnchor.MiddleCenter);
                ClassroomUi.Label(descriptions[i], tile, 0, -.03f, .125f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 1.74f);
                ClassroomUi.Button(inputMode == (InputMode)i ? "Selected" : "Choose", tile, 0, -.48f, 1.55f,
                    () => { inputMode = (InputMode)index; ShowHome(); }, inputMode == (InputMode)i, .33f);
            }
            ClassroomUi.Button("<  Back", content, -2.48f, -1.94f, 1.2f, ShowHome);
        }
        string ModeName() => inputMode == InputMode.Camera ? "Webcam  /  change" : inputMode == InputMode.VR ? "VR hands  /  change" : "Demo  /  change";

        public void StartWarmup()
        {
            lessonIndex = 0; sessionScores.Clear(); StartLesson();
        }

        void StartLesson()
        {
            lessonCompleted = false; matchHeld = 0; mistakes = 0; lessonStarted = Time.time; replayStarted = Time.time;
            simulated.CurrentPose = SimulatedHandPoseProvider.DemoPose.Open;
            BeginPage(Page.Practice);
            tutor?.Say(Titles[lessonIndex] + ". Watch my example first.", true);
            ClassroomUi.Label(Titles[lessonIndex], content, -3.08f, .62f, .28f, ClassroomUi.Ink, maxWidth: 5.4f);
            ClassroomUi.Label($"{lessonIndex + 1} / 3", content, 3.08f, .62f, .19f, ClassroomUi.Blue, TextAnchor.MiddleRight);
            ClassroomUi.Label(Instructions[lessonIndex], content, -3.06f, .28f, .145f, ClassroomUi.Muted, maxWidth: 6.2f);
            var targetCard = ClassroomUi.Panel("Watch card", content, new Vector3(-1.67f, -.65f, 0),
                new Vector2(2.83f, 1.46f), new Color(.94f, .98f, 1f), .16f, true).transform;
            var liveCard = ClassroomUi.Panel("Try card", content, new Vector3(1.67f, -.65f, 0),
                new Vector2(2.83f, 1.46f), new Color(.95f, .98f, .97f), .16f, true).transform;
            ClassroomUi.Label("WATCH BOB", targetCard, -1.17f, .49f, .12f, ClassroomUi.DeepBlue);
            trackingText = ClassroomUi.Label("YOUR HAND", liveCard, -1.17f, .49f, .12f, ClassroomUi.Green, maxWidth: 2.3f);
            var handTexture = lessonIndex == 0 ? "CartoonHandOpen" : lessonIndex == 1 ? "CartoonHandFist" : "CartoonHandPoint";
            practiceExamplePicture = ClassroomUi.Picture("Bob cartoon hand", handTexture, targetCard,
                new Vector3(0, -.05f, -.06f), new Vector2(1.12f, 1.12f));
            if (inputMode == InputMode.Demo)
                practiceLearnerPicture = ClassroomUi.Picture("Demo cartoon hand", handTexture, liveCard,
                    new Vector3(0, -.05f, -.06f), new Vector2(1.12f, 1.12f));
            else if (inputMode == InputMode.Camera)
                liveView = new HandSkeletonView("Tracked learner hand", content, new Color(.93f, .68f, .57f), true);
            else
                liveView = new HandSkeletonView("Learner hand", content, new Color(.78f, .53f, .45f), true);
            feedbackText = ClassroomUi.Label("Bring your hand into view", content, -3.05f, -1.62f, .18f, ClassroomUi.Ink, maxWidth: 5.25f);
            matchText = ClassroomUi.Label("", content, 3.08f, -1.62f, .16f, ClassroomUi.Green, TextAnchor.MiddleRight);
            var track = ClassroomUi.Panel("Hold progress", content, new Vector3(0, -1.88f, -.02f), new Vector2(6.13f, .045f),
                new Color(.86f, .92f, .94f), .02f).transform;
            progressFill = ClassroomUi.Panel("Completed hold", track, new Vector3(-3.05f, 0, -.006f), new Vector2(6.1f, .045f), ClassroomUi.Blue, .02f).transform;
            ClassroomUi.Button("<  Back", content, -2.60f, -2.15f, .98f, ShowHome, height: .35f);
            ClassroomUi.Button("Replay", content, -1.36f, -2.15f, 1.12f, () => replayStarted = Time.time, height: .35f);
            if (inputMode == InputMode.Demo)
            {
                ClassroomUi.Button("Pose  >", content, .05f, -2.15f, 1.12f, CycleDemoPose, height: .35f);
                demoPoseText = ClassroomUi.Label("Demo: open", content, 1.67f, -1.29f, .10f, ClassroomUi.Muted, TextAnchor.MiddleCenter);
            }
            nextButton = ClassroomUi.Button("Check hand", content, 2.30f, -2.15f, 1.55f, CheckOrContinue, true, .35f);
        }

        void CycleDemoPose()
        {
            simulated.CurrentPose = (SimulatedHandPoseProvider.DemoPose)(((int)simulated.CurrentPose + 1) % 3);
            SetDemoPicture();
        }

        void SetDemoPicture()
        {
            if (practiceLearnerPicture == null) return;
            var textureName = simulated.CurrentPose == SimulatedHandPoseProvider.DemoPose.Open ? "CartoonHandOpen" :
                simulated.CurrentPose == SimulatedHandPoseProvider.DemoPose.Fist ? "CartoonHandFist" : "CartoonHandPoint";
            var texture = Resources.Load<Texture2D>(textureName);
            var renderer = practiceLearnerPicture.GetComponent<Renderer>();
            if (texture != null && renderer != null) renderer.material.mainTexture = texture;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ShowHome();
            if (page == Page.Alphabet) { UpdateAlphabet(); return; }
            if (page == Page.Basics) { UpdateBasics(); return; }
            if (page == Page.Fingerspell) { UpdateFingerspell(); return; }
            if (page == Page.SaslPractice) { UpdateResearchPractice(); return; }
            if (page == Page.Practice && lessonCompleted) { UpdateSuccessPrompt(); return; }
            if (page != Page.Practice) return;
            if (inputMode == InputMode.Demo)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) { simulated.CurrentPose = SimulatedHandPoseProvider.DemoPose.Open; SetDemoPicture(); }
                if (Input.GetKeyDown(KeyCode.Alpha2)) { simulated.CurrentPose = SimulatedHandPoseProvider.DemoPose.Fist; SetDemoPicture(); }
                if (Input.GetKeyDown(KeyCode.Alpha3)) { simulated.CurrentPose = SimulatedHandPoseProvider.DemoPose.Point; SetDemoPicture(); }
            }
            current.tracked = false;
            if (inputMode == InputMode.Camera) webcam.TryGetRightHand(current);
            else if (inputMode == InputMode.VR) openXr.TryGetRightHand(current);
            else simulated.TryGetRightHand(current);
            webcamPreview?.SetBase64(inputMode == InputMode.Camera ? webcam.PreviewJpeg : "");
            var target = SimulatedHandPoseProvider.Template((SimulatedHandPoseProvider.DemoPose)lessonIndex);
            // Small, slow wrist sway invites the learner to inspect a three-dimensional hand.
            var angle = Mathf.Sin((Time.time - replayStarted) * .8f) * 12f;
            if (targetView != null)
                targetView.SetPose(target, content.TransformPoint(new Vector3(-1.67f, -1.18f, -.20f)), Quaternion.Euler(0, 180 + angle, 0), .92f);
            if (liveView != null)
                liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.18f, -.20f)), Quaternion.Euler(0, 180, 0), .92f);
            var match = PracticeMatch();
            var correct = current.tracked && match >= PracticeThreshold();
            if (!lessonCompleted)
            {
                matchHeld = correct ? matchHeld + Time.deltaTime : 0;
                if (inputMode != InputMode.Demo && matchHeld >= 1.25f) CompleteLesson(match);
                if (!current.tracked)
                {
                    tutor?.CoachWaiting();
                    tutor?.Say("Let me see your whole hand. Good lighting helps!");
                    feedbackText.text = inputMode == InputMode.Camera ? "Show your RIGHT hand to the webcam." : "Raise your RIGHT hand in front of you.";
                    matchText.text = "Waiting for hand";
                }
                else
                {
                    if (!correct) tutor?.CoachCorrection();
                    tutor?.Say(correct ? "Looking good! Hold it steady for a moment." : PoseHint(lessonIndex));
                    feedbackText.text = correct ? inputMode == InputMode.Demo ? "Ready! Choose Check hand." : "That's it! Hold your hand steady." : PoseHint(lessonIndex);
                    matchText.text = inputMode == InputMode.Camera
                        ? $"{match:P0} shape match  /  hold steady"
                        : $"{match:P0} match";
                }
            }
            trackingText.text = current.tracked ? inputMode == InputMode.Demo ? "YOUR DEMO HAND  /  RIGHT" : inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR VR HAND  /  RIGHT  /  LIVE" :
                inputMode == InputMode.Camera && !webcam.IsReceiving ? "CAMERA  /  WAITING  /  RIGHT HAND" : "YOUR RIGHT HAND  /  NOT VISIBLE";
            if (demoPoseText != null) demoPoseText.text = "Demo pose: " + simulated.CurrentPose.ToString().ToLowerInvariant();
            if (progressFill != null)
            {
                var amount = lessonCompleted ? 1 : inputMode == InputMode.Demo ? 0 : Mathf.Clamp01(matchHeld / 1.25f);
                progressFill.localScale = new Vector3(Mathf.Max(.001f, amount), 1, 1);
                progressFill.localPosition = new Vector3(-3.05f + amount * 3.05f, 0, -.006f);
            }
            if (Input.GetKeyDown(KeyCode.Space)) CheckOrContinue();
        }

        string PoseHint(int index) => HandPoseCoach.Hint(current, (SimulatedHandPoseProvider.DemoPose)index);

        // The Mac webcam landmarks are noisier than the deterministic simulator
        // and are mirrored/normalised by MediaPipe before they reach Unity. Keep
        // the camera gate forgiving enough to complete a real lesson, while the
        // learner still has to hold a tracked hand in the target shape for 1.25s.
        float PracticeThreshold() => inputMode == InputMode.Camera ? .50f : .78f;

        float PracticeMatch() => HandPoseCoach.Match(current,
            (SimulatedHandPoseProvider.DemoPose)lessonIndex);

        string HandName()
        {
            if (inputMode != InputMode.Camera || webcam == null) return "RIGHT";
            return string.IsNullOrWhiteSpace(webcam.DetectedHand) ? "RIGHT" : webcam.DetectedHand.ToUpperInvariant();
        }

        string WebcamHandLabel() => $"TRACKED HAND  /  {HandName()}";

        public static float TargetMatch(HandPoseFrame frame, HandPoseFrame target)
        {
            if (!frame.tracked) return 0;
            var a = frame.NormalizedFeatures(); var b = target.NormalizedFeatures(); var sum = 0f;
            for (var i = 0; i < a.Length; i++) { var delta = a[i] - b[i]; sum += delta * delta; }
            return Mathf.Clamp01(Mathf.Exp(-Mathf.Sqrt(sum / a.Length) * 10f));
        }

        void CheckOrContinue()
        {
            if (lessonCompleted)
            {
                if (++lessonIndex >= 3) ShowResults(); else StartLesson();
                return;
            }
            var match = PracticeMatch();
            if (current.tracked && match >= PracticeThreshold()) CompleteLesson(match);
            else { mistakes++; feedbackText.text = current.tracked ? PoseHint(lessonIndex) : "Your hand needs to be visible first."; }
            if (!current.tracked || match < PracticeThreshold()) tutor?.CoachCorrection();
        }

        void CompleteLesson(float match)
        {
            if (lessonCompleted || preview) return;
            lessonCompleted = true; successMatch = Mathf.Clamp01(match); sessionScores.Add(successMatch);
            tutor?.Encourage();
            tutor?.Say("You got it! Choose the next lesson, or try that sign again.", true);
            feedbackText.text = inputMode == InputMode.Demo
                ? $"Correct! Your demo sign matches  /  accuracy {successMatch:P0}."
                : $"Correct! Bob matched your sign  /  accuracy {successMatch:P0}.";
            feedbackText.color = ClassroomUi.Green.linear;
            var label = nextButton.GetComponentInChildren<TextMesh>(); label.text = lessonIndex == 2 ? "See results  >" : "Continue  >";
            try
            {
                history.Add(new LocalSessionRecord { lessonId = LessonIds[lessonIndex], accuracy = match,
                    durationSeconds = Time.time - lessonStarted, mistakes = mistakes, completed = true,
                    inputSource = inputMode == InputMode.Demo ? "demo" : inputMode == InputMode.Camera ? "webcam" : "openxr" });
                saveStatus = inputMode == InputMode.Demo ? "Demo saved on this Mac" : "Saved on this Mac";
                if (apiConnected) StartCoroutine(SyncPending());
            }
            catch (Exception error) { savingFailed = true; saveStatus = "Could not save on this Mac"; Debug.LogWarning(error.Message); }
            UpdateConnectionLabel();
            ShowSuccessPrompt();
        }

        void ShowSuccessPrompt()
        {
            if (successPrompt != null || content == null) return;
            successDeadline = Time.time + 15f;
            content.gameObject.SetActive(false);
            successModalRoot = new GameObject("Success modal layer").transform;
            successModalRoot.SetParent(board, false);
            successModalRoot.localPosition = new Vector3(0, 0, -.72f);
            successPrompt = ClassroomUi.Panel("Correct sign prompt", successModalRoot, new Vector3(0, -.42f, 0),
                new Vector2(4.75f, 2.08f), new Color(.98f, 1f, .99f), .22f, true).transform;
            ClassroomUi.Label("SIGN CORRECT!", successPrompt, 0, .67f, .25f, ClassroomUi.Green,
                TextAnchor.MiddleCenter);
            ClassroomUi.Label($"Bob says: nice work — accuracy {successMatch:P0}.", successPrompt, 0, .34f, .13f,
                ClassroomUi.Ink, TextAnchor.MiddleCenter, 4.2f);
            var nextPromptButton = ClassroomUi.Button("Next lesson  (15)", successPrompt, -1.10f, -.55f, 1.95f,
                AdvanceAfterSuccess, true, .38f);
            successCountdown = nextPromptButton.GetComponentInChildren<TextMesh>();
            ClassroomUi.Button("Try again", successPrompt, 1.10f, -.55f, 1.65f,
                RetryAfterSuccess, false, .38f);
        }

        void UpdateSuccessPrompt()
        {
            if (successPrompt == null) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Space))
            {
                AdvanceAfterSuccess();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                RetryAfterSuccess();
                return;
            }
            var remaining = Mathf.Max(0, Mathf.CeilToInt(successDeadline - Time.time));
            successCountdown.text = remaining > 0 ? $"Next lesson  ({remaining})" : "Opening…";
            if (remaining <= 0) AdvanceAfterSuccess();
        }

        void AdvanceAfterSuccess()
        {
            if (successPrompt == null) return;
            if (++lessonIndex >= 3) ShowResults(); else StartLesson();
        }

        void RetryAfterSuccess()
        {
            if (successPrompt == null) return;
            if (sessionScores.Count > 0) sessionScores.RemoveAt(sessionScores.Count - 1);
            StartLesson();
            tutor?.Say("No problem. Let’s try that sign once more.", true);
        }

        void ShowResults()
        {
            BeginPage(Page.Results);
            tutor?.Say("Three steps done! Practice makes progress.", true);
            ClassroomUi.Label("A great start!", content, 0, .50f, .38f, ClassroomUi.DeepBlue, TextAnchor.MiddleCenter);
            ClassroomUi.Label(inputMode == InputMode.Demo ? "You've tried the three demo exercises." : "You've completed your three hand warm-ups.",
                content, 0, .06f, .17f, ClassroomUi.Muted, TextAnchor.MiddleCenter);
            var average = 0f; foreach (var score in sessionScores) average += score;
            average /= Mathf.Max(1, sessionScores.Count);
            Metric("EXERCISES", sessionScores.Count + " / 3", -2.05f);
            Metric("TARGET MATCH", average.ToString("P0"), 0);
            Metric("SESSION", inputMode == InputMode.Demo ? "Demo" : "Tracked", 2.05f);
            ClassroomUi.Label(saveStatus, content, 0, -1.23f, .14f, ClassroomUi.Green, TextAnchor.MiddleCenter);
            ClassroomUi.Button("Practice again", content, -1.25f, -1.85f, 2.15f, StartWarmup, true);
            ClassroomUi.Button("Back to classroom", content, 1.25f, -1.85f, 2.15f, ShowHome);
        }

        void Metric(string label, string value, float x)
        {
            var tile = ClassroomUi.Panel(label, content, new Vector3(x, -.65f, 0), new Vector2(1.85f, .95f), ClassroomUi.Paper, .16f, true).transform;
            ClassroomUi.Label(value, tile, 0, .12f, .30f, ClassroomUi.DeepBlue, TextAnchor.MiddleCenter);
            ClassroomUi.Label(label, tile, 0, -.26f, .105f, ClassroomUi.Muted, TextAnchor.MiddleCenter);
        }

        void ShowProgress()
        {
            BeginPage(Page.Progress);
            ClassroomUi.Label("Your practice, at a glance.", content, -3.06f, .56f, .29f, ClassroomUi.Ink);
            ClassroomUi.Label(remoteProgressLoaded ? "Saved to your learner account and this Mac." : "Saved here on your Mac; account sync will appear when connected.",
                content, -3.06f, .19f, .155f, ClassroomUi.Muted);
            Metric("ACCOUNT COMPLETED", remoteProgressLoaded ? remoteCompleted.ToString() : history.TotalCompleted.ToString(), -2.05f);
            Metric("ACCOUNT AVERAGE", remoteProgressLoaded ? remoteAverage.ToString("P0") : history.TotalCompleted == 0 ? "--" : history.BestAccuracy.ToString("P0"), 0);
            Metric("ALPHABET", history.AlphabetPractisedCount + " / 26", 2.05f);
            ClassroomUi.Label($"Research collection: {history.ResearchPractisedCount} / 3 practised", content,
                0, -1.27f, .13f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 5.8f);
            ClassroomUi.Label(history.PendingCount > 0 ? $"{history.PendingCount} results waiting to sync. Your local copy is saved." :
                "Ready when you are. A little practice adds up.", content, 0, -1.54f, .15f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 6f);
            ClassroomUi.Button("Start a warm-up", content, 0, -1.93f, 2.55f, StartWarmup, true);
            if (apiConnected) StartCoroutine(RefreshRemoteProgress());
        }

        void ShowHelp()
        {
            BeginPage(Page.Help);
            ClassroomUi.Label("Make yourself comfortable.", content, -3.06f, .56f, .29f, ClassroomUi.Ink);
            HelpRow("01", "Choose an input", "Webcam uses this Mac's camera. VR uses headset hands.\nDemo lets you try everything with the on-screen pose button.", -.02f);
            HelpRow("02", "Watch, then try", "Keep your full hand in view and use good, even lighting.\nThe blue hand is the example; the green hand is yours.", -.77f);
            HelpRow("03", "You're in control", "Hold a matching pose, or choose Check hand.\nUse Continue when you're ready. Escape returns to the classroom.", -1.52f);
            ClassroomUi.Button("Got it", content, 2.37f, -2.11f, 1.4f, ShowHome, true, .34f);
        }
        void HelpRow(string number, string heading, string body, float y)
        {
            ClassroomUi.Label(number, content, -3.03f, y, .22f, ClassroomUi.Blue);
            ClassroomUi.Label(heading, content, -2.48f, y + .06f, .18f, ClassroomUi.Ink);
            ClassroomUi.Label(body, content, -2.48f, y - .25f, .135f, ClassroomUi.Muted, maxWidth: 5.4f);
        }

        void ShowSasl()
        {
            BeginPage(Page.Sasl);
            ClassroomUi.Label("Explore South African place names.", content, -3.06f, .57f, .27f, ClassroomUi.Ink);
            ClassroomUi.Label("A small SASL research collection, recorded by Deaf signers at UFS.", content, -3.06f, .21f, .145f, ClassroomUi.Muted, maxWidth: 6.2f);
            ClassroomUi.Panel("Research note", content, new Vector3(0, -.23f, 0), new Vector2(6.16f, .43f), new Color(.94f, .97f, .99f), .10f);
            ClassroomUi.Label("Research practice  /  regional variants  /  more review needed for teaching", content,
                0, -.23f, .12f, ClassroomUi.DeepBlue, TextAnchor.MiddleCenter, 5.9f);
            var names = new[] { "Cape Town", "Bloemfontein", "Pretoria" };
            for (var i = 0; i < names.Length; i++)
            {
                var selected = i;
                var x = (i - 1) * 2.09f;
                var tile = ClassroomUi.Panel(names[i], content, new Vector3(x, -1.03f, 0), new Vector2(1.91f, 1.14f),
                    Color.white, .14f, true).transform;
                ClassroomUi.Label(names[i], tile, 0, .31f, .20f, ClassroomUi.DeepBlue, TextAnchor.MiddleCenter, 1.7f);
                ClassroomUi.Label("Reference movement", tile, 0, -.01f, .10f, ClassroomUi.Muted, TextAnchor.MiddleCenter);
                ClassroomUi.Button("Watch sign  >", tile, 0, -.35f, 1.57f, () => ShowResearchPractice(selected), height: .30f);
            }
            trackingText = ClassroomUi.Label("Show your RIGHT hand to the webcam to see a research prediction.", content,
                0, -1.79f, .155f, ClassroomUi.Ink, TextAnchor.MiddleCenter, 6.1f);
            ClassroomUi.Label("Source: University of the Free State  /  CC BY-NC-SA 4.0", content,
                0, -2.13f, .11f, ClassroomUi.Muted, TextAnchor.MiddleCenter);
            StartCoroutine(UpdateResearchPrediction());
        }

        void ShowResearchPractice(int index)
        {
            if (references.Count <= index) return;
            researchIndex = index; researchStarted = Time.time;
            BeginPage(Page.SaslPractice);
            tutor?.Say("Watch the movement, then try it. Replay any time.", true);
            ClassroomUi.Label(references.Title(index), content, -3.06f, .61f, .29f, ClassroomUi.Ink);
            ClassroomUi.Label("SASL research reference  /  watch the hand shape and movement", content, -3.06f, .25f, .145f, ClassroomUi.Muted);
            var example = ClassroomUi.Panel("Research example", content, new Vector3(-1.67f, -.65f, 0), new Vector2(2.83f, 1.46f),
                new Color(.94f, .98f, 1f), .16f, true).transform;
            var learner = ClassroomUi.Panel("Research learner", content, new Vector3(1.67f, -.65f, 0), new Vector2(2.83f, 1.46f),
                new Color(.95f, .98f, .97f), .16f, true).transform;
            ClassroomUi.Label("BOB'S EXAMPLE  /  RIGHT HAND", example, -1.17f, .49f, .105f, ClassroomUi.DeepBlue, maxWidth: 2.55f);
            trackingText = ClassroomUi.Label("TRACKED HAND  /  RIGHT", learner, -1.17f, .49f, .105f, ClassroomUi.Green, maxWidth: 2.55f);
            // Play the attributed research landmark sequence on the board,
            // using the same Bob-style hand renderer as the learner card.
            targetView = new HandSkeletonView("Bob research example hand", content, new Color(.97f, .73f, .57f), true);
            liveView = new HandSkeletonView("Research learner hand", content, new Color(.93f, .68f, .57f), true);
            feedbackText = ClassroomUi.Label("Try the movement at your pace.", content, -3.05f, -1.61f, .16f, ClassroomUi.Ink, maxWidth: 6.1f);
            ClassroomUi.Button("<  Collection", content, -2.35f, -2.05f, 1.5f, ShowSasl, height: .35f);
            ClassroomUi.Button("Replay", content, -.65f, -2.05f, 1.25f, () => researchStarted = Time.time, height: .35f);
            ClassroomUi.Button("Slow / normal", content, 1.00f, -2.05f, 1.65f, () => { slowReplay = !slowReplay; researchStarted = Time.time; }, height: .35f);
            ClassroomUi.Button("Save practice", content, 2.55f, -2.05f, 1.25f, RecordResearchPractice, height: .35f);
        }

        void RecordResearchPractice()
        {
            if (inputMode != InputMode.Demo && !current.tracked)
            {
                feedbackText.text = "Show your RIGHT hand before saving this research practice.";
                tutor?.Say("Bring your whole hand into view first.", true);
                return;
            }
            var label = references.Label(researchIndex);
            var lessonId = "research-sasl-" + label.Replace("RESEARCH_SASL_", "").ToLowerInvariant().Replace('_', '-');
            try
            {
                history.Add(new LocalSessionRecord { lessonId = lessonId, accuracy = 0,
                    durationSeconds = Mathf.Max(0, Time.time - researchStarted), mistakes = 0, completed = true,
                    synced = false, inputSource = inputMode == InputMode.Camera ? "webcam-research-visual-practice" :
                        inputMode == InputMode.VR ? "openxr-research-visual-practice" : "demo-research-visual-practice" });
                saveStatus = "Research practice saved on this Mac";
                if (apiConnected) StartCoroutine(SyncPending());
                tutor?.Say("Research practice saved. Regional signs can vary, so keep comparing the reference.", true);
                ShowSasl();
            }
            catch (Exception error) { savingFailed = true; Debug.LogWarning(error.Message); }
        }

        void UpdateResearchPractice()
        {
            var duration = references.DurationSeconds * (slowReplay ? 1.65f : 1f);
            var motion = references.GetPose(researchIndex, (Time.time - researchStarted) / duration, researchReference);
            if (targetView != null)
                targetView.SetPose(researchReference,
                    content.TransformPoint(new Vector3(-1.67f, -1.15f, -.20f) + motion),
                    Quaternion.Euler(0, 180, 0), .87f);
            current.tracked = false; TryGetInputHand(current);
            webcamPreview?.SetBase64(inputMode == InputMode.Camera ? webcam.PreviewJpeg : "");
            liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.15f, -.20f)), Quaternion.Euler(0, 180, 0), .87f);
            trackingText.text = current.tracked ? inputMode == InputMode.Demo ? "YOUR DEMO HAND  /  RIGHT" : inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR VR HAND  /  RIGHT  /  LIVE" : inputMode == InputMode.Camera && !webcam.IsReceiving ? "CAMERA  /  WAITING  /  RIGHT HAND" :
                "YOUR HAND  /  NOT VISIBLE";
            feedbackText.text = !current.tracked ? "Show your RIGHT hand to the webcam to try the movement." :
                inputMode == InputMode.Camera && webcam.RecognizedLabel == references.Label(researchIndex) ? $"Similar movement detected  /  similarity {webcam.RecognitionConfidence:P0}" :
                "Watch the shape and direction, then try the movement again.";
        }

        void TryGetInputHand(HandPoseFrame destination)
        {
            if (inputMode == InputMode.Camera) webcam.TryGetRightHand(destination);
            else if (inputMode == InputMode.VR) openXr.TryGetRightHand(destination);
            else simulated.TryGetRightHand(destination);
        }

        IEnumerator UpdateResearchPrediction()
        {
            while (page == Page.Sasl)
            {
                var tracked = webcam.TryGetRightHand(current);
                var label = webcam.RecognizedLabel;
                trackingText.text = !tracked ? !webcam.IsReceiving ? "Camera is not sending frames yet." : $"Show your {HandName()} hand to the webcam." :
                    label.StartsWith("RESEARCH_SASL_") ? "Possible sign: " + label.Replace("RESEARCH_SASL_", "").Replace('_', ' ') +
                    $"  /  similarity {webcam.RecognitionConfidence:P0}" : "Watching your movement...";
                yield return new WaitForSeconds(.1f);
            }
        }

        void BuildCamera()
        {
            var item = new GameObject("Learner camera"); item.tag = "MainCamera";
            item.transform.position = new Vector3(0, 1.85f, -2.6f);
            var camera = item.AddComponent<Camera>(); camera.nearClipPlane = .03f; camera.farClipPlane = 50;
            camera.fieldOfView = 50f; camera.backgroundColor = ClassroomFactory.Sky;
            item.AddComponent<AudioListener>(); item.AddComponent<DesktopCameraController>();
            item.AddComponent<BoardPointer>(); item.AddComponent<HeadPoseDriver>();
            item.AddComponent<MacOpenXRBootstrap>();
        }

        IEnumerator ConnectBackend()
        {
            var email = $"demo-{SystemInfo.deviceUniqueIdentifier}@local.bcsignvr";
            var payload = JsonUtility.ToJson(new AuthRequest { name = "Local Learner", email = email, password = "local-demo-password" });
            using var register = Request("http://127.0.0.1:8787/api/auth/register", "POST", payload);
            yield return register.SendWebRequest();
            var response = register.downloadHandler.text;
            if (register.responseCode == 409)
            {
                using var login = Request("http://127.0.0.1:8787/api/auth/login", "POST", payload);
                yield return login.SendWebRequest();
                if (login.result != UnityWebRequest.Result.Success) { UpdateConnectionLabel(); yield break; }
                response = login.downloadHandler.text;
            }
            else if (register.result != UnityWebRequest.Result.Success) { UpdateConnectionLabel(); yield break; }
            token = JsonUtility.FromJson<AuthResponse>(response).token;
            apiConnected = !string.IsNullOrEmpty(token);
            UpdateConnectionLabel();
            if (apiConnected && !preview)
            {
                yield return SyncPending();
                yield return RefreshRemoteProgress();
            }
        }

        IEnumerator RefreshRemoteProgress()
        {
            if (!apiConnected || string.IsNullOrEmpty(token)) yield break;
            using var request = Request("http://127.0.0.1:8787/api/progress", "GET", null);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) yield break;
            try
            {
                var progress = JsonUtility.FromJson<ProgressResponse>(request.downloadHandler.text);
                if (progress?.summary == null) yield break;
                remoteCompleted = Mathf.Max(0, progress.summary.completed);
                remoteAverage = Mathf.Clamp01(progress.summary.averageAccuracy);
                remoteProgressLoaded = true;
            }
            catch (Exception error) { Debug.LogWarning($"Account progress could not be read: {error.Message}"); }
        }

        IEnumerator SyncPending()
        {
            if (syncing || !apiConnected || preview) yield break;
            syncing = true;
            foreach (var record in history.Pending)
            {
                using var request = Request("http://127.0.0.1:8787/api/sessions", "POST", JsonUtility.ToJson(record));
                request.SetRequestHeader("Authorization", $"Bearer {token}");
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { apiConnected = false; break; }
                try { history.MarkSynced(record.id); saveStatus = "Saved on this Mac and synced"; }
                catch (Exception error) { Debug.LogWarning(error.Message); break; }
            }
            syncing = false; UpdateConnectionLabel();
        }

        void UpdateConnectionLabel()
        {
            if (connectionText == null) return;
            connectionText.text = savingFailed ? "Save needs attention" : apiConnected && history.PendingCount == 0 ? "Progress sync ready" : "Progress saved on this Mac";
            connectionText.color = (savingFailed ? ClassroomFactory.Coral : ClassroomUi.Muted).linear;
        }

        IEnumerator CapturePreview(string path)
        {
            var scenario = Environment.GetEnvironmentVariable("BCSIGNVR_PREVIEW_PAGE");
            if (scenario == "practice" || scenario == "missing")
            { inputMode = scenario == "missing" ? InputMode.Camera : InputMode.Demo; StartWarmup(); }
            else if (scenario == "help") ShowHelp();
            else if (scenario == "progress") ShowProgress();
            else if (scenario == "sasl") ShowSasl();
            else if (scenario == "reference") ShowResearchPractice(0);
            else if (scenario == "input") CycleMode();
            else if (scenario == "alphabet") ShowAlphabet(0);
            else if (scenario.StartsWith("alphabet-", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = scenario.Substring("alphabet-".Length);
                var index = !string.IsNullOrEmpty(suffix) ? suffix[0] - 'a' : 0;
                ShowAlphabet(Mathf.Clamp(index, 0, 25));
            }
            else if (scenario == "greeting") ShowGreeting();
            else if (scenario == "fingerspell") ShowFingerspell("HELLO");
            else if (scenario == "success") ShowSuccessPreview();
            else if (scenario == "results")
            { inputMode = InputMode.Demo; sessionScores.AddRange(new[] { .96f, .93f, .98f }); saveStatus = "Demo preview"; ShowResults(); }
            yield return new WaitForSeconds(1.4f); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForSeconds(.5f); Application.Quit();
        }

        void ShowSuccessPreview()
        {
            inputMode = InputMode.Demo;
            StartWarmup();
            lessonCompleted = true;
            successMatch = 1f;
            feedbackText.text = "Correct! Bob matched your sign.";
            feedbackText.color = ClassroomUi.Green.linear;
            ShowSuccessPrompt();
        }

        static UnityWebRequest Request(string url, string method, string json)
        {
            var request = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = 3 };
            if (!string.IsNullOrEmpty(json))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            return request;
        }
        void OnDestroy() => webcam?.Dispose();
        [Serializable] sealed class AuthRequest { public string name; public string email; public string password; }
        [Serializable] sealed class AuthResponse { public string token; }
        [Serializable] sealed class ProgressResponse { public ProgressSummary summary; }
        [Serializable] sealed class ProgressSummary { public int completed; public float averageAccuracy; }
    }

    public sealed class HeadPoseDriver : MonoBehaviour
    {
        Vector3 origin;
        void Awake() => origin = transform.position;
        void LateUpdate()
        {
            if (!XRSettings.isDeviceActive) return;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.TryGetFeatureValue(CommonUsages.devicePosition, out var position))
                transform.position = new Vector3(origin.x, 0, origin.z) + position;
            if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation)) transform.rotation = rotation;
        }
    }
}
