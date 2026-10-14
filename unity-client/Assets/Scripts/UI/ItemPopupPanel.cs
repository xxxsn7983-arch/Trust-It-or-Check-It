using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 物品弹窗面板 - 居中显示图片/视频，支持收集线索
/// 周围半透明遮罩，点击遮罩或关闭按钮关闭
/// </summary>
public class ItemPopupPanel : MonoBehaviour
{
    [Header("面板引用")]
    public Button darkBackground;
    public Image popupImage;
    public RawImage videoDisplay;
    public VideoPlayer videoPlayer;
    public Button collectButton;
    public Button closeButton;

    [Header("内容容器（会被显示/隐藏的子物体）")]
    public GameObject popupContent;

    private ItemData currentData;
    private RenderTexture renderTexture;
    private bool isOpen;

    void Awake()
    {
        renderTexture = new RenderTexture(1920, 1080, 0);
        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.loopPointReached += OnVideoFinished;
        }
        if (videoDisplay != null)
        {
            videoDisplay.texture = renderTexture;
        }
    }

    void Start()
    {
        // 启动时隐藏内容（脚本所在物体保持 active 以维持事件订阅）
        if (popupContent != null) popupContent.SetActive(false);
    }

    void OnEnable()
    {
        GameEvents.OnItemPopupOpen += HandleOpen;
        GameEvents.OnItemPopupClose += HandleClose;

        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
        if (darkBackground != null) darkBackground.onClick.AddListener(OnCloseClicked);
        if (collectButton != null) collectButton.onClick.AddListener(OnCollectClicked);
    }

    void OnDisable()
    {
        GameEvents.OnItemPopupOpen -= HandleOpen;
        GameEvents.OnItemPopupClose -= HandleClose;

        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        if (darkBackground != null) darkBackground.onClick.RemoveListener(OnCloseClicked);
        if (collectButton != null) collectButton.onClick.RemoveListener(OnCollectClicked);
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    private void HandleOpen(ItemData data)
    {
        if (isOpen) HandleClose();

        currentData = data;
        isOpen = true;

        if (popupContent != null) popupContent.SetActive(true);

        if (data.videoClip != null)
        {
            // 有视频：先播放视频
            if (popupImage != null) popupImage.gameObject.SetActive(false);
            if (videoDisplay != null) videoDisplay.gameObject.SetActive(true);
            if (collectButton != null) collectButton.gameObject.SetActive(false);

            videoPlayer.clip = data.videoClip;
            videoPlayer.Play();
        }
        else
        {
            // 无视频：直接显示图片
            ShowImage();
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (videoDisplay != null) videoDisplay.gameObject.SetActive(false);
        ShowImage();
    }

    private void ShowImage()
    {
        if (popupImage != null)
        {
            popupImage.gameObject.SetActive(true);
            popupImage.sprite = currentData.popupImage;
        }

        // 有可收集线索时显示收集按钮
        bool hasClue = currentData.collectibleClue != null
                    && !string.IsNullOrEmpty(currentData.collectibleClue.clueId);
        if (collectButton != null) collectButton.gameObject.SetActive(hasClue);
    }

    private void OnCollectClicked()
    {
        if (currentData?.collectibleClue != null)
        {
            GameEvents.FireClueCollected(currentData.collectibleClue);
            if (collectButton != null) collectButton.gameObject.SetActive(false);
        }
    }

    private void OnCloseClicked()
    {
        GameEvents.FireItemPopupClose();
    }

    private void HandleClose()
    {
        isOpen = false;
        currentData = null;

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (videoDisplay != null) videoDisplay.gameObject.SetActive(false);
        if (popupImage != null) popupImage.gameObject.SetActive(false);
        if (collectButton != null) collectButton.gameObject.SetActive(false);
        if (popupContent != null) popupContent.SetActive(false);
    }
}
