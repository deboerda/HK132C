using UnityEngine;

public class Unity_ALT : MonoBehaviour
{
    [Header("UI Layers")]
    public RectTransform itemHand_1; // 长针：万英尺针
    public RectTransform itemHand_2; // 短针：千英尺针
    public RectTransform itemFace_1; // 气压表盘

    [Header("Flight Data")]
    public float altitude = 0f;      // 高度 [ft]
    public float pressure = 28.0f;   // 气压 [inHg]

    void Update()
    {
        var receiver = FlightDataStreamReceiver.Instance;
        if (receiver != null)
        {
            altitude = receiver.PrimaryAltitudeMeters * 3.28084f;
        }

        // demo 数据里暂不包含气压，保留默认值即可
        if (pressure <= 0f)
        {
            pressure = 28.0f;
        }
        
        // Qt 比例测算逻辑：
        // altitude 每增加 1000英尺，H2 (百英尺/千英尺长分针) 转一圈 360 度 -> 乘 0.36
        // altitude 每增加 10000英尺，H1 (万英尺时针) 转一圈 360 度 -> 乘 0.036
        float angleH1 = altitude * 0.036f;
        float angleH2 = (altitude % 1000f) * 0.36f;
        
        // 28.0 是气压基准值
        float angleF1 = (pressure - 28.0f) * 100.0f; 
        
        // 赋值（Unity下需要反向Z轴来匹配正逆时针）
        if (itemHand_1 != null)
            itemHand_1.localEulerAngles = new Vector3(0, 0, -angleH1);
        if (itemHand_2 != null)
            itemHand_2.localEulerAngles = new Vector3(0, 0, -angleH2);
        if (itemFace_1 != null)
            itemFace_1.localEulerAngles = new Vector3(0, 0, angleF1); // 这里的符号通过比对正逆时针需求来定
    }
}
