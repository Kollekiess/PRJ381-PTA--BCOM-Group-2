using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Applies the OpenXR headset pose to the existing demo camera on Android.
/// This is a minimal seated/standing viewer rig for the prototype; it does not
/// provide locomotion or controller interaction.
/// </summary>
public class QuestHeadPoseDriver : MonoBehaviour
{
    [SerializeField] private Transform headsetCamera;
    [SerializeField] private Behaviour desktopController;

    private InputDevice headDevice;

    private void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Application.isMobilePlatform)
            return;

        if (desktopController != null)
            desktopController.enabled = false;

        var bodyCollider = GetComponent<Collider>();
        if (bodyCollider != null)
            bodyCollider.enabled = false;

        var bodyRigidbody = GetComponent<Rigidbody>();
        if (bodyRigidbody != null)
            bodyRigidbody.isKinematic = true;

        foreach (var bodyRenderer in GetComponentsInChildren<Renderer>())
            bodyRenderer.enabled = false;

        transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
        transform.localScale = Vector3.one;

        if (headsetCamera == null && Camera.main != null)
            headsetCamera = Camera.main.transform;
#endif
    }

    private void LateUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Application.isMobilePlatform || headsetCamera == null)
            return;

        if (!headDevice.isValid)
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);

        if (!headDevice.isValid)
            return;

        if (headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            headsetCamera.localPosition = position;
        if (headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            headsetCamera.localRotation = rotation;
#endif
    }
}
