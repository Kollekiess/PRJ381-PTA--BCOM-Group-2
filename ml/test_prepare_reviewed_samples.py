import json
import tempfile
import unittest
from pathlib import Path

from prepare_reviewed_samples import prepare


def packet(tracked=True):
    return {"tracked": tracked, "landmarks": [
        {"x": i / 20, "y": i / 30, "z": -i / 40} for i in range(21)
    ]}


class PrepareReviewedSamplesTests(unittest.TestCase):
    def test_converts_only_tracked_packets_with_required_provenance(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            raw = root / "capture.jsonl"
            out = root / "reviewed.jsonl"
            raw.write_text(json.dumps(packet(False)) + "\n" + json.dumps(packet()) + "\n")
            self.assertEqual(prepare(raw, out, label="REVIEWED_SASL_A", signer="anon-1",
                                     reviewed_by="reviewer", source="consented capture"), 1)
            row = json.loads(out.read_text().splitlines()[0])
            self.assertEqual(row["label"], "REVIEWED_SASL_A")
            self.assertEqual(len(row["joints"]), 21)

    def test_rejects_unreviewed_label(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            raw = root / "capture.jsonl"
            raw.write_text(json.dumps(packet()) + "\n")
            with self.assertRaises(ValueError):
                prepare(raw, root / "out.jsonl", label="A", signer="anon-1",
                        reviewed_by="reviewer", source="consented capture")


if __name__ == "__main__":
    unittest.main()
