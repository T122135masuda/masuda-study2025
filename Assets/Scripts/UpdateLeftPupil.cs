using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class UpdateLeftPupil : MonoBehaviour
{
    [Header("Left eye pupil (read-only)")]
    public bool diameterValid;
    public bool positionValid;
    public float leftPupilDiameter;   // mm
    public Vector2 leftPupilPosition; // XrVector2f -> Unity Vector2

    void Update()
    {
        // 両眼分の瞳孔データを取得
        XR_HTC_eye_tracker.Interop.GetEyePupilData(out XrSingleEyePupilDataHTC[] pupils);

        // 左目データを選択
        var left = pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];

        // 有効フラグ
        diameterValid = left.isDiameterValid;
        positionValid = left.isPositionValid;

        // 有効なときだけ値を更新
        if (diameterValid)
        {
            leftPupilDiameter = left.pupilDiameter;  // 単位：mm
        }

        if (positionValid)
        {
            leftPupilPosition = new Vector2(left.pupilPosition.x, left.pupilPosition.y);
        }
    }
}
