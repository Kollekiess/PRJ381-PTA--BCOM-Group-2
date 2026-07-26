import mediapipe as mp

print("MediaPipe Version:", mp.__version__)
print("Tasks:", dir(mp.tasks))
print("Vision:", dir(mp.tasks.vision))
