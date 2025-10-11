using UnityEngine;
using UnityEngine.UI;

// NotePopup quan ly giao dien popup hien thi noi dung ghi chu
public class NotePopup : MonoBehaviour
{
    public static NotePopup Instance;
    public GameObject root;
    public Text TextTitle;
    public Text NoteContentText;
    public ScrollRect scrollRect;

    [Header("Resources path")]
    public string resourcesRoot = "NoteItems"; // Duong dan goc trong Resources
    public bool normalizeKey = false; // Co chuan hoa key hay khong

    void Awake() { Instance = this; if (root) root.SetActive(false); }

    // Hien thi popup voi noi dung ghi chu
    public void ShowByKey(string subjectKey, int sessionIndex, string displayTitle = null)
    {
        // Chuan hoa key neu can
        string key = normalizeKey ? subjectKey.Replace(" ", "").Replace("_", "") : subjectKey;
        string path = $"{resourcesRoot}/{key}/Buoi{sessionIndex}";

        // Tai noi dung tu file TextAsset
        TextAsset asset = Resources.Load<TextAsset>(path);

        TextTitle.text = displayTitle ?? $"{subjectKey} - Buổi {sessionIndex}";
        NoteContentText.supportRichText = true;
        NoteContentText.text = asset ? asset.text : $"(No find at Resources/{path}.txt)";
        root.SetActive(true); // Hien thi popup

        // Cuon len dau noi dung
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }
    public void Hide() => root.SetActive(false);
}