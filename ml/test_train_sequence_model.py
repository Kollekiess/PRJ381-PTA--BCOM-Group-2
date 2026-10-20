import json
import tempfile
import unittest
from pathlib import Path

from train_sequence_model import train


class TrainSequenceModelTests(unittest.TestCase):
    def test_output_is_consumable_by_sequence_recognizer(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "recording.jsonl"
            output = root / "sequence-model.json"
            lines = []
            for frame in range(36):
                lines.append(json.dumps({
                    "tracked": True,
                    "landmarks": [{"x": .02 * joint + .001 * frame,
                                   "y": .03 * joint,
                                   "z": -.01 * joint} for joint in range(21)],
                }))
            source.write_text("\n".join(lines) + "\n")
            train(source, output, label="REVIEWED_SASL_HELLO", signer="anonymous-001",
                  reviewed_by="reviewer-001", source="consented capture")
            model = json.loads(output.read_text())
            item = model["classes"][0]
            self.assertEqual("BCSignVRSequenceModelV1", model["format"])
            self.assertEqual("REVIEWED_SASL_HELLO", item["label"])
            self.assertEqual(32, len(item["prototypeSequences"][0]))
            self.assertEqual(65, len(item["prototypeSequences"][0][0]))

    def test_rejects_unreviewed_label(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "recording.jsonl"
            source.write_text(json.dumps({"tracked": False}) + "\n")
            with self.assertRaises(ValueError):
                train(source, root / "model.json", label="HELLO", signer="a",
                      reviewed_by="b", source="c")


if __name__ == "__main__":
    unittest.main()
