class HandPose:
    def __init__(self, landmarks, handedness):
        self.landmarks = landmarks
        self.handedness = handedness
    
    def get_landmark(self, index):
        return self.landmarks[index]

    def thumb_tip(self):
        return self.landmarks[4]

    def index_tip(self):
        return self.landmarks[8]
    
    def middle_tip(self):
        return self.landmarks[12]
    
    def ring_tip(self):
            return self.landmarks[16]
        
    def pinky_tip(self):
            return self.landmarks[20]
    