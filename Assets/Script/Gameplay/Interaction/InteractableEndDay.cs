using System.Collections;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableEndDay : MonoBehaviour
{
    [Header("UI hiển thị khi đứng gần")]
    public GameObject interactEndDayUI;

    [Header("Tag của Player")]
    public string playerTag = "Player";

    [Header("Phím tương tác")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Chống bấm lặp")]
    [SerializeField] private float interactCooldown = 0.2f;

    [Header("Chuyển ca")]
    [Tooltip("Thời gian chờ trước khi chuyển ca (giây)")]
    [SerializeField] private float delayBeforeSlotAdvance = 0.5f;

    [Header("Video (Optional)")]
    [SerializeField] private bool playVideo = false;        // Bật/tắt phát video
    [SerializeField] private VideoProfile videoProfile;     // Video profile để phát
    [SerializeField] private VideoPopupUI videoPopup;       // Reference đến VideoPopupUI (có thể đang inactive)

    [Header("Thông báo")]
    [SerializeField] private string notYetTimeMessage = "Chưa đến giờ về nhà";
    [SerializeField] private string nextDayMessage = "Đã sang ngày tiếp theo";

    private bool _playerInside;
    private float _lastInteractTime;
    private bool _waitingForVideo;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void Awake()
    {
        // Tìm kể cả khi VideoPopupUI đang inactive
        if (!videoPopup)
        {
            videoPopup = Resources.FindObjectsOfTypeAll<VideoPopupUI>().FirstOrDefault();
            if (videoPopup)
            {
                Debug.Log($"[InteractableEndDay] Đã tìm thấy VideoPopupUI: {videoPopup.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[InteractableEndDay] Không tìm thấy VideoPopupUI trong scene!");
            }
        }
    }

    private void Start()
    {
        if (interactEndDayUI != null)
            interactEndDayUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInside = true;
            if (interactEndDayUI != null)
                interactEndDayUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _playerInside = false;
            if (interactEndDayUI != null)
                interactEndDayUI.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (interactEndDayUI != null)
            interactEndDayUI.SetActive(false);
        _playerInside = false;
    }

    private void Update()
    {
        if (!_playerInside || _waitingForVideo) return;

        if (Input.GetKeyDown(interactKey) && Time.time - _lastInteractTime >= interactCooldown)
        {
            _lastInteractTime = Time.time;
            TryAdvanceSlot();
        }
    }

    private void TryAdvanceSlot()
    {
        if (GameClock.Ins == null)
        {
            Debug.LogWarning("[InteractableEndDay] GameClock.Ins is null!");
            return;
        }

        // Kiểm tra ca hiện tại (1-based: 1, 2, 3, 4, 5)
        int currentSlot = GameClock.Ins.SlotIndex1Based;

        // Nếu đang ở ca 1-4, chặn và hiển thị thông báo
        if (currentSlot >= 1 && currentSlot <= 4)
        {
            ShowNotification(notYetTimeMessage);
            Debug.Log($"[InteractableEndDay] Chặn chuyển ca - Đang ở ca {currentSlot}");
            return;
        }

        // Nếu đang ở ca 5, cho phép chuyển ngày
        if (currentSlot == 5)
        {
            // Ẩn UI prompt
            if (interactEndDayUI != null)
                interactEndDayUI.SetActive(false);

            // Nếu có video thì phát video trước khi chuyển ngày
            if (playVideo && videoProfile != null)
            {
                StartCoroutine(CoPlayVideoThenAdvance());
            }
            else
            {
                StartCoroutine(CoAdvanceWithoutVideo());
            }
        }
    }

    /// <summary>
    /// Phát video và sau đó chuyển ngày
    /// </summary>
    private IEnumerator CoPlayVideoThenAdvance()
    {
        _waitingForVideo = true;

        // Đảm bảo có VideoPopupUI, kể cả đang inactive
        if (!videoPopup)
            videoPopup = Resources.FindObjectsOfTypeAll<VideoPopupUI>().FirstOrDefault();

        if (!videoPopup)
        {
            Debug.LogWarning($"[InteractableEndDay] {name}: Không tìm thấy VideoPopupUI trong scene!");
            yield return CoAdvanceWithoutVideo();
            yield break;
        }

        // BẬT MonoBehaviour (GameObject) chứa VideoPopupUI nếu đang tắt
        if (!videoPopup.gameObject.activeInHierarchy)
        {
            Debug.Log("[InteractableEndDay] Kích hoạt VideoPopupUI GameObject...");
            videoPopup.gameObject.SetActive(true);
            // Chờ để đảm bảo Awake/OnEnable đã chạy
            yield return null;
        }

        // Phát video - PlayProfile_Inspector sẽ tự động kích hoạt panelRoot
        Debug.Log("[InteractableEndDay] Bắt đầu phát video...");
        videoPopup.PlayProfile_Inspector(videoProfile);

        // Đợi video kết thúc
        yield return videoPopup.WaitUntilFinished();

        Debug.Log("[InteractableEndDay] Video đã kết thúc!");

        _waitingForVideo = false;

        // Chuyển sang ngày tiếp theo sau khi video kết thúc
        if (delayBeforeSlotAdvance > 0)
        {
            yield return new WaitForSeconds(delayBeforeSlotAdvance);
        }

        DoAdvanceSlot();
    }

    /// <summary>
    /// Chuyển ngày không có video
    /// </summary>
    private IEnumerator CoAdvanceWithoutVideo()
    {
        _waitingForVideo = true;

        if (delayBeforeSlotAdvance > 0)
        {
            yield return new WaitForSeconds(delayBeforeSlotAdvance);
        }

        DoAdvanceSlot();

        _waitingForVideo = false;
    }

    private void DoAdvanceSlot()
    {
        GameClock.Ins.JumpToNextSessionStart();
        ShowNotification(nextDayMessage);
        Debug.Log("[InteractableEndDay] Đã chuyển sang ngày tiếp theo");
    }

    private void ShowNotification(string message)
    {
        if (NotificationPopupSpawner.Ins != null)
        {
            NotificationPopupSpawner.Ins.TriggerCustomNotification(message);
        }
        else
        {
            Debug.LogWarning("[InteractableEndDay] NotificationPopupSpawner.Ins is null!");
        }
    }
}