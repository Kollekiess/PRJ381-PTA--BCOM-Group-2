


class LetterAccumulator:
    def __init__(self, hold_seconds=1.0, min_conf=0.7):
        self.hold_seconds = hold_seconds
        self.min_conf = min_conf
        self.candidate = None
        self.since = None
        self.locked = None

    def update(self, letter, conf, now):
        """
        letter: predicted letter, or None if no hand is visible
        conf:   model confidence 0..1
        now:    time in seconds
        Returns (accepted_letter_or_None, progress 0..1)
        """
        if letter is None:                      # hand gone: reset everything
            self.candidate = self.since = self.locked = None
            return None, 0.0

        if conf < self.min_conf:                # unsure: restart the hold timer
            self.candidate = self.since = None
            return None, 0.0

        if letter == self.locked:               # already typed, waiting for a change
            return None, 1.0
        self.locked = None

        if letter != self.candidate:            # new candidate: start timing
            self.candidate, self.since = letter, now
            return None, 0.0

        progress = (now - self.since) / self.hold_seconds
        if progress >= 1.0:
            self.locked = letter
            self.candidate = self.since = None
            return letter, 1.0
        return None, progress