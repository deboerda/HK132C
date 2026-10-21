using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 两个按钮控制数据回放：/// 1. PlayStopButton —开始停止（类似Unity 播放键）
/// 2. PauseResumeButton —暂停/继续
/// </summary>
public class PlaybackControlButtons : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlightPlaybackPanel playbackPanel;

    [Header("Play/Stop Button")]
    [SerializeField] private Button playStopButton;
    [SerializeField] private Text playStopText;
    [SerializeField] private string playLabel = "开始";
    [SerializeField] private string stopLabel = "停止";

    [Header("Pause/Resume Button")]
    [SerializeField] private Button pauseResumeButton;
    [SerializeField] private Text pauseResumeText;
    [SerializeField] private string pauseLabel = "暂停";
    [SerializeField] private string resumeLabel = "继续";

    [Header("Visual State")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.5f, 0.9f, 1f);
    [SerializeField] private Color activeColor = new Color(0.9f, 0.3f, 0.2f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

    private bool isPlaying;
    private bool isPaused;

    private void Start()
    {
        if (playbackPanel == null)
            playbackPanel = FindAnyObjectByType<FlightPlaybackPanel>();

        if (playStopButton != null)
            playStopButton.onClick.AddListener(OnPlayStopClicked);

        if (pauseResumeButton != null)
            pauseResumeButton.onClick.AddListener(OnPauseResumeClicked);

        UpdateUI();
    }

    private void Update()
    {
        // Sync state from FlightPlaybackPanel
        if (playbackPanel != null)
        {
            var state = playbackPanel.GetState();
            bool nowPlaying = state == FlightPlaybackPanel.PlaybackState.Playing;
            bool nowPaused = state == FlightPlaybackPanel.PlaybackState.Paused;

            if (nowPlaying != isPlaying || nowPaused != isPaused)
            {
                isPlaying = nowPlaying;
                isPaused = nowPaused;
                UpdateUI();
            }
        }
    }

    // ── Play / Stop ─────────────────────────────────────────────

    public void OnPlayStopClicked()
    {
        if (playbackPanel == null)
        {
            Debug.LogError("[PlaybackControlButtons] FlightPlaybackPanel not found");
            return;
        }

        var state = playbackPanel.GetState();
        if (state == FlightPlaybackPanel.PlaybackState.Playing || state == FlightPlaybackPanel.PlaybackState.Paused)
        {
            Debug.Log($"[PlaybackControlButtons] PlayStop clicked: {state} -> Stop");
            playbackPanel.OnStopClicked();
        }
        else
        {
            Debug.Log($"[PlaybackControlButtons] PlayStop clicked: {state} -> Start");
            playbackPanel.OnStartClicked();
        }

        SyncFromPlaybackPanel();
    }

    // ── Pause / Resume ──────────────────────────────────────────

    public void OnPauseResumeClicked()
    {
        if (playbackPanel == null)
        {
            Debug.LogError("[PlaybackControlButtons] FlightPlaybackPanel not found");
            return;
        }

        var state = playbackPanel.GetState();
        if (state == FlightPlaybackPanel.PlaybackState.Paused)
        {
            Debug.Log("[PlaybackControlButtons] PauseResume clicked: Paused -> Resume");
            playbackPanel.OnStartClicked(); // OnStartClicked handles resume when paused
        }
        else if (state == FlightPlaybackPanel.PlaybackState.Playing)
        {
            Debug.Log("[PlaybackControlButtons] PauseResume clicked: Playing -> Pause");
            playbackPanel.OnPauseClicked();
        }
        else
        {
            Debug.Log($"[PlaybackControlButtons] PauseResume ignored in state: {state}");
        }

        SyncFromPlaybackPanel();
    }

    // ── UI Update ───────────────────────────────────────────────

    private void UpdateUI()
    {
        // Play/Stop button
        if (playStopText != null)
            playStopText.text = (isPlaying || isPaused) ? stopLabel : playLabel;

        if (playStopButton != null)
        {
            var img = playStopButton.GetComponent<Image>();
            if (img != null)
                img.color = (isPlaying || isPaused) ? activeColor : normalColor;
        }

        // Pause/Resume button
        if (pauseResumeButton != null)
            pauseResumeButton.interactable = isPlaying || isPaused;

        if (pauseResumeText != null)
            pauseResumeText.text = isPaused ? resumeLabel : pauseLabel;

        if (pauseResumeButton != null && pauseResumeButton.interactable)
        {
            var img = pauseResumeButton.GetComponent<Image>();
            if (img != null)
                img.color = isPaused ? activeColor : normalColor;
        }
        else if (pauseResumeButton != null)
        {
            var img = pauseResumeButton.GetComponent<Image>();
            if (img != null)
                img.color = disabledColor;
        }
    }

    private void SyncFromPlaybackPanel()
    {
        if (playbackPanel == null)
        {
            return;
        }

        var state = playbackPanel.GetState();
        isPlaying = state == FlightPlaybackPanel.PlaybackState.Playing;
        isPaused = state == FlightPlaybackPanel.PlaybackState.Paused;
        UpdateUI();
    }
}
