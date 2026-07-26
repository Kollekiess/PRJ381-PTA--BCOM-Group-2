import cv2
from tracking.hand_tracker import HandTracker

tracker = HandTracker()

cap = cv2.VideoCapture(0)

timestamp = 0

while True:
    
    success, frame = cap.read()
    
    if not success:
        break
    
    ##frame = cv2.flip(frame, 1)
    
    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    
    poses = tracker.detect(rgb, timestamp)
    
    timestamp += 33
    
    height, width, _ = frame.shape
    
    for pose in poses:
        
        print(
            "index Tip:",
            pose.index_tip()
        )
        
        x, y, z = pose.index_tip()
        
        screen_x = int(x * width)
        screen_y = int(y * height)
        
        cv2.circle(
            frame,
            (screen_x, screen_y),
            10,
            (0, 255, 0),
            -1            
        )
        
        cv2.putText(
            frame,
            pose.handedness,
            (screen_x, screen_y - 20),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.8,
            (0, 255, 0),
            2
        )
        
        
        print(pose.handedness)
        
    cv2.imshow("BCSignVR", frame)
    
    if cv2.waitKey(1) == 27:
        break
    
cap.release()
cv2.destroyAllWindows()