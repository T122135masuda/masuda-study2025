using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class RightGazeTracker : MonoBehaviour
{
    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] out_gazes);
        XrSingleEyeGazeDataHTC rightGaze = out_gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];
        if (rightGaze.isValid)
        {
            transform.position = rightGaze.gazePose.position.ToUnityVector();
            transform.rotation = rightGaze.gazePose.orientation.ToUnityQuaternion();
        }
    }
}
