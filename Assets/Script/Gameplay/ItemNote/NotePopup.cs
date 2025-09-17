using UnityEngine;
using UnityEngine.UI;

public class NotePopup : MonoBehaviour
{
    public static NotePopup Instance;
    public GameObject root;
    public Text TextTitle;
    public Text NoteContentText;
    public ScrollRect scrollRect;

    // Đặt mặc định theo thư mục bạn đang dùng
    [Header("Resources path")]
    public string resourcesRoot = "NoteItems";  // ví dụ: NoteItems
    public bool normalizeKey = false;           // nếu key đã là ToanCaoCap thì để false

    void Awake() { Instance = this; if (root) root.SetActive(false); }

    public void ShowByKey(string subjectKey, int sessionIndex, string displayTitle = null)
    {
        string key = normalizeKey ? subjectKey.Replace(" ", "").Replace("_", "") : subjectKey;
        string path = $"{resourcesRoot}/{key}/Buoi{sessionIndex}";

        TextAsset asset = Resources.Load<TextAsset>(path);
        TextTitle.text = displayTitle ?? $"{subjectKey} - Buổi {sessionIndex}";
        NoteContentText.supportRichText = true;
        NoteContentText.text = asset ? asset.text : $"(Không tìm thấy file tại Resources/{path}.txt)";
        root.SetActive(true);

        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void Hide() => root.SetActive(false);
}
