import json
import tempfile
import unittest
from pathlib import Path

from train_pose_model import train


class TrainPoseModelTests(unittest.TestCase):
    def test_output_is_consumable_by_sequence_recognizer(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            input_path = root / "reviewed.jsonl"
            output_path = root / "model.json"
            joints = [[0.01 * index, 0.02 * index, -0.01 * index]
                      for index in range(21)]
            # Two nearby reviewed captures give the trainer a non-zero,
            # deterministic acceptance radius.
            rows = []
            for offset in (0.0, 0.001):
                rows.append({"label": "REVIEWED_SASL_HELLO",
                             "signer": "anonymous-001",
                             "reviewedBy": "reviewer-001",
                             "source": "consented capture",
                             "joints": [[x + offset, y, z] for x, y, z in joints]})
            input_path.write_text("\n".join(json.dumps(row) for row in rows) + "\n")
            train(input_path, output_path)
            model = json.loads(output_path.read_text())
            item = model["classes"][0]
            self.assertEqual("REVIEWED_SASL_HELLO", item["label"])
            self.assertEqual(32, len(item["centroidSequence"]))
            self.assertEqual(63, len(item["centroidSequence"][0]))
            self.assertNotIn("centroid", item)


if __name__ == "__main__":
    unittest.main()
