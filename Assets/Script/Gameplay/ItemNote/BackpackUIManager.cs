using UnityEngine;

// BackpackUIManager quan ly giao dien cua tui do, xu ly viec hien thi cac nut ghi chu
public class BackpackUIManager : MonoBehaviour
{
    public Transform notesContentRoot;
    public NoteButtonUI noteButtonTemplate;

    // Lam moi danh sach cac nut ghi chu
    public void RefreshNoteButtons()
    {
        for (int i = notesContentRoot.childCount - 1; i >= 0; i--) Destroy(notesContentRoot.GetChild(i).gameObject);
        foreach (var r in NotesService.Instance.noteRefs)
        {
            var display = !string.IsNullOrEmpty(r.subjectDisplay) ? r.subjectDisplay : NotesService.Instance.GetDisplayName(r.subjectKey);
            var btn = Instantiate(noteButtonTemplate, notesContentRoot);
            btn.Setup(r.subjectKey, display, r.sessionIndex);
        }
    }
}