using System;
using System.Collections.Generic;
using UnityEngine;

namespace BCSignVR
{
    /// <summary>
    /// Replays the original two-hand landmark clips uploaded with Chantel's
    /// word model. This is a visual reference library, not live word inference.
    /// </summary>
    public sealed class SaslWordPlaybackLibrary
    {
        [Serializable]
        sealed class Playback
        {
            public string format;
            public string source;
            public string notice;
            public int frameCount;
            public int featureCount;
            public Word[] words;
        }

        [Serializable]
        sealed class Word
        {
            public string label;
            public int clipCount;
            public Frame[] frames;
        }

        [Serializable]
        sealed class Frame
        {
            public float[] features;
        }

        readonly Word[] words;
        readonly Vector3[] rawPoints = new Vector3[21];
        public string Source { get; } = "";
        public string Notice { get; } = "";
        public int Count => words.Length;
        public int FrameCount { get; } = 30;

        public SaslWordPlaybackLibrary()
        {
            var asset = Resources.Load<TextAsset>("sasl_chantel_words");
            if (asset == null)
            {
                words = Array.Empty<Word>();
                Debug.LogWarning("Chantel word playback resource was not found.");
                return;
            }

            var playback = JsonUtility.FromJson<Playback>(asset.text);
            Source = playback?.source ?? "";
            Notice = playback?.notice ?? "";
            FrameCount = Mathf.Max(1, playback?.frameCount ?? 30);
            var valid = new List<Word>();
            foreach (var word in playback?.words ?? Array.Empty<Word>())
                if (IsValid(word)) valid.Add(word);
            words = valid.ToArray();
        }

        public string Label(int index) => HasIndex(index) ? words[index].label : "";
        public int ClipCount(int index) => HasIndex(index) ? words[index].clipCount : 0;

        public bool GetPoses(int index, float phase, HandPoseFrame leftOutput, HandPoseFrame rightOutput)
        {
            SetUntracked(leftOutput, "Chantel word preview");
            SetUntracked(rightOutput, "Chantel word preview");
            if (!HasIndex(index) || !Finite(phase)) return false;
            var word = words[index];
            var position = Mathf.Repeat(phase, 1f) * (word.frames.Length - 1);
            var lower = Mathf.FloorToInt(position);
            var upper = Mathf.Min(lower + 1, word.frames.Length - 1);
            var amount = position - lower;
            var from = word.frames[lower].features;
            var to = word.frames[upper].features;
            PopulateHand(Interpolate(from, to, amount, 0), leftOutput);
            PopulateHand(Interpolate(from, to, amount, 63), rightOutput);
            return leftOutput.tracked || rightOutput.tracked;
        }

        static float[] Interpolate(float[] from, float[] to, float amount, int offset)
        {
            var features = new float[63];
            var present = false;
            for (var i = 0; i < features.Length; i++)
            {
                features[i] = Mathf.Lerp(from[offset + i], to[offset + i], amount);
                present |= Mathf.Abs(features[i]) > .0001f;
            }
            return present ? features : null;
        }

        void PopulateHand(float[] features, HandPoseFrame output)
        {
            if (features == null || features.Length != 63)
            {
                SetUntracked(output, "Chantel word preview");
                return;
            }

            var wrist = Point(features, 0);
            for (var i = 0; i < rawPoints.Length; i++)
            {
                var point = Point(features, i);
                rawPoints[i] = new Vector3(-(point.x - wrist.x), -(point.y - wrist.y), -(point.z - wrist.z));
            }

            output.joints[0] = Vector3.zero;
            output.joints[1] = (rawPoints[0] + rawPoints[5] + rawPoints[9] + rawPoints[13] + rawPoints[17]) / 5f;
            for (var i = 0; i < 4; i++) output.joints[2 + i] = rawPoints[1 + i];
            MapFinger(output, 6, 5);
            MapFinger(output, 11, 9);
            MapFinger(output, 16, 13);
            MapFinger(output, 21, 17);
            output.wristRotation = Quaternion.identity;
            output.tracked = true;
            output.source = "Chantel recorded word sequence";
        }

        void MapFinger(HandPoseFrame output, int targetStart, int sourceStart)
        {
            output.joints[targetStart] = Vector3.Lerp(rawPoints[0], rawPoints[sourceStart], .55f);
            for (var i = 0; i < 4; i++) output.joints[targetStart + 1 + i] = rawPoints[sourceStart + i];
        }

        static Vector3 Point(float[] features, int index)
        {
            var offset = index * 3;
            return new Vector3(features[offset], features[offset + 1], features[offset + 2]);
        }

        static void SetUntracked(HandPoseFrame output, string source)
        {
            if (output == null) return;
            output.tracked = false;
            output.source = source;
        }

        static bool IsValid(Word word)
        {
            if (word == null || string.IsNullOrEmpty(word.label) || word.frames == null || word.frames.Length < 2) return false;
            foreach (var frame in word.frames)
                if (frame == null || frame.features == null || frame.features.Length != 126) return false;
            return true;
        }

        bool HasIndex(int index) => index >= 0 && index < words.Length;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
