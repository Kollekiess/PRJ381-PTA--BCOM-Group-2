using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace BCSignVR.Tests
{
    public sealed class LocalSessionStoreTests
    {
        string directory;
        string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "BCSignVR-session-tests-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "sessions.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void OfflineAttemptSurvivesReloadAndSyncAcknowledgement()
        {
            var first = new LocalSessionStore(path);
            var saved = first.Add(Attempt("webcam", .84f));
            Assert.IsNotEmpty(saved.id);
            var restored = new LocalSessionStore(path);
            Assert.AreEqual(saved.id, restored.Pending.Single().id);
            Assert.AreEqual(1, restored.TotalCompleted);
            Assert.AreEqual(.84f, restored.BestAccuracy);
            Assert.IsTrue(restored.MarkSynced(saved.id));
            Assert.AreEqual(0, new LocalSessionStore(path).PendingCount);
            Assert.AreEqual(1, new LocalSessionStore(path).Count);
        }

        [Test]
        public void DemoAttemptsStayInHistoryWithoutInflatingLearnerProgress()
        {
            var store = new LocalSessionStore(path);
            store.Add(Attempt("demo", 1f));
            store.Add(Attempt("quest", .72f));
            Assert.AreEqual(2, store.Count);
            Assert.AreEqual(1, store.DemoCompletedCount);
            Assert.AreEqual(1, store.TotalCompleted);
            Assert.AreEqual(.72f, store.BestAccuracy);
            Assert.AreEqual("quest", store.Pending.Single().inputSource);
        }

        [Test]
        public void ReferencePracticeIsQueuedForAccountHistory()
        {
            var store = new LocalSessionStore(path);
            var saved = store.Add(new LocalSessionRecord
            {
                lessonId = "basic-greeting",
                accuracy = 0f,
                durationSeconds = 8f,
                mistakes = 0,
                completed = true,
                synced = false,
                inputSource = "reference"
            });
            Assert.AreEqual(1, store.PendingCount);
            Assert.AreEqual(saved.id, store.Pending.Single().id);
            Assert.AreEqual(0, store.TotalCompleted);
            Assert.IsTrue(store.MarkSynced(saved.id));
            Assert.AreEqual(0, new LocalSessionStore(path).PendingCount);
        }

        [Test]
        public void ResearchPracticeAppearsInItsOwnProgressCount()
        {
            var store = new LocalSessionStore(path);
            store.Add(new LocalSessionRecord
            {
                lessonId = "research-sasl-cape-town",
                accuracy = 0f,
                durationSeconds = 8f,
                completed = true,
                inputSource = "webcam-research-visual-practice"
            });
            store.Add(new LocalSessionRecord
            {
                lessonId = "research-sasl-cape-town",
                accuracy = 0f,
                durationSeconds = 9f,
                completed = true,
                inputSource = "webcam-research-visual-practice"
            });
            Assert.AreEqual(1, store.ResearchPractisedCount);
        }

        [Test]
        public void CorruptHistoryIsPreservedBeforeNewHistoryReplacesIt()
        {
            Directory.CreateDirectory(directory);
            const string brokenJson = "{this is an interrupted file";
            File.WriteAllText(path, brokenJson);
            var store = new LocalSessionStore(path);
            Assert.AreEqual(0, store.Count);
            Assert.IsNotEmpty(store.RecoveryMessage);
            Assert.AreEqual(brokenJson, File.ReadAllText(path));
            store.Add(Attempt("webcam", .8f));
            Assert.AreEqual(brokenJson, File.ReadAllText(store.RecoveryCopyPath));
            Assert.AreEqual(1, new LocalSessionStore(path).Count);
        }

        [Test]
        public void CallerCannotMutateAnAlreadySavedAttempt()
        {
            var store = new LocalSessionStore(path);
            var attempt = Attempt("webcam", .8f);
            var saved = store.Add(attempt);
            attempt.accuracy = 1f;
            saved.accuracy = 1f;
            store.Pending.Single().synced = true;
            Assert.AreEqual(.8f, store.BestAccuracy);
            Assert.AreEqual(1, store.PendingCount);
            Assert.AreEqual(.8f, new LocalSessionStore(path).BestAccuracy);
        }

        [Test]
        public void FailedDiskSaveDoesNotPretendAnAttemptWasStored()
        {
            // A regular file cannot serve as the parent directory for a session history.
            Directory.CreateDirectory(directory);
            var blocker = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blocker, "occupied");
            var store = new LocalSessionStore(Path.Combine(blocker, "sessions.json"));
            Assert.Throws<IOException>(() => store.Add(Attempt("webcam", .8f)));
            Assert.AreEqual(0, store.Count);
            Assert.AreEqual(0, store.TotalCompleted);
        }

        static LocalSessionRecord Attempt(string source, float accuracy) => new()
        {
            lessonId = "prototype-open",
            accuracy = accuracy,
            durationSeconds = 4.5f,
            mistakes = 1,
            completed = true,
            inputSource = source
        };
    }
}
