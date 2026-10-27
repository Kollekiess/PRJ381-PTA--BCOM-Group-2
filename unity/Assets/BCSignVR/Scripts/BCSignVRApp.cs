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
        enum Page { Profiles, AddProfile, Home, Practice, Results, Progress, Help, Sasl, Input, SaslPractice, Alphabet, Basics, Fingerspell }
        const int ProfilesPerPage = 5;
        static readonly Vector2[] ProfileCardPositions =
        {
            new(-2.12f, -.16f), new(0f, -.16f), new(2.12f, -.16f),
            new(-2.12f, -1.42f), new(0f, -1.42f), new(2.12f, -1.42f)
        };
        static readonly Color[] ProfileAvatarColors =
        {
            new(.15f, .52f, .76f), new(.33f, .62f, .48f), new(.72f, .43f, .33f),
            new(.51f, .43f, .72f), new(.82f, .58f, .23f), new(.28f, .62f, .66f)
        };
        static readonly string[] LessonIds = { "prototype-open", "prototype-fist", "prototype-point" };
        static readonly string[] Titles = { "Open your hand", "Make a gentle fist", "Point your index finger" };
        static readonly string[] Labels = { "DEMO_OPEN", "DEMO_FIST", "DEMO_POINT" };
        static readonly string[] Instructions = { "Spread all five fingers. Face your palm towards the camera.",
            "Curl your fingers into your palm. Rest your thumb across them.",
            "Keep your index finger straight. Curl the other fingers in." };

        readonly HandPoseFrame current = new();
        readonly GestureEvaluator evaluator = new();
        SaslTemplateEvaluator saslEvaluator;
        readonly SimulatedHandPoseProvider simulated = new();
        readonly OpenXRHandPoseProvider openXr = new();
        readonly List<float> sessionScores = new();
        WebcamHandPoseProvider webcam;
        LocalSessionStore history;
        LearnerProfileStore profileStore;
        LearnerProfile activeProfile;
        SaslReferenceLibrary references;
        readonly HandPoseFrame researchReference = new();
        int researchIndex;
        float researchStarted;
        bool slowReplay;
        Transform board;
        Transform content;
        Transform presentationRoot;
        Transform classroomRoot;
        Transform chalkboardFrame;
        Transform navigationRoot;
        Transform controlsFooter;
        Camera learnerCamera;
        TextMesh connectionText, feedbackText, detailsText, matchText, trackingText, demoPoseText;
        TextMesh successCountdown;
        TextMesh profileNamePreview, profileFormStatus;
        Transform successPrompt;
        Transform successModalRoot;
        Transform progressFill;
        BoardButton nextButton;
        CartoonHandReplayView targetView;
        HandSkeletonView liveView;
        GameObject learnerHandPicture;
        GameObject practiceExamplePicture, practiceLearnerPicture;
        GameObject targetPicture;
        WebcamPreviewView webcamPreview;
        InputMode inputMode = InputMode.VR;
        Page page;
        int lessonIndex, mistakes;
        float lessonStarted, matchHeld, replayStarted;
        bool lessonCompleted, syncing, savingFailed;
        float successDeadline;
        float successMatch;
        string token, saveStatus = "Saved on this Mac";
        bool apiConnected;
        int profileRevision;
        Coroutine backendConnection;
        int profilePage;
        string profileNameDraft = "";
        TouchScreenKeyboard profileKeyboard;
        bool remoteProgressLoaded;
        int remoteCompleted;
        float remoteAverage;
        bool preview;
        bool alphabetSuccess;
        string stableSaslLetter = "";
        float stableSaslSince;
        float saslMatchHeld;
        GestureScore saslScore;
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
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.runInBackground = true;
            QualitySettings.antiAliasing = 8;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            preview = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BCSIGNVR_SCREENSHOT"));
            ClassroomFactory.Build();
            classroomRoot = GameObject.Find("Sign Club Classroom")?.transform;
            tutor = FindFirstObjectByType<TutorAvatar>();
            BuildCamera();
            history = new LocalSessionStore();
            profileStore = new LearnerProfileStore();
            references = new SaslReferenceLibrary();
            webcam = new WebcamHandPoseProvider();
            saslEvaluator = new SaslTemplateEvaluator(Resources.Load<TextAsset>("SaslTemplates"));
            for (var i = 0; i < Labels.Length; i++)
                evaluator.AddTemplate(Labels[i], SimulatedHandPoseProvider.Template((SimulatedHandPoseProvider.DemoPose)i));
            BuildShell();
            CreatePresentationRoot();
            if (preview)
            {
                SelectProfile(profileStore.Profiles[0], false);
                StartCoroutine(CapturePreview(Environment.GetEnvironmentVariable("BCSIGNVR_SCREENSHOT")));
            }
            else ShowProfiles();
        }

        void BuildShell()
        {
            chalkboardFrame = ClassroomUi.Panel("Chalkboard frame", null, new Vector3(.65f, 2.65f, 4.96f),
                new Vector2(7.55f, 5.05f), new Color(.13f, .31f, .25f), .28f, true).transform;
            board = ClassroomUi.Panel("Learning board", null, new Vector3(.65f, 2.65f, 4.9f),
                new Vector2(7.2f, 4.7f), Color.white, .26f, true).transform;
            ClassroomUi.Label("BC sign club", board, -3.13f, 1.92f, .28f, ClassroomUi.DeepBlue);
            ClassroomUi.Label("SOUTH AFRICAN SIGN LANGUAGE", board, -3.10f, 1.62f, .105f, ClassroomUi.Muted);
            // Keep the save/sync state internal for the presentation build;
            // the headset header should stay clean and not expose Mac-specific
            // storage wording to learners.
            // Leave the upper-right header free of Mac-specific save wording.
            connectionText = ClassroomUi.Label("", board, 3.08f, 1.90f, .125f,
                ClassroomUi.Muted, TextAnchor.MiddleRight, 2.6f);
            ClassroomUi.Panel("Header divider", board, new Vector3(0, 1.40f, -.025f), new Vector2(6.30f, .012f), ClassroomUi.Line, .006f);
            navigationRoot = new GameObject("Learner navigation").transform;
            navigationRoot.SetParent(board, false);
            ClassroomUi.Button("Classroom", navigationRoot, -2.40f, 1.10f, 1.42f, ShowHome);
            ClassroomUi.Button("Explore signs", navigationRoot, -.84f, 1.10f, 1.42f, ShowSasl);
            ClassroomUi.Button("My progress", navigationRoot, .76f, 1.10f, 1.52f, ShowProgress);
            ClassroomUi.Button("?  Help", navigationRoot, 2.56f, 1.10f, 1.12f, ShowHelp);
            ClassroomUi.Button("Switch user", navigationRoot, 2.55f, 1.90f, 1.18f, ShowProfiles, height: .30f);
            controlsFooter = new GameObject("Comfort and controls").transform;
            controlsFooter.position = new Vector3(.65f, .04f, 4.5f);
            ClassroomUi.Label("Click to choose  /  Tab + Enter  /  VR: look at a button for 5 seconds", controlsFooter,
                0, 0, .12f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 7.3f);
        }

        void CreatePresentationRoot()
        {
            presentationRoot = new GameObject("BCSignVR Presentation Root").transform;
            presentationRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            presentationRoot.localScale = Vector3.one;
            if (classroomRoot != null) classroomRoot.SetParent(presentationRoot, true);
            if (chalkboardFrame != null) chalkboardFrame.SetParent(presentationRoot, true);
            if (board != null) board.SetParent(presentationRoot, true);
            if (controlsFooter != null) controlsFooter.SetParent(presentationRoot, true);
        }

        void PlaceVrClassroom()
        {
            if (presentationRoot == null || board == null || learnerCamera == null) return;

            var forward = Vector3.ProjectOnPlane(learnerCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            forward.Normalize();

            // The authored classroom is intentionally spacious for the desktop
            // view. In a headset, anchor and scale that same scene relative to
            // the current tracking space so it cannot load metres away or above
            // the learner after moving the project between computers.
            const float scale = .36f;
            var boardCenter = learnerCamera.transform.position + forward * 2.35f;
            boardCenter.y = learnerCamera.transform.position.y - .08f;
            var boardLocalPosition = board.localPosition;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            presentationRoot.SetPositionAndRotation(Vector3.zero, rotation);
            presentationRoot.localScale = Vector3.one * scale;
            presentationRoot.position = boardCenter - rotation * Vector3.Scale(boardLocalPosition, presentationRoot.localScale);
            if (controlsFooter != null) controlsFooter.gameObject.SetActive(false);

            Debug.Log($"BCSignVR VR classroom anchored {boardCenter} at {scale:0.00} scale.");
        }

        void BeginPage(Page next)
        {
            page = next;
            if (navigationRoot != null)
                navigationRoot.gameObject.SetActive(next != Page.Profiles && next != Page.AddProfile);
            if (content != null) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
            if (successModalRoot != null) { Destroy(successModalRoot.gameObject); successModalRoot = null; }
            webcamPreview?.Dispose();
            webcamPreview = null;
            content = new GameObject(next.ToString()).transform; content.SetParent(board, false);
            content.localPosition = new Vector3(0, 0, -.10f);
            targetView = null; liveView = null; feedbackText = detailsText = matchText = trackingText = demoPoseText = null;
            learnerHandPicture = null;
            practiceExamplePicture = practiceLearnerPicture = null;
            targetPicture = null;
            successCountdown = null; successPrompt = null;
            progressFill = null; nextButton = null;
            matchHeld = 0;
            saslMatchHeld = 0;
            stableSaslLetter = "";
            stableSaslSince = Time.time;
        }

        GameObject CreateLearnerHandPicture(Transform parent, Vector3 center, Vector2 size, string initialTexture)
        {
            learnerHandPicture = ClassroomUi.Picture("Bob-style learner hand", initialTexture, parent, center, size);
            return learnerHandPicture;
        }

        void CreateLiveInputView(string name, Transform learnerCard, Vector3 imageCenter, Vector2 imageSize)
        {
            if (inputMode == InputMode.Camera)
            {
                webcamPreview = new WebcamPreviewView(name + " camera hand", learnerCard, imageCenter, imageSize);
                return;
            }
            // Keep the actual learner hand in the warm Pinocchio/wood-puppet
            // palette. The teaching/reference cards remain the peach Bob
            // sprites; only this live landmark view uses the puppet material.
            liveView = new HandSkeletonView(name, content, new Color(.74f, .76f, .78f), true);
        }

        void UpdateLearnerHandPicture(string fallbackTexture)
        {
            if (learnerHandPicture == null) return;
            var textureName = fallbackTexture;
            var recognized = webcam?.RecognizedLabel ?? "";
            if (recognized.StartsWith("TEAM_SASL_", StringComparison.Ordinal) && recognized.Length == 11)
            {
                var letter = recognized[10];
                if (letter >= 'A' && letter <= 'Z') textureName = "CartoonHand" + letter;
            }
            else if (recognized == "REVIEWED_SASL_HELLO") textureName = "CartoonHandGreeting";

            var texture = Resources.Load<Texture2D>(textureName);
            var renderer = learnerHandPicture.GetComponent<Renderer>();
            if (texture != null && renderer != null && renderer.sharedMaterial.mainTexture != texture)
                renderer.sharedMaterial.mainTexture = texture;
            learnerHandPicture.SetActive(current.tracked);
        }

        void ShowHome()
        {
            if (activeProfile == null) { ShowProfiles(); return; }
            BeginPage(Page.Home);
            tutor?.Say("Welcome! Let's learn one hand sign at a time.", true);
            ClassroomUi.Label($"LEARNER  /  {activeProfile.displayName}", content, 3.05f, .68f, .12f,
                ClassroomUi.DeepBlue, TextAnchor.MiddleRight, 2.25f);
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
            ClassroomUi.Label("Bob's greeting sign", handCard, 0, .65f, .145f, ClassroomUi.DeepBlue, TextAnchor.MiddleCenter, 1.75f);
            MovementDemoView.Create("Bob animated greeting preview", "CartoonHandGreeting", "greeting", handCard,
                new Vector3(0, -.02f, -.12f), new Vector2(1.10f, 1.10f));
            ClassroomUi.Button("Preview greeting", handCard, 0, -.64f, 1.58f, ShowGreeting, height: .31f);
            ClassroomUi.Label("Choose your input", content, 1.23f, -1.43f, .13f, ClassroomUi.Muted);
            ClassroomUi.Button(ModeName(), content, 2.13f, -1.82f, 1.95f, CycleMode);
            var inputHint = inputMode == InputMode.Camera ? (WebcamModeAvailable() ? "Mac webcam bridge ready." : "Connect Quest by USB-C to unlock webcam.") :
                inputMode == InputMode.VR ? "Use the simulator or Quest hands." : "No camera needed for Demo.";
            ClassroomUi.Label(inputHint, content, 2.13f, -2.14f, .105f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 1.95f);
        }

        void ShowProfiles()
        {
            BeginPage(Page.Profiles);
            profileKeyboard = null;
            var profiles = profileStore.Profiles;
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(profiles.Count / (float)ProfilesPerPage));
            profilePage = Mathf.Clamp(profilePage, 0, pageCount - 1);
            ClassroomUi.Label("Who's practising today?", content, 0, .86f, .31f,
                ClassroomUi.Ink, TextAnchor.MiddleCenter, 6.2f);
            ClassroomUi.Label("Choose a learner to open your signing classroom.", content, 0, .62f, .145f,
                ClassroomUi.Muted, TextAnchor.MiddleCenter, 5.8f);

            var first = profilePage * ProfilesPerPage;
            var visibleCount = Mathf.Min(ProfilesPerPage, profiles.Count - first);
            for (var index = 0; index < visibleCount; index++)
            {
                var position = visibleCount == 1
                    ? new Vector2(-1.06f, ProfileCardPositions[0].y)
                    : ProfileCardPositions[index];
                DrawProfileCard(profiles[first + index], position.x, position.y);
            }

            var addPosition = visibleCount == 1
                ? new Vector2(1.06f, ProfileCardPositions[0].y)
                : ProfileCardPositions[visibleCount];
            DrawAddProfileCard(addPosition.x, addPosition.y);
            if (profilePage > 0)
                ClassroomUi.Button("<  Previous", content, -2.28f, -2.22f, 1.55f,
                    () => { profilePage--; ShowProfiles(); }, height: .20f);
            if (profilePage + 1 < pageCount)
                ClassroomUi.Button("More learners  >", content, 2.28f, -2.22f, 1.75f,
                    () => { profilePage++; ShowProfiles(); }, height: .20f);
        }

        void DrawProfileCard(LearnerProfile profile, float x, float y)
        {
            var card = ClassroomUi.Panel("Learner profile " + profile.displayName, content,
                new Vector3(x, y, -.03f), new Vector2(1.28f, 1.28f), ClassroomUi.Paper, .14f, true);
            var name = ClassroomUi.Label(profile.displayName, card.transform, 0, -.43f, .14f,
                ClassroomUi.DeepBlue, TextAnchor.MiddleCenter, 1.12f);
            var avatarColor = ProfileAvatarColors[Mathf.Clamp(profile.avatarIndex, 0, ProfileAvatarColors.Length - 1)];
            var avatar = ClassroomUi.Panel("Profile avatar", card.transform, new Vector3(0, .16f, -.035f),
                new Vector2(.72f, .72f), avatarColor, .12f, true).transform;
            var initial = string.IsNullOrWhiteSpace(profile.displayName) ? "?" : profile.displayName.Substring(0, 1).ToUpperInvariant();
            ClassroomUi.Label(initial, avatar, 0, 0, .27f, Color.white, TextAnchor.MiddleCenter);
            var button = card.AddComponent<BoardButton>();
            button.Initialize(() => SelectProfile(profile), false, name);
            var collider = card.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.38f, 1.38f, .18f);
        }

        void DrawAddProfileCard(float x, float y)
        {
            var card = ClassroomUi.Panel("Add learner profile", content,
                new Vector3(x, y, -.03f), new Vector2(1.28f, 1.28f), new Color(.95f, .98f, 1f), .14f, true);
            ClassroomUi.Label("+", card.transform, 0, .16f, .42f, ClassroomUi.Blue, TextAnchor.MiddleCenter);
            var label = ClassroomUi.Label("Add user", card.transform, 0, -.43f, .14f,
                ClassroomUi.DeepBlue, TextAnchor.MiddleCenter, 1.12f);
            card.AddComponent<BoardButton>().Initialize(ShowAddProfile, false, label);
            var collider = card.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.38f, 1.38f, .18f);
        }

        void ShowAddProfile()
        {
            BeginPage(Page.AddProfile);
            profileNameDraft = "";
            profileKeyboard = null;
            ClassroomUi.Label("Add a learner", content, 0, .77f, .31f,
                ClassroomUi.Ink, TextAnchor.MiddleCenter, 5.8f);
            ClassroomUi.Label("Give this profile a name. Their practice stays separate on this headset.",
                content, 0, .41f, .14f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 5.9f);
            var entry = ClassroomUi.Panel("Learner name entry", content, new Vector3(0, -.27f, 0),
                new Vector2(4.25f, .72f), new Color(.96f, .99f, 1f), .14f, true).transform;
            profileNamePreview = ClassroomUi.Label("Tap Enter name to type on the Quest keyboard", entry,
                0, 0, .17f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 3.95f);
            profileFormStatus = ClassroomUi.Label("", content, 0, -.89f, .125f,
                ClassroomUi.Muted, TextAnchor.MiddleCenter, 5.2f);
            ClassroomUi.Button("Enter name", content, -1.24f, -1.50f, 1.85f, OpenProfileKeyboard, height: .40f);
            ClassroomUi.Button("Create and continue  >", content, 1.16f, -1.50f, 2.15f,
                CreateProfileFromForm, true, height: .40f);
            ClassroomUi.Button("Back to profiles", content, 0, -2.05f, 1.85f,
                ShowProfiles, height: .34f);
        }

        void OpenProfileKeyboard()
        {
            if (!TouchScreenKeyboard.isSupported)
            {
                if (profileFormStatus != null) profileFormStatus.text = "The system keyboard is not available on this device.";
                return;
            }
            profileKeyboard = TouchScreenKeyboard.Open(profileNameDraft, TouchScreenKeyboardType.Default,
                false, false, false, false, "Learner name", 28);
        }

        void UpdateProfileKeyboard()
        {
            if (profileKeyboard == null) return;
            profileNameDraft = profileKeyboard.text ?? "";
            if (profileNamePreview != null)
            {
                profileNamePreview.text = string.IsNullOrWhiteSpace(profileNameDraft)
                    ? "Type a learner name" : profileNameDraft;
                profileNamePreview.color = string.IsNullOrWhiteSpace(profileNameDraft)
                    ? ClassroomUi.Muted : ClassroomUi.DeepBlue;
            }
            if (profileKeyboard.status == TouchScreenKeyboard.Status.Done ||
                profileKeyboard.status == TouchScreenKeyboard.Status.Canceled)
                profileKeyboard = null;
        }

        void CreateProfileFromForm()
        {
            var name = string.IsNullOrWhiteSpace(profileNameDraft)
                ? $"Learner {profileStore.Profiles.Count + 1}" : profileNameDraft.Trim();
            try
            {
                var profile = profileStore.Add(name);
                profilePage = (profileStore.Profiles.Count - 1) / ProfilesPerPage;
                SelectProfile(profile);
            }
            catch (Exception error)
            {
                if (profileFormStatus != null) profileFormStatus.text = error.Message;
            }
        }

        void SelectProfile(LearnerProfile profile, bool connectBackend = true)
        {
            profileRevision++;
            if (backendConnection != null) StopCoroutine(backendConnection);
            backendConnection = null;
            activeProfile = profile;
            profileKeyboard = null;
            history = new LocalSessionStore(profile.id == LearnerProfileStore.DefaultProfileId
                ? null
                : System.IO.Path.Combine(Application.persistentDataPath, "profiles", profile.id, "sessions.json"));
            token = null;
            apiConnected = false;
            syncing = false;
            savingFailed = false;
            remoteProgressLoaded = false;
            remoteCompleted = 0;
            remoteAverage = 0;
            ShowHome();
            if (connectBackend && !preview)
                backendConnection = StartCoroutine(MaintainBackendConnection(profileRevision));
        }

        IEnumerator MaintainBackendConnection(int revision)
        {
            while (revision == profileRevision)
            {
                if (!apiConnected) yield return ConnectBackend(revision);
                else if (history.PendingCount > 0) yield return SyncPending();
                yield return new WaitForSecondsRealtime(30f);
            }
        }

        void ShowAlphabet(int index)
        {
            alphabetIndex = Mathf.Clamp(index, 0, 25);
            alphabetMistakes = 0;
            lessonStarted = Time.time;
            alphabetSuccess = false;
            BeginPage(Page.Alphabet);
            var letter = ((char)('A' + alphabetIndex)).ToString();
            tutor?.Say($"This is letter {letter}. Copy the handshape, then show me.", true);
            ClassroomUi.Label($"Letter {letter}", content, -3.06f, .61f, .29f, ClassroomUi.Ink);
            ClassroomUi.Label($"{alphabetIndex + 1} / 26", content, 3.06f, .61f, .18f, ClassroomUi.Blue, TextAnchor.MiddleRight);
            ClassroomUi.Label("Study the fingers, thumb and palm direction. Keep your whole hand in view.",
                content, -3.06f, .25f, .14f, ClassroomUi.Muted, maxWidth: 6.1f);
            if (letter == "J")
                ClassroomUi.Label("Movement: start with your little finger up, then trace a gentle J in the air.",
                    content, -3.06f, .13f, .105f, ClassroomUi.Blue, maxWidth: 6.1f);
            else if (letter == "Z")
                ClassroomUi.Label("Movement: start with your index finger out, then trace a clear Z in the air.",
                    content, -3.06f, .13f, .105f, ClassroomUi.Blue, maxWidth: 6.1f);
            var cardY = letter == "J" || letter == "Z" ? -.75f : -.69f;
            var example = ClassroomUi.Panel("Official alphabet reference", content, new Vector3(-1.67f, cardY, 0),
                new Vector2(2.83f, 1.52f), new Color(.94f, .98f, 1f), .16f, true).transform;
            var learner = ClassroomUi.Panel("Learner camera hand", content, new Vector3(1.67f, cardY, 0),
                new Vector2(2.83f, 1.52f), new Color(.95f, .98f, .97f), .16f, true).transform;
            ClassroomUi.Label(letter == "J" || letter == "Z" ? "BOB'S EXAMPLE  /  MOVING" : "BOB'S EXAMPLE  /  RIGHT HAND",
                example, -1.17f, .51f, .105f, ClassroomUi.DeepBlue, maxWidth: 2.55f);
            if (letter == "J" || letter == "Z")
                MovementDemoView.Create("Bob animated " + letter + " movement", "CartoonHand" + letter, letter,
                    example, new Vector3(0, -.08f, -.12f), new Vector2(1.24f, 1.24f));
            else
                ClassroomUi.Picture("Bob cartoon alphabet hand", "CartoonHand" + letter, example,
                    // Keep a little breathing room inside the 2.83 x 1.52 card;
                    // some glyphs use almost the full transparent texture bounds.
                    new Vector3(0, -.08f, -.12f), new Vector2(1.24f, 1.24f));
            trackingText = ClassroomUi.Label("TRACKED HAND  /  RIGHT", learner, -1.17f, .51f, .105f, ClassroomUi.Green, maxWidth: 2.55f);
            if (inputMode == InputMode.Demo)
                CreateLearnerHandPicture(learner, new Vector3(0, -.08f, -.12f), new Vector2(1.24f, 1.24f), "CartoonHand" + letter);
            else
                CreateLiveInputView("Alphabet learner hand", learner, new Vector3(0, -.08f, -.12f), new Vector2(1.70f, 1.00f));
            alphabetStatus = ClassroomUi.Label("Show Bob your RIGHT hand to begin.", content, -3.05f, -1.64f, .15f,
                ClassroomUi.Ink, maxWidth: 5.25f);
            ClassroomUi.Button("<  Previous", content, -2.55f, -2.08f, 1.25f,
                () => ShowAlphabet(Mathf.Max(0, alphabetIndex - 1)), height: .35f);
            ClassroomUi.Button("Camera warm-up", content, -.95f, -2.08f, 1.55f, StartWarmup, height: .35f);
            ClassroomUi.Button("Skip / next  >", content, .70f, -2.08f, 1.35f,
                SkipAlphabetAndNext, height: .35f);
            ClassroomUi.Button(alphabetIndex == 25 ? "Finish alphabet" : "Practised  /  next  >", content,
                2.42f, -2.08f, 1.70f, RecordAlphabetAndNext, true, .35f);
            ClassroomUi.Label("Reference: National Institute for the Deaf", content, 3.05f, -1.72f, .09f,
                ClassroomUi.Muted, TextAnchor.MiddleRight, 3.0f);
        }

        void UpdateAlphabet()
        {
            var letter = ((char)('A' + alphabetIndex)).ToString();
            current.tracked = false;
            TryGetInputHand(current);
            UpdateSaslMatch(letter);
            webcamPreview?.SetBase64(inputMode == InputMode.Camera ? webcam.PreviewJpeg : "");
            if (liveView != null)
                liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.19f, -.20f)),
                    Quaternion.Euler(0, 180, 0), .90f);
            UpdateLearnerHandPicture("CartoonHand" + letter);
            trackingText.text = current.tracked ? inputMode == InputMode.Demo ? "YOUR DEMO HAND  /  RIGHT" : inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR VR HAND  /  RIGHT  /  LIVE" : inputMode == InputMode.Camera && !webcam.IsReceiving ?
                "CAMERA  /  WAITING  /  RIGHT HAND" : "YOUR RIGHT HAND  /  NOT VISIBLE";
            var recognized = SaslLetter();
            if (current.tracked && recognized == letter && SaslConfidence() >= SaslThreshold())
                alphabetStatus.text = $"Bob sees {letter}  /  {SaslSourceName()} match {SaslConfidence():P0}. Hold steady, then choose Practised.";
            else if (current.tracked && !string.IsNullOrEmpty(recognized))
                alphabetStatus.text = $"Bob sees {recognized}. Adjust your hand to match {letter}, then try again.";
            else alphabetStatus.text = current.tracked
                ? saslEvaluator != null && saslEvaluator.IsReady && inputMode == InputMode.VR ? "Hand visible. Match Bob's fingers and thumb; hold steady for recognition." : "Hand visible. Compare it with Bob's example, then choose Practised."
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
            UpdateSaslMatch(letter);
            if (inputMode != InputMode.Demo && (!SaslMatches(letter) || saslMatchHeld < .65f))
            {
                alphabetMistakes++;
                var observed = SaslLetter();
                var tip = string.IsNullOrEmpty(observed) ? "Hold the matching handshape steady for a moment so Bob can compare every finger." : AlphabetTip(letter, observed);
                alphabetStatus.text = string.IsNullOrEmpty(observed) ? tip : $"Not quite {letter}. Bob sees {observed}. {tip}";
                tutor?.CoachCorrection();
                tutor?.Say(tip, true);
                return;
            }
            var accuracy = inputMode == InputMode.Demo ? 1f : SaslConfidence();
            CompleteAlphabetLetter(letter, accuracy);
        }

        void CompleteAlphabetLetter(string letter, float accuracy)
        {
            try
            {
                history.Add(new LocalSessionRecord { lessonId = "alphabet-" + letter.ToLowerInvariant(), accuracy = accuracy,
                    durationSeconds = Mathf.Max(0, Time.time - lessonStarted), mistakes = alphabetMistakes, completed = true,
                    // Reference practice is intentionally local-only; a confident
                    // webcam match is a scored attempt and must enter the upload
                    // queue so the seeded alphabet lesson can be recorded.
                    synced = false, inputSource = inputMode == InputMode.Camera ? "webcam-sasl-research" :
                        inputMode == InputMode.VR ? "openxr-sasl-template" : "demo" });
                saveStatus = "Alphabet practice saved on this Mac";
                if (apiConnected) StartCoroutine(SyncPending());
            }
            catch (Exception error) { Debug.LogWarning(error.Message); }
            alphabetSuccess = true;
            successMatch = Mathf.Clamp01(accuracy);
            tutor?.Say($"Correct {letter}! Bob matched your handshape. Choose the next letter or try it again.", true);
            ShowAlphabetSuccessPrompt();
        }

        void SkipAlphabetAndNext()
        {
            var letter = ((char)('A' + alphabetIndex)).ToString();
            tutor?.Say($"Skipping {letter}. You can revisit it from the alphabet any time.", true);
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
                case "J": return "Keep only your little finger raised, then trace the J smoothly with your wrist.";
                case "Z": return "Keep only your index finger raised, then draw the Z with a small, clear wrist movement.";
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
            ClassroomUi.Label("WATCH BOB'S MOVEMENT", example, -1.17f, .52f, .105f, ClassroomUi.DeepBlue, maxWidth: 2.55f);
            MovementDemoView.Create("Bob animated greeting", "CartoonHandGreeting", "greeting", example,
                new Vector3(0, -.13f, -.05f), new Vector2(1.22f, 1.06f));
            trackingText = ClassroomUi.Label("TRACKED HAND  /  RIGHT", learner, -1.17f, .52f, .095f, ClassroomUi.Green, maxWidth: 2.55f);
            if (inputMode == InputMode.Demo)
                CreateLearnerHandPicture(learner, new Vector3(0, -.13f, -.05f), new Vector2(1.22f, 1.06f), "CartoonHandGreeting");
            else
                CreateLiveInputView("Greeting learner hand", learner, new Vector3(0, -.13f, -.05f), new Vector2(1.70f, 1.00f));
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
            if (letter == "J")
                ClassroomUi.Label("Movement: start with your little finger up, then trace a gentle J in the air.",
                    content, -3.06f, .13f, .105f, ClassroomUi.Blue, maxWidth: 6.1f);
            else if (letter == "Z")
                ClassroomUi.Label("Movement: start with your index finger out, then trace a clear Z in the air.",
                    content, -3.06f, .13f, .105f, ClassroomUi.Blue, maxWidth: 6.1f);
            var example = ClassroomUi.Panel("Fingerspelling example", content, new Vector3(-1.67f, -.69f, 0),
                new Vector2(2.83f, 1.52f), new Color(.94f, .98f, 1f), .16f, true).transform;
            var learner = ClassroomUi.Panel("Learner webcam", content, new Vector3(1.67f, -.69f, 0),
                new Vector2(2.83f, 1.52f), new Color(.95f, .98f, .97f), .16f, true).transform;
            ClassroomUi.Label(letter == "J" || letter == "Z" ? "BOB'S EXAMPLE  /  MOVING" : "BOB'S EXAMPLE  /  RIGHT HAND",
                example, -1.17f, .51f, .105f, ClassroomUi.DeepBlue, maxWidth: 2.55f);
            if (letter == "J" || letter == "Z")
                MovementDemoView.Create("Bob animated fingerspell " + letter, "CartoonHand" + letter, letter,
                    example, new Vector3(0, -.05f, -.12f), new Vector2(1.24f, 1.24f));
            else
                ClassroomUi.Picture("Bob cartoon fingerspell hand", "CartoonHand" + letter, example,
                    new Vector3(0, -.05f, -.12f), new Vector2(1.24f, 1.24f));
            trackingText = ClassroomUi.Label("TRACKED HAND  /  RIGHT", learner, -1.17f, .51f, .105f, ClassroomUi.Green, maxWidth: 2.55f);
            if (inputMode == InputMode.Demo)
                CreateLearnerHandPicture(learner, new Vector3(0, -.05f, -.12f), new Vector2(1.24f, 1.24f), "CartoonHand" + letter);
            else
                CreateLiveInputView("Phrase learner hand", learner, new Vector3(0, -.05f, -.12f), new Vector2(1.70f, 1.00f));
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
            UpdateSaslMatch(fingerspellLetters[fingerspellIndex].ToString());
            webcamPreview?.SetBase64(inputMode == InputMode.Camera ? webcam.PreviewJpeg : "");
            if (liveView != null)
                liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.19f, -.20f)),
                    Quaternion.Euler(0, 180, 0), .90f);
            UpdateLearnerHandPicture("CartoonHand" + fingerspellLetters[fingerspellIndex]);
            trackingText.text = current.tracked ? inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR HAND  /  RIGHT  /  LIVE" :
                inputMode == InputMode.Camera && !webcam.IsReceiving ? "CAMERA  /  WAITING  /  RIGHT HAND" : "YOUR RIGHT HAND  /  NOT VISIBLE";
            if (current.tracked && SaslMatches(fingerspellLetters[fingerspellIndex].ToString()) &&
                saslMatchHeld >= .65f)
            {
                fingerspellHeld += Time.deltaTime;
                alphabetStatus.text = $"Bob sees {fingerspellLetters[fingerspellIndex]}  /  {SaslSourceName()} match {SaslConfidence():P0}. Hold it steady, then check.";
                tutor?.Say("That handshape is looking good. Hold it steady.");
            }
            else
            {
                fingerspellHeld = 0;
                alphabetStatus.text = current.tracked ? string.IsNullOrEmpty(SaslLetter()) ? "Hand visible. Match every finger to Bob's example; hold steady for recognition." :
                    $"Bob sees {SaslLetter()}. Compare it with {fingerspellLetters[fingerspellIndex]} and try again." :
                    inputMode == InputMode.Camera ? "Show your RIGHT hand to the webcam." : "Raise your RIGHT hand in front of you.";
                if (current.tracked && !string.IsNullOrEmpty(SaslLetter()) && SaslConfidence() >= SaslThreshold())
                {
                    var observed = SaslLetter();
                    tutor?.Say(AlphabetTip(fingerspellLetters[fingerspellIndex].ToString(), observed));
                }
            }
        }

        void CheckFingerspellLetter()
        {
            var expected = "TEAM_SASL_" + fingerspellLetters[fingerspellIndex];
            var correct = inputMode != InputMode.Demo && current.tracked &&
                SaslMatches(fingerspellLetters[fingerspellIndex].ToString()) && saslMatchHeld >= .65f;
            if (inputMode == InputMode.Demo) correct = current.tracked;
            if (!correct)
            {
                fingerspellMistakes++;
                var observed = SaslLetter();
                if (string.IsNullOrEmpty(observed)) observed = "unknown";
                var tip = observed == "unknown" ? "Show the whole hand, then try the letter again." : AlphabetTip(fingerspellLetters[fingerspellIndex].ToString(), observed);
                alphabetStatus.text = tip;
                tutor?.Say(tip, true);
                return;
            }
            fingerspellScores.Add(inputMode == InputMode.Demo ? .75f : SaslConfidence());
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
            if (liveView != null)
                liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.18f, -.20f)),
                    Quaternion.Euler(0, 180, 0), .90f);
            UpdateLearnerHandPicture("CartoonHandGreeting");
            trackingText.text = current.tracked ? inputMode == InputMode.Demo ? "YOUR DEMO HAND  /  RIGHT" : inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR VR HAND  /  RIGHT  /  LIVE" : inputMode == InputMode.Camera && !webcam.IsReceiving ?
                "CAMERA  /  WAITING  /  RIGHT HAND" : "YOUR RIGHT HAND  /  NOT VISIBLE";
            var reviewedGreeting = inputMode == InputMode.Camera && current.tracked &&
                webcam.RecognizedLabel == "REVIEWED_SASL_HELLO" && webcam.RecognitionConfidence >= .80f;
            alphabetStatus.text = reviewedGreeting ? $"Bob recognises the reviewed SASL greeting  /  {webcam.RecognitionConfidence:P0}. You can save a scored attempt."
                : current.tracked ? "Hand visible. Copy the movement slowly and clearly."
                : inputMode == InputMode.Camera && !webcam.IsReceiving ? "Camera is not sending frames yet. Allow Camera access." :
                inputMode == InputMode.Camera ? "Show your RIGHT hand to the webcam." :
                inputMode == InputMode.VR ? "Raise your RIGHT hand in front of you." : "Choose a demo pose to practise the movement.";
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
            var reviewed = inputMode == InputMode.Camera && webcam.RecognizedLabel == "REVIEWED_SASL_HELLO" && webcam.RecognitionConfidence >= .80f;
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
            var webcamAvailable = WebcamModeAvailable();
            // Do not leave the selector showing a mode that cannot be used in
            // this session. Demo remains the safe offline fallback until the
            // OpenXR headset or simulator is running.
            if (inputMode == InputMode.Camera && !webcamAvailable)
                inputMode = openXr.IsConnected ? InputMode.VR : InputMode.Demo;
            BeginPage(Page.Input);
            ClassroomUi.Label("How would you like to practise?", content, -3.06f, .55f, .29f, ClassroomUi.Ink);
            ClassroomUi.Label("Choose one input. You can change it between sessions.", content, -3.06f, .16f, .155f, ClassroomUi.Muted);
            var names = new[] { "Webcam", "VR hands", "Try a demo" };
            var descriptions = new[] { webcamAvailable ? "Use your Mac's camera.\nQuest USB bridge is ready.\nAllow Camera access." : "Connect Quest by USB-C\nand start the launcher.\nThen Webcam unlocks.",
                "Use the simulator or headset.\nFollow your tracked hands.", "Use the on-screen controls.\nExplore without a camera." };
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var x = (i - 1) * 2.09f;
                var webcamDisabled = i == (int)InputMode.Camera && !webcamAvailable;
                var tileColor = webcamDisabled ? new Color(.90f, .93f, .94f) : ClassroomUi.Paper;
                var titleColor = webcamDisabled ? ClassroomUi.Muted : ClassroomUi.DeepBlue;
                var tile = ClassroomUi.Panel(names[i], content, new Vector3(x, -.70f, 0), new Vector2(1.94f, 1.42f),
                    tileColor, .16f, true).transform;
                ClassroomUi.Label(names[i], tile, 0, .40f, .23f, titleColor, TextAnchor.MiddleCenter);
                ClassroomUi.Label(descriptions[i], tile, 0, -.03f, .125f, ClassroomUi.Muted, TextAnchor.MiddleCenter, 1.74f);
                var selected = inputMode == (InputMode)i && !webcamDisabled;
                var buttonLabel = webcamDisabled ? "Unavailable" : selected ? "Selected" : "Choose";
                ClassroomUi.Button(buttonLabel, tile, 0, -.48f, 1.55f,
                    () => { if (index == (int)InputMode.Camera && !WebcamModeAvailable()) return; inputMode = (InputMode)index; ShowHome(); },
                    selected, .33f, webcamDisabled);
            }
            ClassroomUi.Button("<  Back", content, -2.48f, -1.94f, 1.2f, ShowHome);
        }
        bool WebcamModeAvailable() => openXr != null && openXr.IsConnected && webcam != null && webcam.EndpointReady;
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
            else
                CreateLiveInputView("Learner hand", liveCard, new Vector3(0, -.05f, -.06f), new Vector2(1.70f, 1.00f));
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
            if (!preview && XRSettings.isDeviceActive && presentationRoot != null && presentationRoot.localScale.x > .99f)
                PlaceVrClassroom();
            UpdateProfileKeyboard();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (page == Page.AddProfile) ShowProfiles();
                else if (page != Page.Profiles) ShowHome();
            }
            if (page == Page.Profiles || page == Page.AddProfile) return;
            if (page == Page.Alphabet)
            {
                if (alphabetSuccess) UpdateAlphabetSuccessPrompt();
                else UpdateAlphabet();
                return;
            }
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
            var angle = Mathf.Sin((Time.time - replayStarted) * .4f) * 12f;
            if (targetView != null)
                targetView.SetPose(target, content.TransformPoint(new Vector3(-1.67f, -1.18f, -.20f)), Quaternion.Euler(0, 180 + angle, 0), .92f);
            if (liveView != null)
                liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -1.18f, -.20f)), Quaternion.Euler(0, 180, 0), .92f);
            var handTexture = lessonIndex == 0 ? "CartoonHandOpen" : lessonIndex == 1 ? "CartoonHandFist" : "CartoonHandPoint";
            UpdateLearnerHandPicture(handTexture);
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

        void UpdateSaslMatch(string expected)
        {
            var previous = stableSaslLetter;
            if (!current.tracked)
            {
                saslScore = new GestureScore("NO_HAND", 0f, float.PositiveInfinity);
                stableSaslLetter = "";
                stableSaslSince = Time.time;
                saslMatchHeld = 0;
                return;
            }

            if (inputMode == InputMode.Camera)
            {
                var label = webcam?.RecognizedLabel ?? "";
                var letter = label.StartsWith("TEAM_SASL_", StringComparison.Ordinal) && label.Length == 11
                    ? label.Substring(10) : "";
                saslScore = new GestureScore(letter, webcam?.RecognitionConfidence ?? 0f, 0f);
            }
            else if (inputMode == InputMode.VR)
            {
                saslScore = saslEvaluator?.Evaluate(current) ?? new GestureScore("UNKNOWN", 0f, float.PositiveInfinity);
            }
            else
            {
                saslScore = new GestureScore("", 0f, float.PositiveInfinity);
            }

            var candidate = saslScore.label ?? "";
            if (candidate != previous)
            {
                stableSaslLetter = candidate;
                stableSaslSince = Time.time;
            }

            var stable = string.Equals(stableSaslLetter, expected, StringComparison.Ordinal) &&
                Time.time - stableSaslSince >= .18f && saslScore.confidence >= SaslThreshold();
            saslMatchHeld = stable ? saslMatchHeld + Time.deltaTime : 0;
        }

        string SaslLetter() => Time.time - stableSaslSince >= .18f ? stableSaslLetter : "";
        float SaslConfidence() => saslScore.confidence;
        float SaslThreshold() => .80f;
        string SaslSourceName() => inputMode == InputMode.Camera ? "webcam" : inputMode == InputMode.VR ? "VR template" : "demo";
        bool SaslMatches(string expected) => string.Equals(SaslLetter(), expected, StringComparison.Ordinal) &&
            SaslConfidence() >= SaslThreshold();

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

        void ShowAlphabetSuccessPrompt()
        {
            if (successPrompt != null || content == null) return;
            successDeadline = Time.time + 15f;
            content.gameObject.SetActive(false);
            successModalRoot = new GameObject("Alphabet success modal layer").transform;
            successModalRoot.SetParent(board, false);
            successModalRoot.localPosition = new Vector3(0, 0, -.72f);
            successPrompt = ClassroomUi.Panel("Correct alphabet sign prompt", successModalRoot, new Vector3(0, -.42f, 0),
                new Vector2(4.75f, 2.08f), new Color(.98f, 1f, .99f), .22f, true).transform;
            var letter = ((char)('A' + alphabetIndex)).ToString();
            ClassroomUi.Label("SIGN CORRECT!", successPrompt, 0, .67f, .25f, ClassroomUi.Green,
                TextAnchor.MiddleCenter);
            ClassroomUi.Label($"Bob matched {letter}  /  {SaslSourceName()} {successMatch:P0}.", successPrompt, 0, .34f, .13f,
                ClassroomUi.Ink, TextAnchor.MiddleCenter, 4.2f);
            var nextPromptButton = ClassroomUi.Button(alphabetIndex == 25 ? "Finish alphabet  (15)" : "Next letter  (15)",
                successPrompt, -1.10f, -.55f, 1.95f, AdvanceAfterAlphabetSuccess, true, .38f);
            successCountdown = nextPromptButton.GetComponentInChildren<TextMesh>();
            ClassroomUi.Button("Try again", successPrompt, 1.10f, -.55f, 1.65f,
                RetryAfterAlphabetSuccess, false, .38f);
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

        void UpdateAlphabetSuccessPrompt()
        {
            if (successPrompt == null) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Space))
            {
                AdvanceAfterAlphabetSuccess();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                RetryAfterAlphabetSuccess();
                return;
            }
            var remaining = Mathf.Max(0, Mathf.CeilToInt(successDeadline - Time.time));
            successCountdown.text = remaining > 0
                ? alphabetIndex == 25 ? $"Finish alphabet  ({remaining})" : $"Next letter  ({remaining})"
                : "Opening…";
            if (remaining <= 0) AdvanceAfterAlphabetSuccess();
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

        void AdvanceAfterAlphabetSuccess()
        {
            alphabetSuccess = false;
            if (alphabetIndex == 25) ShowHome(); else ShowAlphabet(alphabetIndex + 1);
        }

        void RetryAfterAlphabetSuccess()
        {
            alphabetSuccess = false;
            ShowAlphabet(alphabetIndex);
            tutor?.Say("No problem. Let’s try that handshape once more.", true);
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
            HelpRow("01", "Choose an input", "Webcam unlocks when the Quest is connected by USB-C and the Mac bridge is running.\nVR hands stays the default; Demo needs no camera.", -.02f);
            HelpRow("02", "Watch, then try", "Keep your full hand in view and use good, even lighting.\nBob's peach hand is the example; the light-grey tracked hand is yours.", -.77f);
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
            trackingText = ClassroomUi.Label("Show your RIGHT hand to see a research prediction.", content,
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
            ClassroomUi.Label("BOB'S ANIMATED REFERENCE", example, -1.17f, .49f, .105f, ClassroomUi.DeepBlue, maxWidth: 2.55f);
            trackingText = ClassroomUi.Label("TRACKED HAND  /  RIGHT", learner, -1.17f, .49f, .105f, ClassroomUi.Green, maxWidth: 2.55f);
            // Play the attributed research landmark sequence on the board,
            // using the same Bob-style hand renderer as the learner card.
            targetView = new CartoonHandReplayView("Bob research example hand", content, new Color(.97f, .73f, .57f), true);
            if (inputMode == InputMode.Demo)
                CreateLearnerHandPicture(learner, new Vector3(0, -.05f, -.12f), new Vector2(1.24f, 1.24f), "CartoonHandOpen");
            else
                CreateLiveInputView("Research learner hand", learner, new Vector3(0, -.05f, -.12f), new Vector2(1.70f, 1.00f));
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
            // The recorded Explore reference is deliberately shown at half speed
            // so the learner can read both handshape and movement. The optional
            // slow toggle remains a further 1.65x reduction.
            var duration = references.DurationSeconds * (slowReplay ? 3.3f : 2f);
            var motion = references.GetPose(researchIndex, (Time.time - researchStarted) / duration, researchReference);
            if (targetView != null)
                targetView.SetPose(researchReference,
                    content.TransformPoint(new Vector3(-1.67f, -.78f, -.20f) + motion),
                    Quaternion.Euler(0, 180, 0), .72f);
            current.tracked = false; TryGetInputHand(current);
            webcamPreview?.SetBase64(inputMode == InputMode.Camera ? webcam.PreviewJpeg : "");
            if (liveView != null)
                liveView.SetPose(current, content.TransformPoint(new Vector3(1.67f, -.78f, -.20f)), Quaternion.Euler(0, 180, 0), .72f);
            UpdateLearnerHandPicture("CartoonHandOpen");
            trackingText.text = current.tracked ? inputMode == InputMode.Demo ? "YOUR DEMO HAND  /  RIGHT" : inputMode == InputMode.Camera ? WebcamHandLabel() : "YOUR VR HAND  /  RIGHT  /  LIVE" : inputMode == InputMode.Camera && !webcam.IsReceiving ? "CAMERA  /  WAITING  /  RIGHT HAND" :
                "YOUR HAND  /  NOT VISIBLE";
            feedbackText.text = !current.tracked ? inputMode == InputMode.Camera ? "Show your RIGHT hand to the webcam to try the movement." :
                inputMode == InputMode.VR ? "Raise your RIGHT hand to try the movement." : "Choose a demo pose to try the movement." :
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
                current.tracked = false;
                TryGetInputHand(current);
                if (!current.tracked)
                {
                    trackingText.text = inputMode == InputMode.Camera && !webcam.IsReceiving
                        ? "Camera is not sending frames yet."
                        : inputMode == InputMode.Camera ? $"Show your {HandName()} hand to the webcam."
                        : inputMode == InputMode.VR ? "Raise your RIGHT hand to see the tracked view."
                        : "Choose a demo pose to preview the research view.";
                }
                else if (inputMode == InputMode.Camera && webcam.RecognizedLabel.StartsWith("RESEARCH_SASL_"))
                {
                    trackingText.text = "Possible sign: " + webcam.RecognizedLabel.Replace("RESEARCH_SASL_", "").Replace('_', ' ') +
                        $"  /  similarity {webcam.RecognitionConfidence:P0}";
                }
                else
                {
                    trackingText.text = inputMode == InputMode.VR ? "VR hand visible. Watch the reference movement." :
                        inputMode == InputMode.Demo ? "Demo hand visible. Watch the reference movement." : "Watching your movement...";
                }
                yield return new WaitForSeconds(.1f);
            }
        }

        void BuildCamera()
        {
            var item = new GameObject("Learner camera"); item.tag = "MainCamera";
            item.transform.position = new Vector3(0, 1.85f, -2.6f);
            var camera = item.AddComponent<Camera>(); camera.nearClipPlane = .03f; camera.farClipPlane = 50;
            learnerCamera = camera;
            if (Application.platform != RuntimePlatform.Android) camera.fieldOfView = 50f;
            camera.backgroundColor = ClassroomFactory.Sky;
            item.AddComponent<AudioListener>(); item.AddComponent<DesktopCameraController>();
            item.AddComponent<BoardPointer>(); item.AddComponent<HeadPoseDriver>();
            item.AddComponent<MacOpenXRBootstrap>();
        }

        IEnumerator ConnectBackend(int revision)
        {
            if (activeProfile == null) yield break;
            var suffix = activeProfile.id == LearnerProfileStore.DefaultProfileId ? "" : "-" + activeProfile.id;
            var email = $"demo-{SystemInfo.deviceUniqueIdentifier}{suffix}@local.bcsignvr";
            var payload = JsonUtility.ToJson(new AuthRequest { name = activeProfile.displayName, email = email, password = "local-demo-password" });
            using var register = Request("http://127.0.0.1:8787/api/auth/register", "POST", payload);
            yield return register.SendWebRequest();
            if (revision != profileRevision) yield break;
            var response = register.downloadHandler.text;
            if (register.responseCode == 409)
            {
                using var login = Request("http://127.0.0.1:8787/api/auth/login", "POST", payload);
                yield return login.SendWebRequest();
                if (revision != profileRevision) yield break;
                if (login.result != UnityWebRequest.Result.Success) { UpdateConnectionLabel(); yield break; }
                response = login.downloadHandler.text;
            }
            else if (register.result != UnityWebRequest.Result.Success) { UpdateConnectionLabel(); yield break; }
            AuthResponse auth = null;
            try { auth = JsonUtility.FromJson<AuthResponse>(response); }
            catch (Exception error) { Debug.LogWarning("Account connection could not be read: " + error.Message); }
            token = auth?.token;
            apiConnected = !string.IsNullOrEmpty(token);
            UpdateConnectionLabel();
            if (apiConnected && !preview)
            {
                yield return SyncPending();
                if (revision != profileRevision) yield break;
                yield return RefreshRemoteProgress();
            }
        }

        IEnumerator RefreshRemoteProgress()
        {
            if (!apiConnected || string.IsNullOrEmpty(token)) yield break;
            var revision = profileRevision;
            using var request = Request("http://127.0.0.1:8787/api/progress", "GET", null);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            yield return request.SendWebRequest();
            if (revision != profileRevision) yield break;
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
            var revision = profileRevision;
            var store = history;
            var sessionToken = token;
            syncing = true;
            foreach (var record in store.Pending)
            {
                if (revision != profileRevision) yield break;
                using var request = Request("http://127.0.0.1:8787/api/sessions", "POST", JsonUtility.ToJson(record));
                request.SetRequestHeader("Authorization", $"Bearer {sessionToken}");
                yield return request.SendWebRequest();
                if (revision != profileRevision) yield break;
                if (request.result != UnityWebRequest.Result.Success) { apiConnected = false; break; }
                try { store.MarkSynced(record.id); saveStatus = "Progress saved and synced"; }
                catch (Exception error) { Debug.LogWarning(error.Message); break; }
            }
            if (revision == profileRevision)
            {
                syncing = false;
                UpdateConnectionLabel();
            }
        }

        void UpdateConnectionLabel()
        {
            if (connectionText == null) return;
            connectionText.text = "";
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
            else if (scenario == "input")
            {
                // The simulator's OpenXR loader starts from another component's
                // Start method. Let it finish before taking the input-menu
                // preview, otherwise a connected runtime looks disconnected.
                if (Environment.GetEnvironmentVariable("BCSIGNVR_XR") == "1")
                    yield return new WaitForSeconds(.8f);
                CycleMode();
            }
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
