using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class NoteRef { public string subjectKey; public int sessionIndex; public string subjectDisplay; }
// Lop NotesService quan ly danh sach ghi chu va ten hien thi mon hoc
public class NotesService : MonoBehaviour
{
    public static NotesService Instance { get; private set; }
    const string PREF = "Note_Ref"; // Key luu tru trong PlayerPrefs

    [Serializable] class SaveData { public List<NoteRef> noteRefs = new(); public List<SubjectName> subjectNames = new(); }
    [Serializable] public class SubjectName { public string key; public string display; } // Map key voi ten hien thi

    public List<NoteRef> noteRefs = new(); // Danh sach ghi chu
    public List<SubjectName> subjectNames = new(); // Danh sach ten hien thi mon hoc

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject); Load();
    }

    // Them ghi chu moi
    public void AddNoteRef(string subjectKey, int sessionIndex, string subjectDisplay = null)
    {
        if (noteRefs.Exists(n => n.subjectKey == subjectKey && n.sessionIndex == sessionIndex)) return;
        noteRefs.Add(new NoteRef { subjectKey = subjectKey, sessionIndex = sessionIndex, subjectDisplay = subjectDisplay });
        if (!string.IsNullOrEmpty(subjectDisplay)) UpsertName(subjectKey, subjectDisplay);
        Save();
    }

    // Lay ten hien thi cho mon hoc
    public string GetDisplayName(string key)
    {
        var m = subjectNames.Find(s => s.key == key);
        if (m != null && !string.IsNullOrEmpty(m.display)) return m.display;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key.Replace('_', ' ').ToLower());
    }

    // Cap nhat hoac them ten hien thi mon hoc
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
}