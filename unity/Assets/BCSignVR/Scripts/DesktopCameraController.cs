using UnityEngine;

namespace BCSignVR
{
    public sealed class DesktopCameraController : MonoBehaviour
    {
        public float moveSpeed = 2.2f;
        public float lookSpeed = 65f;

        void Update()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive)
                return;
            var movement = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            transform.position += transform.TransformDirection(movement.normalized) * (moveSpeed * Time.deltaTime);
            if (Input.GetKey(KeyCode.Q)) transform.Rotate(Vector3.up, -lookSpeed * Time.deltaTime, Space.World);
            if (Input.GetKey(KeyCode.E)) transform.Rotate(Vector3.up, lookSpeed * Time.deltaTime, Space.World);
        }
    }
}

