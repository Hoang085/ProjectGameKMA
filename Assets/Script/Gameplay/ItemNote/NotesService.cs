using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NoteRef
{
    public string subjectKey;
    public int sessionIndex;
    public string subjectDisplay;
}

public class NotesService : MonoBehaviour
{
    public static NotesService Instance { get; private set; }
    const string PREF = "Note_Ref"; // PlayerPrefs key

    [Serializable] class SaveData { public List<NoteRef> noteRefs = new(); public List<SubjectName> subjectNames = new(); }
    [Serializable] public class SubjectName { public string key; public string display; }

    // ====== Data in memory ======
    public List<NoteRef> noteRefs = new();
    public List<SubjectName> subjectNames = new();

    // ====== JSON config in Resources ======
    [Header("Subject display mapping (JSON in Resources)")]
    public string jsonPathInResources = "TextSubjectDisplay/subjectname";
    [Tooltip("Normalize keys when loading from JSON (remove space/underscore, lowercase).")]
    public bool normalizeKeyOnLoad = false;

    // Wrapper type for JsonUtility (root object with 'items' array)
    [Serializable]
    private class SubjectNameList { public List<SubjectName> items = new(); }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load(); // load PlayerPrefs (if any)

        // Load & merge JSON mapping (if exists)
        LoadSubjectNamesFromJson(jsonPathInResources, merge: true);

        // Save back so lần sau khỏi phải đọc JSON nữa (vẫn an toàn khi JSON không đổi)
        Save();
    }
    
    void OnEnable()
    {
        // **MỚI: Đăng ký sự kiện chuyển học kỳ để tự động xóa tài liệu**
        if (GameClock.Ins != null)
        {
            GameClock.Ins.OnTermChanged += HandleTermChanged;
        }
    }
    
    void OnDisable()
    {
        // **MỚI: Hủy đăng ký sự kiện khi disable**
        if (GameClock.Ins != null)
        {
            GameClock.Ins.OnTermChanged -= HandleTermChanged;
        }
    }
    
    /// <summary>
    /// **MỚI: Xử lý khi chuyển sang học kỳ mới - xóa toàn bộ tài liệu trong balo**
    /// </summary>
    private void HandleTermChanged()
    {
        Debug.Log("[NotesService] Học kỳ mới bắt đầu - Xóa toàn bộ tài liệu trong balo");
        ClearAllNotes();
        
        // Refresh BackpackUIManager nếu đang mở
        var backpackUI = FindAnyObjectByType<BackpackUIManager>();
        if (backpackUI != null && backpackUI.gameObject.activeInHierarchy)
        {
            backpackUI.RefreshNoteButtons();
            Debug.Log("[NotesService] Đã refresh BackpackUI sau khi xóa tài liệu");
        }
        
        // Clear notification icon for Balo
        if (GameManager.Ins != null)
        {
            GameManager.Ins.SetIconNotification(IconType.Balo, false);
            Debug.Log("[NotesService] Đã xóa notification icon Balo");
        }
    }

    // ------- Public API -------
    public void AddNoteRef(string subjectKey, int sessionIndex, string subjectDisplay = null)
    {
        if (noteRefs.Exists(n => n.subjectKey == subjectKey && n.sessionIndex == sessionIndex)) return;

        noteRefs.Add(new NoteRef { subjectKey = subjectKey, sessionIndex = sessionIndex, subjectDisplay = subjectDisplay });

        if (!string.IsNullOrEmpty(subjectDisplay)) UpsertName(subjectKey, subjectDisplay);

        Save();

        // ===== NOTIFICATION INTEGRATION =====
        // Trigger notification for new note
        if (GameManager.Ins != null)
        {
            GameManager.Ins.OnNoteAdded(subjectKey, sessionIndex);
        }
    }

    public string GetDisplayName(string key)
    {
        // 1) Ưu tiên map đã nạp (từ JSON hoặc AddNoteRef)
        var m = subjectNames.Find(s => s.key == key);
        if (m != null && !string.IsNullOrEmpty(m.display)) return m.display;

        // 2) Fallback: TitleCase từ key (giữ nguyên hành vi cũ)
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key.Replace('_', ' ').ToLower());
    }
    
    /// <summary>
    /// Xóa toàn bộ tài liệu trong balo
    /// Public method để có thể gọi từ bên ngoài nếu cần
    /// </summary>
    public void ClearAllNotes()
    {
        int count = noteRefs.Count;
        noteRefs.Clear();
        Save();
        
        Debug.Log($"[NotesService] ✓ Đã xóa {count} tài liệu khỏi balo");
    }

    // ------- Internal helpers -------
    void UpsertName(string key, string display)
    {
        var m = subjectNames.Find(s => s.key == key);
        if (m == null) subjectNames.Add(new() { key = key, display = display });
        else m.display = display;
    }

    void Save()
    {
        PlayerPrefs.SetString(PREF, JsonUtility.ToJson(new SaveData { noteRefs = noteRefs, subjectNames = subjectNames }));
        PlayerPrefs.Save();
    }

    void Load()
    {
        if (!PlayerPrefs.HasKey(PREF)) return;
        var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(PREF));
        noteRefs = d?.noteRefs ?? new();
        subjectNames = d?.subjectNames ?? new();
    }

    void LoadSubjectNamesFromJson(string resourcesPath, bool merge = true)
    {
        if (string.IsNullOrEmpty(resourcesPath)) return;
        var ta = Resources.Load<TextAsset>(resourcesPath);
        if (!ta) return;

        SubjectNameList list = null;
        try
        {
            list = JsonUtility.FromJson<SubjectNameList>(ta.text);
        }
        catch (Exception e)
        {
            Debug.LogError($"[NotesService] JSON parse error at Resources/{resourcesPath}.json: {e.Message}");
            return;
        }
        if (list == null || list.items == null) return;

        foreach (var item in list.items)
        {
            if (item == null) continue;
            if (string.IsNullOrEmpty(item.key) || string.IsNullOrEmpty(item.display)) continue;

            var key = item.key;
            if (normalizeKeyOnLoad)
                key = key.Replace(" ", "").Replace("_", "").ToLowerInvariant();

            if (merge)
            {
                UpsertName(key, item.display);
            }
            else
            {
                // Replace all: clear & add (not used in this build)
                if (subjectNames == null) subjectNames = new List<SubjectName>();
                subjectNames.Add(new SubjectName { key = key, display = item.display });
            }
        }
    }
    
    // ===== DEBUG METHODS =====
    
    /// <summary>
    /// Debug method để xem tất cả tài liệu hiện có
    /// </summary>
    [ContextMenu("Debug: List All Notes")]
    private void DebugListAllNotes()
    {
        Debug.Log($"[NotesService] ===== DANH SÁCH TÀI LIỆU ({noteRefs.Count}) =====");
        foreach (var note in noteRefs)
        {
            Debug.Log($"[NotesService]  - {note.subjectDisplay ?? note.subjectKey} - Buổi {note.sessionIndex}");
        }
    }
    
    /// <summary>
    /// Debug method để force clear tất cả tài liệu
    /// </summary>
    [ContextMenu("Debug: Force Clear All Notes")]
    private void DebugForceClearAllNotes()
    {
        ClearAllNotes();
        Debug.Log("[NotesService] ✓ Debug: Đã force clear tất cả tài liệu");
    }
}
