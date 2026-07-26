class Landmark:
    def __init__ (self, x, y, z):
        self.x = x
        self.y = y
        self.z = z
    
    def to_list(self):
        return [self.x, self.y, self.z]
    
    def __repr__(self):
        return (
            f"Landmark("
            f"x={self.x:.3f}, "
            f"y={self.y:.3f}, "
            f"z={self.z:.3f}"
        )