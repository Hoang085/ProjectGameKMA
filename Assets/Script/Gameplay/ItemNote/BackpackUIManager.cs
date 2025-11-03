using UnityEngine;
using HHH.Common;

public class BackpackUIManager : BasePopUp
{
    [Header("Refs")]
    public Transform notesContentRoot;
    public NoteButtonUI noteButtonTemplate;  

    public override void OnInitScreen()
    {
        base.OnInitScreen();
        if (!notesContentRoot) notesContentRoot = transform;
        if (noteButtonTemplate) noteButtonTemplate.gameObject.SetActive(false);
    }

    public override void OnShowScreen()
    {
        base.OnShowScreen();        
        RefreshNoteButtons();
    }

    public override void OnShowScreen(object arg)
    {
        base.OnShowScreen(arg);       
        RefreshNoteButtons();
    }

    public void RefreshNoteButtons()
    {
        // Clear
        for (int i = notesContentRoot.childCount - 1; i >= 0; i--)
        {
            var child = notesContentRoot.GetChild(i).gameObject;
            if (child == noteButtonTemplate.gameObject) continue; 
            Destroy(child);
        }

        var svc = NotesService.Instance;
        if (svc == null || noteButtonTemplate == null) return;

        foreach (var r in svc.noteRefs)
        {
            var normKey = r.subjectKey.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            var mapped = svc.GetDisplayName(normKey);
            var display = !string.IsNullOrEmpty(mapped)
                ? mapped
                : (!string.IsNullOrEmpty(r.subjectDisplay) ? r.subjectDisplay : r.subjectKey);

            var btn = Instantiate(noteButtonTemplate, notesContentRoot);
            btn.gameObject.SetActive(true);
            btn.Setup(r.subjectKey, display, r.sessionIndex);
        }
    }
}
