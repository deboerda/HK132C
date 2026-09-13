using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仪表盘飞机选择器：使用场景中预设的 Dropdown（非运行时创建）：/// 选择后统一设置所有仪表和3D模型的trackId。/// 在Inspector 中将场景里的 Dropdown 组件拖到 dropdown 字段即可。/// </summary>
public class InstrumentTrackSelector : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Dropdown dropdown;

    [Header("Data")]
    [SerializeField] private float refreshInterval = 1f;

    private readonly List<string> dropdownTrackIds = new List<string>();
    private string selectedTrackId = "";
    private float nextRefreshTime;
    private readonly List<Component> instrumentComponents = new List<Component>();

    private bool listenerRegistered;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = transform as RectTransform;

        RegisterListener();
    }

    private void OnEnable()
    {
        RegisterListener();
        FindInstruments();
        PopulateDropdown();
    }

    private void Update()
    {
        // Ensure listener is registered even if OnEnable was missed (inactive panel)
        if (!listenerRegistered)
            RegisterListener();

        if (Time.unscaledTime < nextRefreshTime)
            return;
        nextRefreshTime = Time.unscaledTime + refreshInterval;
        PopulateDropdown();
    }

    private void RegisterListener()
    {
        if (dropdown == null || listenerRegistered) return;

        dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        dropdown.onValueChanged.AddListener(OnDropdownChanged);
        listenerRegistered = true;
    }

    // ── Instrument discovery ────────────────────────────────────

    private void FindInstruments()
    {
        instrumentComponents.Clear();

        var allMonoBehaviours = panelRoot.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in allMonoBehaviours)
        {
            if (mb == null) continue;
            var method = mb.GetType().GetMethod("SetTrackId", new[] { typeof(string) });
            if (method != null)
                instrumentComponents.Add(mb);
        }

        var attitude = FindAnyObjectByType<PlaneAttitudeController>();
        if (attitude != null && !instrumentComponents.Contains(attitude))
            instrumentComponents.Add(attitude);
    }

    // ── Dropdown population ──────────────────────────────────────

    private void PopulateDropdown()
    {
        if (dropdown == null) return;

        var receiver = FlightDataStreamReceiver.Instance;
        if (receiver == null) return;

        // Collect track IDs from both real-time positions and planned route lines
        var newTrackIds = new List<string>();

        var positions = receiver.SnapshotPositions;
        if (positions != null)
        {
            foreach (var pos in positions)
            {
                if (pos == null || string.IsNullOrWhiteSpace(pos.trackId)) continue;
                if (!newTrackIds.Contains(pos.trackId))
                    newTrackIds.Add(pos.trackId);
            }
        }

        // Also check track lines (planned routes) for track IDs
        var trackLines = receiver.SnapshotTrackLines;
        if (trackLines != null)
        {
            foreach (var tl in trackLines)
            {
                if (tl == null || string.IsNullOrWhiteSpace(tl.trackId)) continue;
                if (!newTrackIds.Contains(tl.trackId))
                    newTrackIds.Add(tl.trackId);
            }
        }

        // Also check timetable states
        var states = receiver.SnapshotStates;
        if (states != null)
        {
            foreach (var s in states)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.trackId)) continue;
                if (!newTrackIds.Contains(s.trackId))
                    newTrackIds.Add(s.trackId);
            }
        }

        if (newTrackIds.Count == 0) return;

        bool changed = newTrackIds.Count != dropdownTrackIds.Count;
        if (!changed)
        {
            for (int i = 0; i < newTrackIds.Count; i++)
            {
                if (dropdownTrackIds[i] != newTrackIds[i]) { changed = true; break; }
            }
        }
        if (!changed) return;

        dropdownTrackIds.Clear();
        dropdown.ClearOptions();

        var options = new List<Dropdown.OptionData>();
        foreach (var trackId in newTrackIds)
        {
            dropdownTrackIds.Add(trackId);
            options.Add(new Dropdown.OptionData(trackId));
        }

        dropdown.AddOptions(options);

        int selectedIndex = 0;
        if (!string.IsNullOrEmpty(selectedTrackId))
        {
            int idx = dropdownTrackIds.IndexOf(selectedTrackId);
            if (idx >= 0) selectedIndex = idx;
        }
        else if (dropdownTrackIds.Count > 0)
        {
            selectedTrackId = dropdownTrackIds[0];
        }
        dropdown.SetValueWithoutNotify(selectedIndex);
        dropdown.RefreshShownValue();

        // Auto-select first plane on initial load only
        if (!string.IsNullOrEmpty(selectedTrackId) && instrumentComponents.Count == 0)
        {
            OnDropdownChanged(0);
        }
    }

    // ── Selection handler ────────────────────────────────────────

    private void OnDropdownChanged(int index)
    {
        if (instrumentComponents.Count == 0)
            FindInstruments();

        if (index >= 0 && index < dropdownTrackIds.Count)
        {
            selectedTrackId = dropdownTrackIds[index];
            foreach (var comp in instrumentComponents)
            {
                if (comp == null) continue;
                comp.GetType().GetMethod("SetTrackId", new[] { typeof(string) })
                    ?.Invoke(comp, new object[] { selectedTrackId });
            }
        }
    }
}
