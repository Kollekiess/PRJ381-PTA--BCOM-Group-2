using System;
using System.Collections.Generic;
using UnityEngine;

namespace BCSignVR
{
    /// <summary>Plays a recorded, attributed SASL landmark sequence on the lesson board.</summary>
    public sealed class SaslReferenceLibrary
    {
        [Serializable] sealed class Playback
        {
            public string source;
            public string license;
            public string notice;
            public float durationSeconds = 4f;
            public Lesson[] lessons;
        }

        [Serializable] sealed class Lesson
        {
            public string label;
            public string title;
            public string sourceFile;
            public string sourceMd5;
            public Frame[] frames;
            [NonSerialized] public Vector2 motionCenter;
            [NonSerialized] public float motionScale;
        }

        [Serializable] sealed class Frame
        {
            public Vector3[] joints;
            public float wristX;
            public float wristY;
        }

        readonly Lesson[] lessons;
        readonly Vector3[] points = new Vector3[21];
        public int Count => lessons.Length;
        public string Source { get; } = "";
        public string License { get; } = "";
        public string Notice { get; } = "";
        public float DurationSeconds { get; } = 4f;

        public SaslReferenceLibrary()
        {
            var asset = Resources.Load<TextAsset>("sasl_playback");
            if (asset == null)
            {
                lessons = Array.Empty<Lesson>();
                Debug.LogWarning("SASL reference playback resource was not found.");
                return;
            }
            var playback = JsonUtility.FromJson<Playback>(asset.text);
            Source = playback.source ?? "";
            License = playback.license ?? "";
            Notice = playback.notice ?? "";
            DurationSeconds = Mathf.Max(1f, playback.durationSeconds);
            var valid = new List<Lesson>();
            foreach (var lesson in playback.lessons ?? Array.Empty<Lesson>())
            {
                if (!IsValid(lesson)) continue;
                PrepareMotion(lesson);
                valid.Add(lesson);
            }
            lessons = valid.ToArray();
        }

        public string Title(int index) => HasIndex(index) ? lessons[index].title : "SASL practice";
        public string Label(int index) => HasIndex(index) ? lessons[index].label : "";
        public string SourceFile(int index) => HasIndex(index) ? lessons[index].sourceFile : "";

        /// <param name="phase">Normalized playback phase. For a four-second loop pass Time.time / 4f.</param>
        /// <returns>Mirrored wrist movement offset in board world units, bounded to ±.18 X and ±.12 Y.</returns>
        public Vector3 GetPose(int index, float phase, HandPoseFrame output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (!HasIndex(index) || float.IsNaN(phase) || float.IsInfinity(phase))
            {
                output.tracked = false;
                output.source = "No SASL reference";
                return Vector3.zero;
            }
            var lesson = lessons[index];
            var framePosition = Mathf.Repeat(phase, 1f) * (lesson.frames.Length - 1);
            var lower = Mathf.FloorToInt(framePosition);
            var upper = Mathf.Min(lower + 1, lesson.frames.Length - 1);
            var t = framePosition - lower;
            var from = lesson.frames[lower];
            var to = lesson.frames[upper];
            for (var i = 0; i < points.Length; i++)
            {
                // This is the same camera-to-board axis basis as the live webcam provider.
                var point = Vector3.Lerp(from.joints[i], to.joints[i], t);
                points[i] = new Vector3(-point.x, -point.y, -point.z);
            }

            output.joints[0] = Vector3.zero;
            output.joints[1] = (points[0] + points[5] + points[9] + points[13] + points[17]) / 5f;
            for (var i = 0; i < 4; i++) output.joints[2 + i] = points[1 + i];
            MapFinger(output, 6, 5);
            MapFinger(output, 11, 9);
            MapFinger(output, 16, 13);
            MapFinger(output, 21, 17);
            output.wristRotation = Quaternion.identity;
            output.tracked = true;
            output.source = "SASL reference: " + lesson.sourceFile;

            var wrist = Vector2.Lerp(new Vector2(from.wristX, from.wristY),
                new Vector2(to.wristX, to.wristY), t);
            var movement = (wrist - lesson.motionCenter) * -lesson.motionScale;
            return new Vector3(Mathf.Clamp(movement.x, -.18f, .18f),
                Mathf.Clamp(movement.y, -.12f, .12f), 0f);
        }

        bool HasIndex(int index) => index >= 0 && index < lessons.Length;

        void MapFinger(HandPoseFrame output, int targetStart, int sourceStart)
        {
            output.joints[targetStart] = Vector3.Lerp(points[0], points[sourceStart], .55f);
            for (var i = 0; i < 4; i++) output.joints[targetStart + 1 + i] = points[sourceStart + i];
        }

        static bool IsValid(Lesson lesson)
        {
            if (lesson == null || string.IsNullOrEmpty(lesson.label) || lesson.frames == null ||
                lesson.frames.Length == 0) return false;
            foreach (var frame in lesson.frames)
            {
                if (frame == null || frame.joints == null || frame.joints.Length != 21 ||
                    !Finite(frame.wristX) || !Finite(frame.wristY)) return false;
                foreach (var point in frame.joints)
                    if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) return false;
            }
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void PrepareMotion(Lesson lesson)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var frame in lesson.frames)
            {
                var point = new Vector2(frame.wristX, frame.wristY);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            lesson.motionCenter = (min + max) * .5f;
            var extent = (max - min) * .5f;
            // One scale for both axes preserves the recorded trajectory's proportions.
            // The upper scale limit prevents tiny tracking noise becoming a large movement.
            lesson.motionScale = Mathf.Min(.7f, .18f / Mathf.Max(.0001f, extent.x),
                .12f / Mathf.Max(.0001f, extent.y));
        }
    }
}
