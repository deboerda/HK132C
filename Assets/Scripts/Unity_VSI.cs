using UnityEngine;

public class Unity_VSI : MonoBehaviour
{
    [Header("UI Layers")]
    public RectTransform itemHand; // 垂直速度指针

    [Header("Flight Data")]
    public float climbRate = 0f; // 爬升角 [deg]

    [Header("Data Source")]
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;

    private const float minClimbRate = -30f; // 最大下降率
    private const float maxClimbRate = 30f;  // 最大爬升率

    void Update()
    {
        if (!useManualInput)
            UpdateData();

        float normalizedClimbRate = Mathf.Clamp((climbRate - minClimbRate) / (maxClimbRate - minClimbRate), 0f, 1f);
        float angle = (normalizedClimbRate - 0.5f) * 360f;

        if (itemHand != null)
            itemHand.localEulerAngles = new Vector3(0, 0, -angle);
    }

    public void SetTrackId(string id)
    {
        trackId = id;
        usePrimaryTrack = string.IsNullOrEmpty(id);
    }

    private void UpdateData()
    {
        // 优先使用 UDPDataReceiver 的爬升角数据
        if (UDPDataReceiver.Instance != null)
        {
            string climbRateKey = "[载机爬升角][_角度_毫弧度]";
            if (UDPDataReceiver.Instance.planeData.ContainsKey(climbRateKey))
            {
                float climbRateValue;
                if (float.TryParse(UDPDataReceiver.Instance.planeData[climbRateKey], out climbRateValue))
                {
                    climbRate = climbRateValue * 180f / Mathf.PI / 1000f;
                    return;
                }
            }
        }

        // 回退到 FlightDataStreamReceiver（无直接爬升率属性，暂用0）
        if (receiver == null)
            receiver = FlightDataStreamReceiver.Instance;

        if (receiver == null)
            return;

        // FDSR 暂无垂直速度/爬升角数据，保持当前值
    }
}