from tracking.landmark import Landmark

class HandPose:
    def __init__(self, landmarks, handedness):
        self.landmarks = landmarks
        self.handedness = handedness
    
    def get_landmark(self, index):
        return self.landmarks[index]

    @property
    def wrist(self):
        return self.landmarks[0]

    @property
    def thumb_tip(self):
        return self.landmarks[4]

    @property
    def index_tip(self):
        return self.landmarks[8]
    
    @property
    def middle_tip(self):
        return self.landmarks[12]
    
    @property
    def ring_tip(self):
            return self.landmarks[16]

    @property    
    def pinky_tip(self):
            return self.landmarks[20]

    def to_dict(self):
        return {
            "handedness": self.handedness,
            "landmarks": [
                landmark.to_list()
                for landmark in self.landmarks
            ]
        }
        
    def normalize_landmarks(self):
        wrist = self.wrist
        normalized = []
        
        for landmark in self.landmarks:
            
            normalized.append(
                Landmark(
                    landmark.x - wrist.x,
                    landmark.y - wrist.y,
                    landmark.z - wrist.z
                )
            )
        
        return normalized
    
    def flattened_landmarks(self):
        
        features = []
        
        for landmarks in self.landmarks:
            features.extend([
                landmarks.x,
                landmarks.y,
                landmarks.z
            ])
            
        return features
    