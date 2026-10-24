using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BCSignVR
{
    [Serializable]
    public sealed class LearnerProfile
    {
        public string id;
        public string displayName;
        public int avatarIndex;

        internal LearnerProfile Copy() => (LearnerProfile)MemberwiseClone();
    }

    /// <summary>
    /// On-headset profile directory. Each profile gets its own local progress file,
    /// while the directory itself survives app restarts and Quest updates.
    /// </summary>
    public sealed class LearnerProfileStore
    {
        const int CurrentSchemaVersion = 1;
        readonly List<LearnerProfile> profiles = new();
        bool unreadableFile;

        public const string DefaultProfileId = "local-learner";
        public string FilePath { get; }
        public string RecoveryCopyPath { get; private set; }
        public IReadOnlyList<LearnerProfile> Profiles => profiles.Select(profile => profile.Copy()).ToArray();

        public LearnerProfileStore(string path = null)
        {
            FilePath = Path.GetFullPath(path ?? Path.Combine(Application.persistentDataPath, "learner-profiles.json"));
            Load();
            if (profiles.Count == 0)
            {
                profiles.Add(new LearnerProfile
                {
                    id = DefaultProfileId,
                    displayName = "Local Learner",
                    avatarIndex = 0
                });
                Persist();
            }
        }

        public LearnerProfile Add(string displayName)
        {
            var name = (displayName ?? string.Empty).Trim();
            if (name.Length == 0) throw new ArgumentException("Enter a learner name.", nameof(displayName));
            if (name.Length > 28) throw new ArgumentException("Learner names must be 28 characters or fewer.", nameof(displayName));

            var profile = new LearnerProfile
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = name,
                avatarIndex = profiles.Count % 6
            };
            profiles.Add(profile);
            try { Persist(); }
            catch
            {
                profiles.Remove(profile);
                throw;
            }
            return profile.Copy();
        }

        void Load()
        {
            if (!File.Exists(FilePath)) return;
            try
            {
                var envelope = JsonUtility.FromJson<ProfileEnvelope>(File.ReadAllText(FilePath));
                if (envelope == null || envelope.schemaVersion != CurrentSchemaVersion || envelope.profiles == null)
                    throw new InvalidDataException("The learner profile format is not recognised.");

                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var profile in envelope.profiles)
                {
                    Validate(profile);
                    if (!ids.Add(profile.id)) throw new InvalidDataException("The learner profile list contains duplicate ids.");
                }
                profiles.AddRange(envelope.profiles.Select(profile => profile.Copy()));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                          error is ArgumentException || error is InvalidOperationException)
            {
                unreadableFile = true;
                Debug.LogWarning("Learner profiles could not be read; the original file will be preserved before recovery. " + error.Message);
            }
        }

        void Persist()
        {
            var directory = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(directory);
            var temporaryPath = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var envelope = new ProfileEnvelope { schemaVersion = CurrentSchemaVersion, profiles = profiles };
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    {
                        writer.Write(JsonUtility.ToJson(envelope, true));
                        writer.Flush();
                    }
                    stream.Flush(true);
                }
                if (unreadableFile && File.Exists(FilePath))
                {
                    RecoveryCopyPath = FilePath + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") +
                                       "-" + Guid.NewGuid().ToString("N");
                    File.Copy(FilePath, RecoveryCopyPath);
                }
                if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
                else File.Move(temporaryPath, FilePath);
                unreadableFile = false;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        static void Validate(LearnerProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.id) || string.IsNullOrWhiteSpace(profile.displayName) ||
                profile.displayName.Length > 28 || profile.avatarIndex < 0 || profile.avatarIndex >= 6)
                throw new InvalidDataException("A learner profile has invalid details.");
        }

        [Serializable]
        sealed class ProfileEnvelope
        {
            public int schemaVersion;
            public List<LearnerProfile> profiles;
        }
    }
}
