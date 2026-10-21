using UnityEngine;

public class Unity_ASI : MonoBehaviour
{
    [Header("UI Layers")]
    public RectTransform itemHand; // 空速指针

    [Header("Flight Data")]
    public float airspeed = 0f; // 空速 [knots]

    // 空速表刻度范围（根据实际需求调整）
    private const float minAirspeed = 0f;
    private const float maxAirspeed = 300f;

    void Update()
    {
        var receiver = FlightDataStreamReceiver.Instance;
        if (receiver != null)
        {
            airspeed = receiver.PrimaryAirspeedKnots;
        }
        
        // 计算指针旋转角度
        // 线性映射：0-300 knots 对应 0-360 度
        float normalizedAirspeed = Mathf.Clamp((airspeed - minAirspeed) / (maxAirspeed - minAirspeed), 0f, 1f);
        float angle = normalizedAirspeed * 360f;

        // 赋值（Unity下需要反向Z轴来匹配正逆时针）
        if (itemHand != null)
            itemHand.localEulerAngles = new Vector3(0, 0, -angle);
    }
}
