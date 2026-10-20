using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BCSignVR
{
    [Serializable]
    public sealed class LocalSessionRecord
    {
        public string id;
        public string lessonId;
        public float accuracy;
        public float durationSeconds;
        public int mistakes;
        public bool completed;
        public bool synced;
        public string inputSource;

        public bool IsDemo => string.Equals(inputSource, "demo", StringComparison.OrdinalIgnoreCase);
        public bool IsReference => string.Equals(inputSource, "reference", StringComparison.OrdinalIgnoreCase);
        public bool IsScored => !IsDemo && !IsReference;
        public bool IsSyncable => !IsDemo;

        internal LocalSessionRecord Copy() => (LocalSessionRecord)MemberwiseClone();
    }

    /// <summary>
    /// Durable local history and upload queue. Add returns only after a record has been saved.
    /// Demo attempts remain in history but never enter the upload queue or learner statistics.
    /// Use the returned record's id as the server idempotency key when uploading it.
    /// </summary>
    public sealed class LocalSessionStore
    {
        const int CurrentSchemaVersion = 1;
        readonly List<LocalSessionRecord> records = new();
        bool unreadableFile;

        public string FilePath { get; }
        public string RecoveryMessage { get; private set; }
        public string RecoveryCopyPath { get; private set; }
        public int Count => records.Count;
        public int TotalCompleted => records.Count(record => record.completed && record.IsScored);
        public int DemoCompletedCount => records.Count(record => record.completed && record.IsDemo);
        public int AlphabetPractisedCount => records.Where(record => record.completed && record.IsReference &&
            record.lessonId.StartsWith("alphabet-", StringComparison.OrdinalIgnoreCase))
            .Select(record => record.lessonId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        public int ResearchPractisedCount => records.Where(record => record.completed && record.lessonId.StartsWith("research-sasl-", StringComparison.OrdinalIgnoreCase))
            .Select(record => record.lessonId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        public int PendingCount => records.Count(record => !record.synced && record.IsSyncable);
        public float BestAccuracy => records.Where(record => record.completed && record.IsScored)
            .Select(record => record.accuracy).DefaultIfEmpty(0f).Max();

        // Copies prevent callers from changing saved data without a successful disk write.
        public IReadOnlyList<LocalSessionRecord> Records => records.Select(record => record.Copy()).ToArray();
        public IEnumerable<LocalSessionRecord> Pending => records
            .Where(record => !record.synced && record.IsSyncable).Select(record => record.Copy()).ToArray();

        /// <param name="path">Optional complete filename, useful for tests and separate learner profiles.</param>
        public LocalSessionStore(string path = null)
        {
            FilePath = Path.GetFullPath(path ?? Path.Combine(Application.persistentDataPath, "sessions.json"));
            Load();
        }

        /// <summary>
        /// Persists a copy and returns that copy with its generated id. I/O failures are thrown;
        /// a failed save does not appear in history or inflate progress.
        /// </summary>
        public LocalSessionRecord Add(LocalSessionRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            var saved = record.Copy();
            if (string.IsNullOrWhiteSpace(saved.id)) saved.id = Guid.NewGuid().ToString("N");
            Validate(saved);
            if (records.Any(existing => existing.id == saved.id))
                throw new ArgumentException("A session with that id already exists.", nameof(record));

            records.Add(saved);
            try { Persist(); }
            catch
            {
                records.Remove(saved);
                throw;
            }
            return saved.Copy();
        }

        /// <summary>Call only after the server confirms that it stored this id.</summary>
        public bool MarkSynced(string id)
        {
            var record = records.Find(candidate => candidate.id == id);
            if (record == null) return false;
            if (record.synced) return true;
            record.synced = true;
            try { Persist(); }
            catch
            {
                record.synced = false;
                throw;
            }
            return true;
        }

        void Load()
        {
            if (!File.Exists(FilePath)) return;
            try
            {
                var envelope = JsonUtility.FromJson<SessionEnvelope>(File.ReadAllText(FilePath));
                if (envelope == null || envelope.schemaVersion != CurrentSchemaVersion || envelope.records == null)
                    throw new InvalidDataException("The session history format is not recognised.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var record in envelope.records)
                {
                    Validate(record);
                    if (!ids.Add(record.id)) throw new InvalidDataException("The session history contains duplicate ids.");
                }
                records.AddRange(envelope.records);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                          error is ArgumentException || error is InvalidOperationException)
            {
                // Leave the original untouched. A later save first preserves a separate recovery copy.
                unreadableFile = true;
                RecoveryMessage = "Previous session history could not be read. It has been preserved. " + error.Message;
            }
        }

        void Persist()
        {
            var directory = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(directory);
            var temporaryPath = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var envelope = new SessionEnvelope { schemaVersion = CurrentSchemaVersion, records = records };
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
                    var recoveryPath = FilePath + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") +
                                       "-" + Guid.NewGuid().ToString("N");
                    File.Copy(FilePath, recoveryPath);
                    RecoveryCopyPath = recoveryPath;
                }
                if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
                else File.Move(temporaryPath, FilePath);
                unreadableFile = false;
            }
            finally
            {
                // Never leave a partially written file in place of a valid history.
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        static void Validate(LocalSessionRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.id) || string.IsNullOrWhiteSpace(record.lessonId) ||
                string.IsNullOrWhiteSpace(record.inputSource))
                throw new InvalidDataException("A session needs an id, lesson, and input source.");
            if (float.IsNaN(record.accuracy) || float.IsInfinity(record.accuracy) || record.accuracy < 0f || record.accuracy > 1f ||
                float.IsNaN(record.durationSeconds) || float.IsInfinity(record.durationSeconds) || record.durationSeconds < 0f ||
                record.mistakes < 0)
                throw new InvalidDataException("A session contains invalid measurements.");
        }

        [Serializable]
        sealed class SessionEnvelope
        {
            public int schemaVersion;
            public List<LocalSessionRecord> records;
        }
    }
}
