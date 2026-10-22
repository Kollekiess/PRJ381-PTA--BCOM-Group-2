using UnityEngine;

/// <summary>
/// Allows Cardboard gaze and VR pointer interaction on chalkboard lesson sign columns.
/// Targets and practices the specific sign when gazed at or clicked.
/// </summary>
public class ChalkboardColumnInteractable : MonoBehaviour, ICardboardInteractable
{
    public int signIndex = 0; // 0, 1, or 2

    public void OnGazeEnter()
    {
        if (SASLGestureManager.Instance != null)
        {
            SASLGestureManager.Instance.SetTargetedSign(signIndex);
        }
    }

    public void OnGazeExit()
    {
    }

    public void OnGazeTrigger()
    {
        if (SASLGestureManager.Instance != null)
        {
            SASLGestureManager.Instance.SetTargetedSign(signIndex);
            SASLGestureManager.Instance.OnGestureRecognized(signIndex);
        }
    }
}
