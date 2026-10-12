

import numpy as np


def normalize_landmarks(flat_landmarks):
    pts = np.array(flat_landmarks, dtype=float).reshape(21, 3)
    pts = pts - pts[0]
    scale = np.linalg.norm(pts[9, :2])
    if scale < 1e-6:
        scale = 1.0
    return (pts / scale).flatten()
