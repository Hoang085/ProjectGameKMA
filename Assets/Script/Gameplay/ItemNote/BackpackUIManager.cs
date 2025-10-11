using UnityEngine;

// BackpackUIManager quan ly giao dien cua tui do, xu ly viec hien thi cac nut ghi chu
public class BackpackUIManager : MonoBehaviour
{
    public Transform notesContentRoot;
    public NoteButtonUI noteButtonTemplate;

    // Lam moi danh sach cac nut ghi chu
    public void RefreshNoteButtons()
    {
        // Clear
        for (int i = notesContentRoot.childCount - 1; i >= 0; i--)
            Destroy(notesContentRoot.GetChild(i).gameObject);

        var svc = NotesService.Instance;
        if (svc == null) return;

        foreach (var r in svc.noteRefs)
        {
            // Chuẩn hoá key chỉ để TRA MAP (JSON/subjectNames) cho chắc khớp
            var normKey = r.subjectKey.Replace(" ", "").Replace("_", "").ToLowerInvariant();

            // Ưu tiên tên tiếng Việt từ map; nếu chưa có thì rơi về SubjectDisplay; cuối cùng là chính key
            var mapped = svc.GetDisplayName(normKey);
            var display = !string.IsNullOrEmpty(mapped)
                ? mapped
                : (!string.IsNullOrEmpty(r.subjectDisplay) ? r.subjectDisplay : r.subjectKey);

            var btn = Instantiate(noteButtonTemplate, notesContentRoot);

            // LƯU Ý: vẫn truyền subjectKey gốc vào popup để không ảnh hưởng đường dẫn Resources
            btn.Setup(r.subjectKey, display, r.sessionIndex);
        }
    }
}