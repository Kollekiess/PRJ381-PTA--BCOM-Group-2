using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace BCSignVR.Tests
{
    public sealed class LearnerProfileStoreTests
    {
        string directory;
        string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "BCSignVR-profile-tests-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "profiles.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void FirstRunCreatesOneUsableDefaultProfileAndPersistsIt()
        {
            var store = new LearnerProfileStore(path);
            Assert.AreEqual(1, store.Profiles.Count);
            Assert.AreEqual(LearnerProfileStore.DefaultProfileId, store.Profiles[0].id);
            Assert.AreEqual("Local Learner", new LearnerProfileStore(path).Profiles.Single().displayName);
        }

        [Test]
        public void AddedProfilesSurviveReloadAndMayShareDisplayNames()
        {
            var store = new LearnerProfileStore(path);
            var first = store.Add("Asha");
            var second = store.Add("Asha");
            var restored = new LearnerProfileStore(path).Profiles;

            Assert.AreEqual(3, restored.Count);
            Assert.AreNotEqual(first.id, second.id);
            Assert.AreEqual("Asha", restored[1].displayName);
            Assert.AreEqual("Asha", restored[2].displayName);
        }

        [Test]
        public void InvalidNamesAreRejectedWithoutChangingSavedProfiles()
        {
            var store = new LearnerProfileStore(path);
            Assert.Throws<ArgumentException>(() => store.Add("  "));
            Assert.Throws<ArgumentException>(() => store.Add(new string('x', 29)));
            Assert.AreEqual(1, new LearnerProfileStore(path).Profiles.Count);
        }

        [Test]
        public void CorruptProfileFileIsPreservedBeforeRecovery()
        {
            Directory.CreateDirectory(directory);
            const string brokenJson = "{interrupted profile write";
            File.WriteAllText(path, brokenJson);
            var store = new LearnerProfileStore(path);
            Assert.AreEqual(1, store.Profiles.Count);
            Assert.AreEqual(brokenJson, File.ReadAllText(store.RecoveryCopyPath));
            Assert.AreEqual(1, new LearnerProfileStore(path).Profiles.Count);
        }
    }
}
