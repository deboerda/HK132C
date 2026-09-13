using UnityEngine;

/// <summary>
/// 将飞行姿态数据（heading/pitch/roll）应用到3D飞机模型，
/// 只旋转不移动——飞机始终在原位置，仅显示姿态变化。
/// </summary>
public class PlaneAttitudeController : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private bool usePrimaryTrack = true;
    [SerializeField] private string trackId = "";
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private float manualHeading = 0f;
    [SerializeField] private float manualPitch = 0f;
    [SerializeField] private float manualRoll = 0f;

    [Header("Rotation Mapping")]
    [Tooltip("模型初始朝向偏移（度），用于对齐模型前方与正北")]
    [SerializeField] private float headingOffset = 0f;
    [Tooltip("俯仰轴翻转（有些模型 pitch 正负相反）")]
    [SerializeField] private bool invertPitch = false;
    [Tooltip("横滚轴翻转")]
    [SerializeField] private bool invertRoll = false;

    [Header("Smoothing")]
    [SerializeField] private float rotationLerpSpeed = 5f;

    private Quaternion targetRotation;

    private void Start()
    {
        targetRotation = transform.rotation;
    }

    private void Update()
    {
        float heading, pitch, roll;

        if (useManualInput)
        {
            heading = manualHeading;
            pitch = manualPitch;
            roll = manualRoll;
        }
        else
        {
            if (receiver == null)
                receiver = FlightDataStreamReceiver.Instance;

            if (receiver == null)
                return;

            FlightDataStreamReceiver.TrackPosition pos = null;
            if (usePrimaryTrack || string.IsNullOrEmpty(trackId))
            {
                pos = receiver.PrimaryPosition;
            }
            else
            {
                var positions = receiver.SnapshotPositions;
                var sel = positions.Find(p => p != null && p.trackId == trackId);
                pos = sel;
            }

            if (pos == null)
                return;

            heading = pos.heading;
            pitch = pos.pitch;
            roll = pos.roll;
        }

        if (invertPitch) pitch = -pitch;
        if (invertRoll) roll = -roll;

        // heading = yaw (绕Y轴), pitch = 绕X轴, roll = 绕Z轴
        targetRotation = Quaternion.Euler(pitch, heading + headingOffset, roll);

        if (rotationLerpSpeed > 0f)
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
        else
            transform.rotation = targetRotation;
    }

    public void SetTrackId(string id)
    {
        trackId = id;
        usePrimaryTrack = string.IsNullOrEmpty(id);
    }
}
