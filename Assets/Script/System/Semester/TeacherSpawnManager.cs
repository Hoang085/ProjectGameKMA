using System;
using System.Collections.Generic;
using UnityEngine;

// Quan ly spawn va despawn giao vien
[DisallowMultipleComponent]
public class TeacherSpawnManager : Singleton<TeacherSpawnManager>
{
    [Header("Setup")]
    [Tooltip("Danh sach cac giao vien can spawn")]
    public List<TeacherEntry> teachers = new List<TeacherEntry>(); // Danh sach thong tin giao vien

    [Header("Optional Debug")]
    public bool spawnOnStart = true; // Tu dong spawn khi start

    // Cache instance theo teacherId
    private readonly Dictionary<string, GameObject> _instances = new Dictionary<string, GameObject>(StringComparer.Ordinal);

    // Khoi tao singleton
    public override void Awake()
    {
        MakeSingleton(false);
    }

    // Spawn tat ca giao vien khi start neu duoc bat
    private void Start()
    {
        if (spawnOnStart) SpawnAll();
    }

    // Spawn tat ca giao vien chua co instance
    [ContextMenu("Spawn All Now")]
    public void SpawnAll()
    {
        foreach (var t in teachers)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.teacherId)) continue;
            if (!_instances.ContainsKey(t.teacherId) || _instances[t.teacherId] == null)
                Spawn(t.teacherId); // Spawn giao vien
        }
    }

    // Despawn tat ca giao vien
    [ContextMenu("Despawn All Now")]
    public void DespawnAll()
    {
        foreach (var kv in _instances)
        {
            if (kv.Value) Destroy(kv.Value);
        }
        _instances.Clear(); // Xoa cache
    }

    // Spawn giao vien theo teacherId
    public GameObject Spawn(string teacherId)
    {
        var entry = GetEntry(teacherId);
        if (entry == null)
        {
            Debug.LogWarning($"[TeacherSpawnManager] Không tìm thấy entry teacherId='{teacherId}'");
            return null;
        }

        if (!entry.prefab || !entry.spawnPoint)
        {
            Debug.LogWarning($"[TeacherSpawnManager] Entry '{teacherId}' thiếu prefab hoặc spawnPoint");
            return null;
        }

        // Tra ve instance neu da ton tai
        if (_instances.TryGetValue(teacherId, out var existed) && existed != null)
            return existed;

        // Tao instance moi
        var rot = entry.keepUprightOnSpawn
            ? Quaternion.Euler(0f, entry.spawnPoint.rotation.eulerAngles.y, 0f)
            : entry.spawnPoint.rotation;

        var go = Instantiate(entry.prefab, entry.spawnPoint.position, rot, transform);
        go.name = string.IsNullOrWhiteSpace(entry.displayName) ? entry.prefab.name : entry.displayName;

        // Cau hinh component tren instance
        SetupComponents(go, entry);

        _instances[teacherId] = go;
        return go;
    }

    // Despawn giao vien theo teacherId
    public void Despawn(string teacherId)
    {
        if (_instances.TryGetValue(teacherId, out var inst) && inst != null)
            Destroy(inst);
        _instances.Remove(teacherId);
    }

    // Despawn va spawn lai giao vien
    public void Respawn(string teacherId)
    {
        Despawn(teacherId);
        Spawn(teacherId);
    }

    // Lay instance giao vien theo teacherId
    public GameObject GetInstance(string teacherId)
    {
        return _instances.TryGetValue(teacherId, out var inst) ? inst : null;
    }

    // Tim entry giao vien theo teacherId
    public TeacherEntry GetEntry(string teacherId)
    {
        return teachers.Find(t => t != null && t.teacherId == teacherId);
    }

    // Cau hinh component cho instance giao vien
    private void SetupComponents(GameObject go, TeacherEntry e)
    {
        // Dat vi tri va xoay
        if (e.spawnPoint)
        {
            var pos = e.spawnPoint.position;
            var rot = e.keepUprightOnSpawn
                ? Quaternion.Euler(0f, e.spawnPoint.rotation.eulerAngles.y, 0f)
                : e.spawnPoint.rotation;

            go.transform.SetPositionAndRotation(pos, rot);

            // Snap velocity neu co Rigidbody
            if (e.snapOnStart)
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        // Cau hinh TeacherAction
        var teacher = go.GetComponent<TeacherAction>();
        if (teacher != null)
        {
            if (e.semesterConfig) teacher.semesterConfig = e.semesterConfig;
            if (!string.IsNullOrWhiteSpace(e.overrideTitleText)) teacher.titleText = e.overrideTitleText;

            // Gan danh sach mon hoc
            if (e.subjects != null && e.subjects.Count > 0)
            {
                teacher.subjects = new List<SubjectEntry>(e.subjects.Count);
                foreach (var s in e.subjects)
                {
                    if (s == null) continue;
                    var copy = new SubjectEntry
                    {
                        subjectName = s.subjectName,
                        subjectKeyForNotes = s.subjectKeyForNotes,
                        maxSessions = s.maxSessions,
                        currentSessionIndex = 0 // Tien do se duoc tai trong Awake()
                    };
                    teacher.subjects.Add(copy);
                }
            }
        }

        // Tat DialogueAction neu can
        if (e.disableDialogueActionOnSpawn)
        {
            var dlg = go.GetComponent<DialogueAction>();
            if (dlg != null) dlg.enabled = false;
        }
    }

#if UNITY_EDITOR
    // Ve gizmos de debug vi tri spawn
    private void OnDrawGizmosSelected()
    {
        if (teachers == null) return;
        Gizmos.matrix = Matrix4x4.identity;

        foreach (var t in teachers)
        {
            if (t == null || t.spawnPoint == null) continue;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(t.spawnPoint.position, 0.25f);
            Gizmos.DrawLine(t.spawnPoint.position, t.spawnPoint.position + t.spawnPoint.forward * 0.8f);

# if UNITY_EDITOR
            UnityEditor.Handles.Label(t.spawnPoint.position + Vector3.up * 0.25f, $"Teacher: {t.displayName} ({t.teacherId})");
# endif
        }
    }
#endif
}

// Thong tin cau hinh cho mot giao vien
[Serializable]
public class TeacherEntry
{
    [Header("Dinh danh")]
    [Tooltip("ID duy nhat cho giao vien")]
    public string teacherId = "GV_Toan_01"; // ID giao vien
    [Tooltip("Ten hien thi trong Hierarchy")]
    public string displayName = "Teacher Toan 01"; // Ten hien thi

    [Header("Prefab & Vi tri")]
    public GameObject prefab; // Prefab giao vien
    public Transform spawnPoint; // Vi tri spawn
    [Tooltip("Giu truc dung khi spawn")]
    public bool keepUprightOnSpawn = true; // Giu truc dung
    [Tooltip("Dat velocity=0 khi spawn neu co Rigidbody")]
    public bool snapOnStart = true; // Snap velocity

    [Header("Lich / UI")]
    public SemesterConfig semesterConfig; // Cau hinh hoc ky
    [Tooltip("Ghi de titleText trong TeacherAction")]
    public string overrideTitleText = "Giang vien"; // Tieu de giao dien

    [Header("Mon giang day")]
    public List<SubjectEntry> subjects = new List<SubjectEntry>(); // Danh sach mon day

    [Header("Khac")]
    [Tooltip("Tat DialogueAction khi spawn")]
    public bool disableDialogueActionOnSpawn = true; // Tat DialogueAction
}