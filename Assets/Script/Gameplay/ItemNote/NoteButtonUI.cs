using UnityEngine;
using UnityEngine.UI;

// NoteButtonUI quan ly nut ghi chu trong giao dien
public class NoteButtonUI : MonoBehaviour
{
    public Text titleText;
    string _subjectKey;
    int _sessionIndex;

    // Thiet lap nut ghi chu
    public void Setup(string subjectKey, string subjectDisplay, int sessionIndex)
    {
        _subjectKey = subjectKey;
        _sessionIndex = sessionIndex;

        if (titleText) titleText.text = $"{subjectDisplay} - Buổi {sessionIndex}";

        var btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            NotePopup popup = null;
            var ui = GameUIManager.Ins;
            if (ui)
                popup = ui.GetOrCreateNotePopup();

            if (!popup)
                popup = NotePopup.Instance ?? FindFirstObjectByType<NotePopup>(FindObjectsInactive.Include);

            if (!popup)
            {
                Debug.LogError("[NoteButtonUI] Không tìm/khởi tạo được NotePopup. Hãy gán prefab vào GameUIManager.");
                return;
            }

            // Hien thi popup voi thong tin ghi chu
            var title = titleText ? titleText.text : $"{subjectDisplay} - Buổi {_sessionIndex}";
            popup.ShowByKey(_subjectKey, _sessionIndex, title);
        });
    }
}