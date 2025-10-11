using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class BoxTask : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private Button actionBtn;

    public string Key { get; private set; }

    // Lưu trữ callback và argument để có thể cleanup
    private Action<string> currentCallback;
    private string currentClickArg;

    public void Setup(string key, string title, string detail, string buttonText, Action<string> onClick, string clickArg)
    {
        Key = key;

        // Cleanup previous listeners trước khi setup mới
        CleanupEventListeners();

        // Khởi tạo UI components nếu chưa có
        InitializeUIComponents();

        // Setup UI content
        SetupUIContent(title, detail, buttonText);

        // Setup button event với proper cleanup
        SetupButtonEvent(onClick, clickArg);
    }

    private void InitializeUIComponents()
    {
        // Tìm titleText với fallback options
        if (!titleText)
        {
            titleText = transform.Find("BoxText/TittleText")?.GetComponent<TMP_Text>()
                     ?? transform.Find("BoxText/TitleText")?.GetComponent<TMP_Text>();
        }

        // Tìm detailText
        if (!detailText)
        {
            detailText = transform.Find("BoxText/ChildText")?.GetComponent<TMP_Text>();
        }

        // Tìm actionBtn
        if (!actionBtn)
        {
            actionBtn = transform.Find("BoxBtn")?.GetComponent<Button>();
        }
    }

    private void SetupUIContent(string title, string detail, string buttonText)
    {
        // Set title text
        if (titleText)
            titleText.text = title ?? string.Empty;

        // Set detail text
        if (detailText)
            detailText.text = detail ?? string.Empty;

        // Set button text
        if (actionBtn)
        {
            var btnLabel = actionBtn.GetComponentInChildren<TMP_Text>();
            if (btnLabel && !string.IsNullOrEmpty(buttonText))
                btnLabel.text = buttonText;
        }
    }

    private void SetupButtonEvent(Action<string> onClick, string clickArg)
    {
        if (!actionBtn) return;

        // Lưu trữ references để có thể cleanup sau
        currentCallback = onClick;
        currentClickArg = clickArg;

        // Thêm event listener mới
        if (onClick != null)
        {
            actionBtn.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        // Gọi callback với argument đã lưu
        currentCallback?.Invoke(currentClickArg);
    }

    private void CleanupEventListeners()
    {
        if (actionBtn)
        {
            // Remove tất cả listeners cũ để tránh memory leak
            actionBtn.onClick.RemoveAllListeners();
        }

        // Clear stored references
        currentCallback = null;
        currentClickArg = null;
    }

    // Được gọi khi GameObject bị destroy
    private void OnDestroy()
    {
        CleanupEventListeners();
        Debug.Log($"[BoxTask] Destroyed task with key: {Key}");
    }

    // Method để cleanup manual nếu cần
    public void Cleanup()
    {
        CleanupEventListeners();

        // Clear UI references
        titleText = null;
        detailText = null;
        actionBtn = null;

        Key = null;
    }

    // Method để validate state của BoxTask
    public bool IsValid()
    {
        return this != null &&
               gameObject != null &&
               !string.IsNullOrEmpty(Key);
    }

    // Method để refresh UI components nếu cần
    public void RefreshComponents()
    {
        InitializeUIComponents();
    }
}