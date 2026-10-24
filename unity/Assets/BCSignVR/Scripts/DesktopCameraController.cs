using UnityEngine;

namespace BCSignVR
{
    public sealed class DesktopCameraController : MonoBehaviour
    {
        public float moveSpeed = 2.2f;
        public float lookSpeed = 65f;
        const float ReferenceAspect = 1.6f;
        const float ReferenceVerticalFov = 50f;
        Camera camera;

        void Awake()
        {
            camera = GetComponent<Camera>();
            KeepClassroomFramed();
        }

        void Update()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive)
                return;
            KeepClassroomFramed();
            var movement = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            transform.position += transform.TransformDirection(movement.normalized) * (moveSpeed * Time.deltaTime);
            if (Input.GetKey(KeyCode.Q)) transform.Rotate(Vector3.up, -lookSpeed * Time.deltaTime, Space.World);
            if (Input.GetKey(KeyCode.E)) transform.Rotate(Vector3.up, lookSpeed * Time.deltaTime, Space.World);
        }

        void KeepClassroomFramed()
        {
            if (camera == null || camera.aspect <= 0f) return;
            // The board and Bob are laid out for the 16:10 Mac preview. On a
            // narrower desktop window, matching the reference horizontal FOV
            // prevents the tutor being clipped while keeping the board's
            // spacing and button positions unchanged.
            var referenceHorizontalFov = 2f * Mathf.Atan(
                Mathf.Tan(ReferenceVerticalFov * Mathf.Deg2Rad * .5f) * ReferenceAspect);
            var verticalFov = 2f * Mathf.Atan(
                Mathf.Tan(referenceHorizontalFov * .5f) / camera.aspect) * Mathf.Rad2Deg;
            camera.fieldOfView = Mathf.Clamp(verticalFov, 42f, 68f);
        }
    }
}

