using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class UpdateRightPupil : MonoBehaviour
{
    [Header("Right eye pupil (read-only)")]
    public bool diameterValid;
    public bool positionValid;
    public float rightPupilDiameter;   // mm
    public Vector2 rightPupilPosition; // XrVector2f -> Unity Vector2

    void Update()
    {
        XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] pupils);
        var right = pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];

        diameterValid = right.isDiameterValid;
        positionValid = right.isPositionValid;

        if (diameterValid)
        {
            rightPupilDiameter = right.pupilDiameter;
            // Do something...
        }

        if (positionValid)
        {
            rightPupilPosition = new Vector2(right.pupilPosition.x, right.pupilPosition.y);
            // Do something...
        }
    }
}
