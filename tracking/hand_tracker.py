import mediapipe as mp

from tracking.hand_pose import HandPose

class HandTracker:
    
    def __init__(self):
        
        BaseOptions = mp.tasks.BaseOptions
        HandLandmarker = mp.tasks.vision.HandLandmarker
        HandLandmarkerOptions = mp.tasks.vision.HandLandmarkerOptions
        RunningMode = mp.tasks.vision.RunningMode
        
        options = HandLandmarkerOptions(
            base_options=BaseOptions(
                model_asset_path="./models/hand_landmarker.task"
            ),
            num_hands=2,
            running_mode=RunningMode.VIDEO
        )
        
        self.detector = (
            HandLandmarker.create_from_options(options)
        )
        
    def detect(self, frame, timestamp):
        
        mp_image = mp.Image(
            image_format=mp.ImageFormat.SRGB,
            data=frame
        )
        
        result = self.detector.detect_for_video(
            mp_image,
            timestamp
        )
        
        poses = []
        
        for hand_index, hand in enumerate(result.hand_landmarks):
            
            landmarks = []
            
            for landmark in hand:
                landmarks.append(
                    (
                        landmark.x,
                        landmark.y,
                        landmark.z
                    )
            )
                
            handedness = "Unknown"
            
            if result.handedness:
                handedness = (
                    result.handedness[hand_index][0].category_name
                )

            poses.append(HandPose(landmarks, handedness))
        
        return poses