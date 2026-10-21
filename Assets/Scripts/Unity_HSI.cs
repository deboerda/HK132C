using UnityEngine;

public class Unity_HSI : MonoBehaviour
{
    [Header("UI Layers")]
    public RectTransform itemFace;
    public RectTransform itemHand;

    [Header("Flight Data")]
    public float heading = 0f;

    [Header("Data Source")]
    [SerializeField] private bool useManualInput = false;
    [SerializeField] private FlightDataStreamReceiver receiver;
    [SerializeField] private string trackId;
    [SerializeField] private bool usePrimaryTrack = true;

    public void SetHeading(float headingDegrees)
    {
        heading = headingDegrees;
    }

    public void SetTrackId(string id)
    {
        trackId = id;
        usePrimaryTrack = string.IsNullOrEmpty(id);
    }

    private void Update()
    {
        if (!useManualInput)
            UpdateData();

        float faceAngle = -heading;

        if (itemFace != null)
            itemFace.localEulerAngles = new Vector3(0, 0, faceAngle);
        if (itemHand != null)
            itemHand.localEulerAngles = Vector3.zero;
    }

    private void UpdateData()
    {
        if (receiver == null)
            receiver = FlightDataStreamReceiver.Instance;

        if (receiver == null)
            return;

        if (usePrimaryTrack || string.IsNullOrEmpty(trackId))
        {
            var pos = receiver.PrimaryPosition;
            if (pos != null)
                heading = pos.heading;
            return;
        }

        var positions = receiver.SnapshotPositions;
        var sel = positions.Find(p => p != null && p.trackId == trackId);
        if (sel != null)
            heading = sel.heading;
    }
}