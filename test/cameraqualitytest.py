import cv2

cap = cv2.VideoCapture(0)

while True:
    success, frame = cap.read()

    if not success:
        break

    cv2.imshow("Raw Camera", frame)

    if cv2.waitKey(1) == 27:
        break

cap.release()
cv2.destroyAllWindows()