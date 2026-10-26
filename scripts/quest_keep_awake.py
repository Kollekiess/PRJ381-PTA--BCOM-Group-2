#!/usr/bin/env python3
"""Keep signzzZ awake during a USB developer session, not other Quest apps.

Horizon OS owns off-face sleep; Unity's NeverSleep cannot override that sensor.
These are Meta's documented, reversible developer broadcasts. No persistent
power settings are changed and no app is relaunched or brought to the front.
"""

import argparse
import os
import re
import shutil
import signal
import subprocess
import threading


PACKAGE = "za.ac.belgiumcampus.bcsignvr"
KEEP_AWAKE = "com.oculus.vrpowermanager.prox_close"
RESTORE_SENSOR = "com.oculus.vrpowermanager.automation_disable"


def app_is_resumed(activity_dump):
    # Do not match history, process lists, or a paused app beneath another app.
    return any(
        re.search(r"(?:topResumedActivity|mResumedActivity|ResumedActivity|Resumed)\s*[:=]", line)
        and re.search(r"\s" + re.escape(PACKAGE) + r"/", line)
        for line in activity_dump.splitlines()
    )


class AwakeSession:
    def __init__(self, adb, serial):
        self.adb = adb
        self.serial = serial
        self.owns_override = False
        self.boot_id = None

    def shell(self, *args):
        return subprocess.run(
            [self.adb, "-s", self.serial, "shell", *args],
            check=True, capture_output=True, text=True, timeout=10,
        ).stdout

    def broadcast(self, action):
        result = self.shell("am", "broadcast", "-a", action)
        if "Broadcast completed: result=0" not in result:
            raise RuntimeError("Quest did not confirm the sleep-setting broadcast: " + result.strip())

    def update(self, activity_dump):
        active = app_is_resumed(activity_dump)
        if active and not self.owns_override:
            # Mark before sending so cleanup also runs if ADB times out after delivery.
            self.owns_override = True
            self.broadcast(KEEP_AWAKE)
            print("signzzZ is active: off-face sleep disabled for this test session.", flush=True)
        elif not active:
            self.restore()

    def observe(self, boot_id, activity_dump):
        if not boot_id.strip():
            raise RuntimeError("Quest boot identity is not available yet.")
        if boot_id != self.boot_id:
            # The override belongs to an OS boot, not a USB connection. A reboot
            # clears it; an unplug does not. Reapply only when our app is resumed.
            self.boot_id = boot_id
            self.owns_override = False
        self.update(activity_dump)

    def restore(self, force=False):
        if self.owns_override or force:
            self.broadcast(RESTORE_SENSOR)
            self.owns_override = False
            print("Normal Quest proximity-sensor sleep restored.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", default=os.environ.get("ANDROID_SERIAL"))
    parser.add_argument("--adb", default=os.environ.get("ADB_BIN") or shutil.which("adb"))
    parser.add_argument("--restore", action="store_true", help="Restore normal off-face sleep, then exit.")
    args = parser.parse_args()
    if not args.adb:
        parser.error("ADB is required. Install Android platform-tools first.")
    if not args.serial:
        devices = subprocess.run([args.adb, "devices"], check=True, capture_output=True, text=True, timeout=10)
        serials = [row.split()[0] for row in devices.stdout.splitlines() if row.split()[1:] == ["device"]]
        if len(serials) != 1:
            parser.error("Connect one authorized Quest or specify --serial.")
        args.serial = serials[0]
    session = AwakeSession(args.adb, args.serial)
    if args.restore:
        session.restore(force=True)
        return
    stopped = threading.Event()
    for signum in (signal.SIGINT, signal.SIGTERM):
        signal.signal(signum, lambda *_: stopped.set())
    print("Watching signzzZ on Quest, including reconnects/reboots. Ctrl+C restores normal sleep.", flush=True)
    disconnected = False
    try:
        while not stopped.is_set():
            try:
                boot_id = session.shell("cat", "/proc/sys/kernel/random/boot_id").strip()
                activity_dump = session.shell("dumpsys", "activity", "activities")
                session.observe(boot_id, activity_dump)
                if disconnected:
                    print("Quest reconnected; automatic signzzZ stay-awake is ready.", flush=True)
                disconnected = False
            except (subprocess.SubprocessError, RuntimeError, OSError):
                if not disconnected:
                    print("Waiting for the authorized Quest to reconnect. Sleep settings will be reapplied when signzzZ resumes.", flush=True)
                disconnected = True
            stopped.wait(2)
    finally:
        try:
            session.restore()
        except (subprocess.SubprocessError, RuntimeError) as error:
            print("Could not restore the sensor. Reconnect and run with --restore, or reboot the Quest.", flush=True)
            print(str(error), flush=True)


if __name__ == "__main__":
    main()
