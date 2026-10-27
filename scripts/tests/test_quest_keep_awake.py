import importlib.util
from pathlib import Path
import unittest
from unittest.mock import Mock


spec = importlib.util.spec_from_file_location("quest_keep_awake", Path(__file__).parents[1] / "quest_keep_awake.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class QuestKeepAwakeTests(unittest.TestCase):
    def setUp(self):
        self.session = module.AwakeSession("adb", "test-quest")
        self.session.broadcast = Mock()
        self.active = "topResumedActivity=ActivityRecord{123 u0 " + module.PACKAGE + "/UnityActivity t9}"

    def test_resumed_activity_formats(self):
        for prefix in ("topResumedActivity=", "mResumedActivity:", "ResumedActivity:", "Resumed:"):
            self.assertTrue(module.app_is_resumed(prefix + self.active.split("=", 1)[1]))

    def test_background_history_and_similarly_named_apps_do_not_enable_override(self):
        for dump in ("", "Hist #0 " + module.PACKAGE + "/UnityActivity", "packageName=" + module.PACKAGE,
                     self.active.replace(module.PACKAGE, module.PACKAGE + ".other")):
            self.assertFalse(module.app_is_resumed(dump))

    def test_override_only_changes_on_app_transitions(self):
        self.session.update(self.active)
        self.session.update(self.active)
        self.session.broadcast.assert_called_once_with(module.KEEP_AWAKE)
        self.session.update("Resumed: ActivityRecord{u0 another.app/Main}")
        self.session.update("")
        self.assertEqual(self.session.broadcast.call_count, 2)
        self.session.broadcast.assert_called_with(module.RESTORE_SENSOR)
        self.session.update(self.active)
        self.assertEqual(self.session.broadcast.call_count, 3)

    def test_cleanup_restores_owned_override_once(self):
        self.session.update(self.active)
        self.session.restore()
        self.session.restore()
        self.assertEqual(self.session.broadcast.call_count, 2)
        self.assertFalse(self.session.owns_override)

    def test_cleanup_does_not_change_headset_when_app_never_started(self):
        self.session.restore()
        self.session.broadcast.assert_not_called()

    def test_restore_option_recovers_previous_disconnected_session(self):
        self.session.restore(force=True)
        self.session.broadcast.assert_called_once_with(module.RESTORE_SENSOR)

    def test_failed_broadcast_still_attempts_cleanup(self):
        self.session.broadcast.side_effect = [RuntimeError("timeout"), None]
        with self.assertRaises(RuntimeError):
            self.session.update(self.active)
        self.session.restore()
        self.session.broadcast.assert_called_with(module.RESTORE_SENSOR)

    def test_reboot_reapplies_override_even_when_same_app_is_resumed(self):
        self.session.observe("first-boot", self.active)
        self.session.observe("first-boot", self.active)
        self.assertEqual(self.session.broadcast.call_count, 1)
        self.session.observe("second-boot", self.active)
        self.assertEqual(self.session.broadcast.call_count, 2)
        self.session.broadcast.assert_called_with(module.KEEP_AWAKE)

    def test_reboot_does_not_disable_sensor_before_signzzz_opens(self):
        self.session.observe("first-boot", self.active)
        self.session.observe("second-boot", "Resumed: ActivityRecord{u0 other.app/Main}")
        self.assertFalse(self.session.owns_override)
        self.assertEqual(self.session.broadcast.call_count, 1)
        self.session.observe("second-boot", self.active)
        self.session.broadcast.assert_called_with(module.KEEP_AWAKE)

    def test_same_boot_reconnect_restores_sensor_if_app_was_closed(self):
        self.session.observe("same-boot", self.active)
        self.session.observe("same-boot", "")
        self.session.broadcast.assert_called_with(module.RESTORE_SENSOR)

    def test_missing_boot_id_cannot_change_power_settings(self):
        with self.assertRaises(RuntimeError):
            self.session.observe("", self.active)
        self.session.broadcast.assert_not_called()


if __name__ == "__main__":
    unittest.main()
