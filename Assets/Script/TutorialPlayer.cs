using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialPlayer : MonoBehaviour
{
    [Header("Cấu hình trang")]
    [SerializeField] private CanvasGroup[] pages;
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("Nút điều hướng")]
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;
    [SerializeField] private Button btnClose;

    private int currentIndex = 0;
    private Coroutine transitionRoutine;
    
    /// <summary>
    /// **MỚI: Lưu trạng thái GameClock trước khi pause để khôi phục**
    /// </summary>
    private bool _wasClockPausedBefore = false;

    private void Start()
    {
        // Chỉ bật trang đầu tiên
        for (int i = 0; i < pages.Length; i++)
            pages[i].gameObject.SetActive(i == 0);

        UpdateButtonVisibility();
        
        // **MỚI: Đăng ký sự kiện cho nút Close**
        if (btnClose != null)
        {
            btnClose.onClick.AddListener(OnClose);
        }
    }
    
    private void OnDestroy()
    {
        // **MỚI: Hủy đăng ký sự kiện**
        if (btnClose != null)
        {
            btnClose.onClick.RemoveListener(OnClose);
        }
    }

    public void NextPage()
    {
        if (currentIndex < pages.Length - 1)
            ShowPage(currentIndex + 1);
    }

    public void PrevPage()
    {
        if (currentIndex > 0)
            ShowPage(currentIndex - 1);
    }

    private void ShowPage(int newIndex)
    {
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionPage(currentIndex, newIndex));
        currentIndex = newIndex;
        UpdateButtonVisibility();
    }

    private IEnumerator TransitionPage(int from, int to)
    {
        CanvasGroup oldPage = pages[from];
        CanvasGroup newPage = pages[to];
        newPage.gameObject.SetActive(true);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = t / fadeDuration;
            oldPage.alpha = Mathf.Lerp(1, 0, a);
            newPage.alpha = Mathf.Lerp(0, 1, a);
            yield return null;
        }

        oldPage.alpha = 0;
        newPage.alpha = 1;
        oldPage.gameObject.SetActive(false);
    }

    private void UpdateButtonVisibility()
    {
        // Ẩn nút Prev ở trang đầu
        if (btnPrev) btnPrev.gameObject.SetActive(currentIndex > 0);

        // Ẩn nút Next ở trang cuối
        if (btnNext) btnNext.gameObject.SetActive(currentIndex < pages.Length - 1);

        // Chỉ hiện BtnClose ở trang cuối cùng (ví dụ TutorialPage6)
        if (btnClose) btnClose.gameObject.SetActive(currentIndex == pages.Length - 1);
    }
    
    /// <summary>
    /// **MỚI: Được gọi khi TutorialPlayer được kích hoạt**
    /// </summary>
    private void OnEnable()
    {
        PauseGameClock();
    }
    
    /// <summary>
    /// **MỚI: Tạm dừng GameClock khi tutorial hiển thị**
    /// </summary>
    private void PauseGameClock()
    {
        if (GameClock.Ins == null)
        {
            Debug.LogWarning("[TutorialPlayer] GameClock không tồn tại, không thể pause");
            return;
        }
        
        // Lưu trạng thái hiện tại trước khi pause
        _wasClockPausedBefore = GameClock.Ins.IsPaused;
        
        // Pause GameClock
        GameClock.Ins.Pause();
        
        Debug.Log("[TutorialPlayer] ⏸ Đã tạm dừng GameClock cho tutorial");
    }
    
    /// <summary>
    /// **MỚI: Tiếp tục GameClock khi đóng tutorial**
    /// </summary>
    private void ResumeGameClock()
    {
        if (GameClock.Ins == null)
        {
            Debug.LogWarning("[TutorialPlayer] GameClock không tồn tại, không thể resume");
            return;
        }
        
        // Chỉ resume nếu trước đó clock không bị pause
        if (!_wasClockPausedBefore)
        {
            GameClock.Ins.Resume();
            Debug.Log("[TutorialPlayer] ▶ Đã tiếp tục GameClock sau tutorial");
        }
    }
    
    /// <summary>
    /// **MỚI: Xử lý khi nhấn nút Close**
    /// </summary>
    private void OnClose()
    {
        ResumeGameClock();
        
        // Đóng tutorial (tắt GameObject)
        gameObject.SetActive(false);
        
        Debug.Log("[TutorialPlayer] ✓ Tutorial đã đóng");
    }
}
