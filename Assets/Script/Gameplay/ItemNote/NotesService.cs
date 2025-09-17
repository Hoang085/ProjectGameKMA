using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class NoteRef { public string subjectKey; public int sessionIndex; public string subjectDisplay; }

public class NotesService : MonoBehaviour
{
    public static NotesService Instance { get; private set; }
    const string PREF = "player_note_refs_v1";

    [Serializable] class SaveData { public List<NoteRef> noteRefs = new(); public List<SubjectName> subjectNames = new(); }
    [Serializable] public class SubjectName { public string key; public string display; }

    public List<NoteRef> noteRefs = new();
    public List<SubjectName> subjectNames = new();   // map key -> tên hiển thị

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject); Load();
    }

    public void AddNoteRef(string subjectKey, int sessionIndex, string subjectDisplay = null)
    {
        if (noteRefs.Exists(n => n.subjectKey == subjectKey && n.sessionIndex == sessionIndex)) return;
        noteRefs.Add(new NoteRef { subjectKey = subjectKey, sessionIndex = sessionIndex, subjectDisplay = subjectDisplay });
        if (!string.IsNullOrEmpty(subjectDisplay)) UpsertName(subjectKey, subjectDisplay);
        Save();
    }

    public string GetDisplayName(string key)
    {
        var m = subjectNames.Find(s => s.key == key);
        if (m != null && !string.IsNullOrEmpty(m.display)) return m.display;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key.Replace('_', ' ').ToLower());
    }

    void UpsertName(string key, string display) { var m = subjectNames.Find(s => s.key == key); if (m == null) subjectNames.Add(new() { key = key, display = display }); else m.display = display; }
    void Save() { PlayerPrefs.SetString(PREF, JsonUtility.ToJson(new SaveData { noteRefs = noteRefs, subjectNames = subjectNames })); PlayerPrefs.Save(); }
    void Load()
    {
        if (!PlayerPrefs.HasKey(PREF)) return;
        var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(PREF));
        noteRefs = d?.noteRefs ?? new(); subjectNames = d?.subjectNames ?? new();
    }
}
