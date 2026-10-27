using System.Linq;
using NUnit.Framework;

namespace BCSignVR.Tests
{
    public sealed class SaslWordPlaybackLibraryTests
    {
        [Test]
        public void ChantelWordCatalogMatchesUploadedSequenceLabelsAndClipCounts()
        {
            var expected = new[]
            {
                ("HELLO", 24),
                ("GOODBYE", 25),
                ("NICE_TO_MEET_YOU", 14),
                ("PLEASE", 30),
                ("THANK_YOU", 26),
            };
            Assert.That(ChantelWordCatalog.Count, Is.EqualTo(expected.Length));
            foreach (var item in expected)
            {
                var entry = ChantelWordCatalog.Get(ChantelWordCatalog.IndexOf(item.Item1));
                Assert.IsNotNull(entry);
                Assert.That(entry.label, Is.EqualTo(item.Item1));
                Assert.That(entry.clipCount, Is.EqualTo(item.Item2));
            }
        }

        [Test]
        public void ChantelWordPlaybackLoadsBothHandStreamsForEveryUploadedWord()
        {
            var library = new SaslWordPlaybackLibrary();
            Assert.That(library.Count, Is.EqualTo(5));
            var left = new HandPoseFrame();
            var right = new HandPoseFrame();
            for (var index = 0; index < library.Count; index++)
            {
                Assert.IsTrue(library.GetPoses(index, .5f, left, right));
                Assert.IsTrue(left.tracked || right.tracked, library.Label(index));
                Assert.That(library.ClipCount(index), Is.GreaterThan(0));
            }
        }

        [Test]
        public void ChantelWordPlaybackKeepsEveryCapturedFrameVisibleWithoutHandStreamJumps()
        {
            var library = new SaslWordPlaybackLibrary();
            var left = new HandPoseFrame();
            var right = new HandPoseFrame();
            for (var index = 0; index < library.Count; index++)
            {
                for (var frame = 0; frame < library.FrameCount; frame++)
                {
                    var phase = frame / (float)(library.FrameCount - 1);
                    Assert.IsTrue(library.GetPoses(index, phase, left, right),
                        $"The {library.Label(index)} replay dropped its hand at captured frame {frame}.");
                    Assert.That(left.tracked ^ right.tracked, Is.True,
                        $"The {library.Label(index)} replay should keep its single captured hand on one stable board side at frame {frame}.");
                }
            }
        }

        [Test]
        public void QuestAlphabetTemplatesContainTheCompleteAtoZLabelSet()
        {
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("sasl_templates_quest");
            Assert.IsNotNull(asset);
            var labels = System.Text.RegularExpressions.Regex.Matches(asset.text, "\\\"label\\\":\\\"([A-Z])\\\"")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => match.Groups[1].Value)
                .ToArray();
            Assert.That(labels.Distinct().OrderBy(label => label), Is.EqualTo(
                Enumerable.Range('A', 26).Select(letter => ((char)letter).ToString()).OrderBy(label => label)));
        }
    }
}
