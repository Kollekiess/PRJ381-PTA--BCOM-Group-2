import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch

from hand_bridge import packet_for, read_camera_frame, SequenceRecognizer


def example_hand():
    return SimpleNamespace(landmark=[SimpleNamespace(x=i / 100, y=i / 200, z=-i / 300)
                                     for i in range(21)])


def prototype(label, features, threshold=.1):
    return {"label": label, "acceptanceDistance": threshold,
            "centroidSequence": [features] * 32}


class PacketTests(unittest.TestCase):
    def test_packet_has_media_pipe_joint_order(self):
        packet = packet_for(example_hand(), "Right", 1280, 720)
        self.assertEqual(21, len(packet["landmarks"]))
        self.assertEqual("Right", packet["handedness"])
        self.assertEqual(0.2, packet["landmarks"][20]["x"])

    def test_packet_confidence_is_finite_and_bounded(self):
        self.assertEqual(1.0, packet_for(example_hand(), "Right", 1280, 720, confidence=8)["confidence"])
        self.assertEqual(0.0, packet_for(example_hand(), "Right", 1280, 720, confidence=float("nan"))["confidence"])

    def test_packet_rejects_invalid_landmarks(self):
        hand = example_hand()
        hand.landmark[0].x = float("nan")
        with self.assertRaises(ValueError):
            packet_for(hand, "Right", 1280, 720)


class RecognizerTests(unittest.TestCase):
    """These verify rejection/state mechanics, not SASL recognition accuracy."""

    def setUp(self):
        self.recognizer = SequenceRecognizer(None)
        self.hand = example_hand()
        self.expected = self.recognizer.features(self.hand)
        self.recognizer.classes = [prototype("REFERENCE", self.expected)]

    def warm_up(self):
        for _ in range(32):
            result = self.recognizer.update(self.hand)
        return result

    def test_exact_synthetic_reference_recognizes_class(self):
        result = self.warm_up()
        self.assertEqual("REFERENCE", result[0])
        self.assertAlmostEqual(1.0, result[1])

    def test_distant_pose_returns_unknown(self):
        self.recognizer.classes = [prototype("UNRELATED", [value + 1 for value in self.expected])]
        self.assertEqual(("UNKNOWN", 0.0), self.warm_up())

    def test_low_confidence_pose_returns_unknown_within_distance_limit(self):
        self.recognizer.classes = [prototype("WEAK", [value + .07 for value in self.expected])]
        self.assertEqual(("UNKNOWN", 0.0), self.warm_up())

    def test_equally_matching_labels_are_ambiguous(self):
        self.recognizer.classes.append(prototype("OTHER_REFERENCE", self.expected))
        self.assertEqual(("UNKNOWN", 0.0), self.warm_up())

    def test_tracking_loss_requires_a_new_movement(self):
        self.assertEqual("REFERENCE", self.warm_up()[0])
        self.recognizer.reset()
        result = self.recognizer.update(self.hand)
        self.assertEqual("COLLECTING_MOVEMENT", result[0])
        self.assertEqual(1, len(self.recognizer.frames))
        self.assertLess(result[1], 1)

    def test_frame_gap_clears_previous_movement(self):
        self.warm_up()
        self.recognizer.last_frame_at = 1.0
        with patch("hand_bridge.time.monotonic", return_value=2.0):
            result = self.recognizer.update(self.hand)
        self.assertEqual("COLLECTING_MOVEMENT", result[0])
        self.assertEqual(1, len(self.recognizer.frames))

    def test_missing_model_never_reports_unbounded_warmup_percentage(self):
        self.recognizer.classes = []
        for _ in range(150):
            self.assertEqual(("MODEL_UNAVAILABLE", 0.0), self.recognizer.update(self.hand))

    def test_nonfinite_landmarks_clear_recognition_history(self):
        self.warm_up()
        self.hand.landmark[0].x = float("inf")
        self.assertEqual(("UNKNOWN", 0.0), self.recognizer.update(self.hand))
        self.assertEqual(0, len(self.recognizer.frames))

    def test_malformed_model_sequence_is_not_a_match(self):
        self.recognizer.classes = [prototype("BAD", self.expected[:-1])]
        self.assertEqual(("UNKNOWN", 0.0), self.warm_up())


class CameraReadTests(unittest.TestCase):
    @patch("hand_bridge.time.sleep")
    def test_transient_failure_reports_loss_and_recovers(self, sleep):
        frame = object()
        camera = Mock()
        camera.read.side_effect = [(False, None), (False, None), (True, frame)]
        lost = Mock()
        self.assertIs(frame, read_camera_frame(camera, lost))
        self.assertEqual(2, lost.call_count)
        self.assertEqual(2, sleep.call_count)

    @patch("hand_bridge.time.sleep")
    def test_failed_live_camera_stops_after_bounded_retries(self, sleep):
        camera = Mock()
        camera.read.return_value = (False, None)
        lost = Mock()
        with self.assertRaisesRegex(RuntimeError, "Camera stopped"):
            read_camera_frame(camera, lost, max_failures=3)
        self.assertEqual(3, camera.read.call_count)
        self.assertEqual(3, lost.call_count)
        self.assertEqual(2, sleep.call_count)

    @patch("hand_bridge.time.sleep")
    def test_video_eof_reports_loss_without_retrying(self, sleep):
        camera = Mock()
        camera.read.return_value = (False, None)
        lost = Mock()
        self.assertIsNone(read_camera_frame(camera, lost, video=True))
        lost.assert_called_once()
        camera.read.assert_called_once()
        sleep.assert_not_called()

    def test_camera_open_fallback_skips_preferred_failure(self):
        from hand_bridge import open_camera
        failed = Mock(isOpened=Mock(return_value=False))
        working = Mock(isOpened=Mock(return_value=True))
        with patch("hand_bridge.cv2.VideoCapture", side_effect=[failed, working]):
            self.assertIs(working, open_camera(2))
        failed.release.assert_called_once()


if __name__ == "__main__":
    unittest.main()

