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
        public void FirstRunCreatesDefaultProfileAndPersistsAddedLearner()
        {
            var first = new LearnerProfileStore(path);
            Assert.AreEqual(1, first.Profiles.Count);
            Assert.AreEqual(LearnerProfileStore.DefaultProfileId, first.Profiles[0].id);

            var added = first.Add("Presentation learner");
            var restored = new LearnerProfileStore(path).Profiles;
            Assert.AreEqual(2, restored.Count);
            Assert.IsTrue(restored.Any(profile => profile.id == added.id && profile.displayName == "Presentation learner"));
        }

        [Test]
        public void InvalidProfileNamesAreRejected()
        {
            var store = new LearnerProfileStore(path);
            Assert.Throws<ArgumentException>(() => store.Add("   "));
            Assert.Throws<ArgumentException>(() => store.Add(new string('x', 29)));
            Assert.AreEqual(1, store.Profiles.Count);
        }
    }
}
