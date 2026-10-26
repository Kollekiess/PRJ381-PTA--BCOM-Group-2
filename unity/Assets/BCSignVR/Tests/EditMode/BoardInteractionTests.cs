using NUnit.Framework;
using UnityEngine;

namespace BCSignVR.Tests
{
    public sealed class BoardInteractionTests
    {
        GameObject parent;

        [TearDown]
        public void TearDown()
        {
            if (parent != null) Object.DestroyImmediate(parent);
        }

        [Test]
        public void MenuButtonDispatchesOneActionPerPress()
        {
            parent = new GameObject("Button test root");
            var calls = 0;
            var button = ClassroomUi.Button("Test", parent.transform, 0, 0, 1f, () => calls++);

            button.Press();
            button.Press();

            Assert.AreEqual(1, calls, "A single press must not activate twice through overlapping input paths.");
        }

        [Test]
        public void DisabledMenuButtonCannotRunItsAction()
        {
            parent = new GameObject("Disabled button test root");
            var calls = 0;
            var button = ClassroomUi.Button("Test", parent.transform, 0, 0, 1f, () => calls++);
            button.enabled = false;

            button.Press();

            Assert.Zero(calls);
        }

        [Test]
        public void DwellSelectionWaitsFiveSecondsAndFiresOnlyOnce()
        {
            parent = new GameObject("Dwell target");
            var button = parent.AddComponent<BoardButton>();
            var dwell = new BoardDwellSelection();

            Assert.AreEqual(0f, dwell.Advance(button, 4, 0f, out var selected));
            Assert.IsNull(selected);
            var progress = dwell.Advance(button, 4, 4.95f, out selected);
            Assert.That(progress, Is.InRange(.98f, .991f));
            Assert.IsNull(selected, "Dwell must not activate before five seconds.");

            progress = dwell.Advance(button, 4, .06f, out selected);
            Assert.AreEqual(1f, progress);
            Assert.AreSame(button, selected);
            dwell.Advance(button, 4, 1f, out selected);
            Assert.IsNull(selected, "A held pointer must not repeat the action.");
        }

        [Test]
        public void CompletedDwellDoesNotRestartOnAButtonThatStaysVisible()
        {
            parent = new GameObject("Persistent replay button");
            var calls = 0;
            var button = ClassroomUi.Button("Replay", parent.transform, 0, 0, 1f, () => calls++);
            var dwell = new BoardDwellSelection();
            dwell.Advance(button, 4, 0f, out _);
            dwell.Advance(button, 4, 5f, out var selected);
            dwell.Press(selected, 4);
            Assert.AreEqual(1, calls);
            dwell.Advance(button, 4, 10f, out selected);
            Assert.IsNull(selected, "Holding on Replay must not restart it every five seconds.");
            dwell.Advance(null, 4, .1f, out _);
            dwell.Advance(button, 4, 0f, out _);
            dwell.Advance(button, 4, 5f, out selected);
            Assert.AreSame(button, selected, "Leaving and returning must allow a new deliberate selection.");
        }

        [Test]
        public void TriggerPressConsumesThePendingDwell()
        {
            parent = new GameObject("Trigger target");
            var calls = 0;
            var button = ClassroomUi.Button("Slow / normal", parent.transform, 0, 0, 1f, () => calls++);
            var dwell = new BoardDwellSelection();
            dwell.Advance(button, 1, 0f, out _);
            dwell.Advance(button, 1, 4f, out _);
            dwell.Press(button, 1);
            dwell.Advance(button, 1, 6f, out var selected);
            Assert.IsNull(selected, "A trigger selection must not also fire the dwell action.");
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void DwellResetsWhenTargetOrPointerChanges()
        {
            parent = new GameObject("Dwell target root");
            var first = parent.AddComponent<BoardButton>();
            var second = new GameObject("Second dwell target").AddComponent<BoardButton>();
            var dwell = new BoardDwellSelection();
            dwell.Advance(first, 4, 0f, out _);
            dwell.Advance(first, 4, 4f, out _);

            Assert.Zero(dwell.Advance(second, 4, .1f, out var selected));
            Assert.IsNull(selected);
            Assert.Zero(dwell.Advance(second, 3, .1f, out selected));
            Assert.IsNull(selected);

            Object.DestroyImmediate(second.gameObject);
        }

        [Test]
        public void DwellDoesNotSelectAnInactiveTarget()
        {
            parent = new GameObject("Inactive dwell target");
            var button = parent.AddComponent<BoardButton>();
            var dwell = new BoardDwellSelection();
            parent.SetActive(false);

            Assert.Zero(dwell.Advance(button, 4, 5f, out var selected));
            Assert.IsNull(selected);
        }

        [Test]
        public void ConfidentAlphabetMatchArmsAutomaticAdvanceOnlyAtEightyPercent()
        {
            Assert.IsFalse(BCSignVRApp.IsConfidentAlphabetMatch("TEAM_SASL_C", "TEAM_SASL_C", .799f));
            Assert.IsTrue(BCSignVRApp.IsConfidentAlphabetMatch("TEAM_SASL_C", "TEAM_SASL_C", .80f));
            Assert.IsFalse(BCSignVRApp.IsConfidentAlphabetMatch("TEAM_SASL_C", "TEAM_SASL_D", .99f));
            Assert.IsFalse(BCSignVRApp.IsConfidentAlphabetMatch("TEAM_SASL_C", "UNKNOWN", 1f));
        }

        [Test]
        public void MenuGazeFallbackIsEnabledByDefault()
        {
            Assert.IsTrue(ClassroomUi.GazeSelectionEnabled,
                "The profile picker must remain usable before a controller or hand is available.");
        }
    }
}
