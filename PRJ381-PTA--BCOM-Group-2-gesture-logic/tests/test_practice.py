import sys
import types
import unittest
from unittest.mock import MagicMock, patch

import practice
from test_backend_client import Result


class PracticeTests(unittest.TestCase):
    def run_camera(self, fail=False):
        cv = types.ModuleType('cv2')
        for name in ['flip', 'cvtColor', 'circle', 'putText', 'rectangle', 'imshow', 'destroyAllWindows']:
            setattr(cv, name, MagicMock())
        cv.COLOR_BGR2RGB, cv.FONT_HERSHEY_SIMPLEX = 1, 1
        cv.waitKey = MagicMock(return_value=255)
        frame = MagicMock()
        frame.shape = (480, 640, 3)
        cv.flip.return_value = frame
        cap = MagicMock()
        cap.read.side_effect = RuntimeError('camera failed') if fail else [(True, frame), (True, frame), (False, None)]
        cv.VideoCapture = MagicMock(return_value=cap)
        wrong = Result(predicted='B', target_score=0.1, correct=False, message='Wrong', hint='Hint')
        correct = Result()
        feedback = types.ModuleType('ml.feedback')
        engine = MagicMock(labels=['A', 'B'], min_confidence=0.7)
        engine.evaluate.side_effect = [wrong, correct]
        feedback.FeedbackEngine = MagicMock(return_value=engine)
        tracking = types.ModuleType('tracking.hand_tracker')
        tracker = MagicMock()
        pose = MagicMock(landmarks=[])
        tracker.detect.return_value = [pose]
        tracking.HandTracker = MagicMock(return_value=tracker)
        accumulator = types.ModuleType('ml.accumulator')
        acc = MagicMock()
        acc.update.side_effect = [('B', 1.0), ('A', 1.0)]
        accumulator.LetterAccumulator = MagicMock(return_value=acc)
        client, sender = MagicMock(), MagicMock()
        client.lesson.return_value = {'id': '11111111-1111-4111-8111-111111111111', 'title': 'A', 'targetGestures': ['A']}
        client.progress.return_value = {'highestScore': 0}
        modules = {'cv2': cv, 'ml.feedback': feedback, 'tracking.hand_tracker': tracking, 'ml.accumulator': accumulator}
        with patch.dict(sys.modules, modules), patch.object(sys, 'argv', ['practice.py', '--backend-url', 'http://localhost:3000', '--lesson-id', client.lesson.return_value['id']]), patch.object(practice, 'BackendClient', return_value=client), patch.object(practice, 'FeedbackSender', return_value=sender), patch.object(practice, 'save_session'), patch.dict('os.environ', {'BCSIGNVR_ACCESS_TOKEN': 'test'}):
            if fail:
                with self.assertRaises(RuntimeError):
                    practice.main()
            else:
                practice.main()
                self.assertEqual([call.args[0] for call in sender.submit.call_args_list], [wrong, correct])
        sender.close.assert_called_once()
        cap.release.assert_called_once()
        tracker.detector.close.assert_called_once()
        cv.destroyAllWindows.assert_called_once()

    def test_camera_loop_submits_wrong_and_correct_accepted_results(self):
        self.run_camera()

    def test_camera_failure_still_drains_feedback_and_releases_resources(self):
        self.run_camera(fail=True)
