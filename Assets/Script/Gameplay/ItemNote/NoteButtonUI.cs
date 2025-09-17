using UnityEngine;
using UnityEngine.UI;

public class NoteButtonUI : MonoBehaviour
{
    public Text titleText;
    string _subjectKey;
    int _sessionIndex;

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
            if (ui) popup = ui.GetOrCreateNotePopup();
            if (!popup) popup = NotePopup.Instance ?? FindObjectOfType<NotePopup>(true);

            if (!popup)
            {
                Debug.LogError("[NoteButtonUI] Không tìm/khởi tạo được NotePopup. Hãy gán prefab vào GameUIManager.");
                return;
            }

            var title = titleText ? titleText.text : $"{subjectDisplay} - Buổi {sessionIndex}";
            popup.ShowByKey(_subjectKey, _sessionIndex, title);
        });
    }
}
