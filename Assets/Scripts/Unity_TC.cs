using UnityEngine;

public class Unity_TC : MonoBehaviour
{
    [Header("UI Layers")]
    public RectTransform itemMark; // 飞机图案标志
    public RectTransform itemBall; // 底部侧滑黑球

    [Header("Flight Data")]
    public float turnRate = 0.0f; // 转弯率
    public float slipSkid = 0.0f; // 侧滑量

    [Header("Data Source")]
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;

    void Update()
    {
        if (!useManualInput)
            UpdateData();

        // 1. 黑球的旋转映射
        if (itemBall != null)
            itemBall.localEulerAngles = new Vector3(0, 0, slipSkid);

        // 2. 飞机标记的倾斜映射 (原理：3度转弯率 = 20度UI旋转)
        float angle = (turnRate / 3.0f) * 20.0f;
        if (itemMark != null)
            itemMark.localEulerAngles = new Vector3(0, 0, -angle);
    }

    public void SetTrackId(string id)
    {
        trackId = id;
        usePrimaryTrack = string.IsNullOrEmpty(id);
    }

    private void UpdateData()
    {
        // 转弯率：优先 FlightDataStreamReceiver
        if (receiver == null)
            receiver = FlightDataStreamReceiver.Instance;

        if (receiver != null)
        {
            if (usePrimaryTrack || string.IsNullOrEmpty(trackId))
            {
                turnRate = receiver.PrimaryTurnRateDegPerSec;
            }
            else
            {
                var positions = receiver.SnapshotPositions;
                var sel = positions.Find(p => p != null && p.trackId == trackId);
                if (sel != null)
                    turnRate = receiver.PrimaryTurnRateDegPerSec;
            }
        }

        // 侧滑角：UDPDataReceiver 有专用数据，保持使用
        if (UDPDataReceiver.Instance != null)
        {
            string slipSkidKey = "[载机侧滑角][_角度_毫弧度]";
            if (UDPDataReceiver.Instance.planeData.ContainsKey(slipSkidKey))
            {
                float slipSkidValue;
                if (float.TryParse(UDPDataReceiver.Instance.planeData[slipSkidKey], out slipSkidValue))
                {
                    slipSkid = slipSkidValue * 180f / Mathf.PI / 1000f;
                }
            }
        }
    }
}