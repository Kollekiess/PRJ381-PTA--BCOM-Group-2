using System;

namespace BCSignVR
{
    /// <summary>Word labels and clip counts from Chantel's Updated-Hand-signs upload.</summary>
    public static class ChantelWordCatalog
    {
        [Serializable]
        public sealed class Entry
        {
            public string label;
            public string displayName;
            public int clipCount;
            public string helper;

            public Entry(string label, string displayName, int clipCount, string helper)
            {
                this.label = label;
                this.displayName = displayName;
                this.clipCount = clipCount;
                this.helper = helper;
            }
        }

        static readonly Entry[] entries =
        {
            new("HELLO", "Hello", 24, "A greeting sequence"),
            new("GOODBYE", "Goodbye", 25, "A farewell sequence"),
            new("NICE_TO_MEET_YOU", "Nice to meet you", 14, "A longer greeting phrase"),
            new("PLEASE", "Please", 30, "A polite request"),
            new("THANK_YOU", "Thank you", 26, "A polite thanks"),
        };

        public static int Count => entries.Length;
        public static Entry Get(int index) => index >= 0 && index < entries.Length ? entries[index] : null;

        public static int IndexOf(string label)
        {
            if (string.IsNullOrEmpty(label)) return -1;
            for (var i = 0; i < entries.Length; i++)
                if (string.Equals(entries[i].label, label, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
