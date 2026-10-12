class HandSequence:
    
    def __init__(self):
        
        self.frames = []
        
    def add_frame(self, pose):
        
        self.frames.append(pose)
        
    def clear(self):
        
        self.frames.clear()
        
    def length(self):
        
        return len(self.frames)