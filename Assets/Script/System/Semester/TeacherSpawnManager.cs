using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý spawn/despawn giáo viên theo học kỳ hiện tại (GameClock.Term).
/// - Hỗ trợ nhiều SemesterConfig cho 1 giáo viên (list).
/// - Lọc theo kỳ hiện tại, tự respawn khi kỳ đổi (tùy chọn).
/// - Khi spawn, luôn gán TeacherAction.semesterConfig đúng với kỳ hiện tại (Cách 1).
/// </summary>
[DisallowMultipleComponent]
public class TeacherSpawnManager : Singleton<TeacherSpawnManager>
{
    [Header("Setup")]
    [Tooltip("Danh sách các giáo viên cần spawn")]
    public List<TeacherEntry> teachers = new List<TeacherEntry>();

    [Header("Semester / Term Options")]
    [Tooltip("Chỉ spawn giáo viên thuộc học kỳ hiện tại (GameClock.Term)")]
    public bool filterByCurrentTerm = true;

    [Tooltip("Tự động respawn đúng danh sách giáo viên khi học kỳ thay đổi")]
    public bool respawnOnTermChanged = true;

    [Header("Optional Debug")]
    [Tooltip("Tự động spawn phù hợp khi Start")]
    public bool spawnOnStart = true;

    // Cache instance theo teacherKey
    private readonly Dictionary<string, GameObject> _instances =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);

    private GameClock _clock;

    // ---------------- Singleton ----------------
    public override void Awake()
    {
        MakeSingleton(false); // Không DontDestroyOnLoad
    }

    private void OnEnable()
    {
        _clock = GameClock.Ins;
        if (_clock != null && respawnOnTermChanged)
        {
            _clock.OnTermChanged -= HandleTermChanged;
            _clock.OnTermChanged += HandleTermChanged;
        }
    }

    private void OnDisable()
    {
        if (_clock != null)
            _clock.OnTermChanged -= HandleTermChanged;
    }

    // ---------------- Lifecycle ----------------
    private void Start()
    {
        if (!spawnOnStart) return;

        if (filterByCurrentTerm) SpawnAllForCurrentTerm();
        else SpawnAll();
    }

    // ---------------- Public API ----------------
    [ContextMenu("Spawn All (no filter)")]
    public void SpawnAll()
    {
        foreach (var t in teachers)
        {
            if (t == null) continue;
            var key = MakeTeacherKey(t);
            if (!_instances.ContainsKey(key) || _instances[key] == null)
                Spawn(key);
        }
    }

    [ContextMenu("Despawn All")]
    public void DespawnAll()
    {
        foreach (var kv in _instances)
            if (kv.Value) Destroy(kv.Value);
        _instances.Clear();
    }

    [ContextMenu("Spawn All For Current Term")]
    public void SpawnAllForCurrentTerm()
    {
        if (_clock == null) _clock = GameClock.Ins;

        DespawnAll();

        foreach (var t in teachers)
        {
            if (t == null) continue;
            if (filterByCurrentTerm && !IsTeacherInCurrentTerm(t)) continue;

            var key = MakeTeacherKey(t);
            if (!_instances.ContainsKey(key) || _instances[key] == null)
                Spawn(key);
        }

        Debug.Log($"[TeacherSpawnManager] Spawn theo kỳ T{_clock?.Term}. Tổng: {_instances.Count}");
    }

    /// <summary>Spawn theo key (teacherId hoặc displayName nếu id trống).</summary>
    public GameObject Spawn(string teacherKey)
    {
        var entry = GetEntry(teacherKey);
        if (entry == null)
        {
            Debug.LogWarning($"[TeacherSpawnManager] Không tìm thấy entry '{teacherKey}'");
            return null;
        }

        if (filterByCurrentTerm && !IsTeacherInCurrentTerm(entry))
            return null; // Không thuộc kỳ hiện tại

        if (!entry.prefab || !entry.spawnPoint)
        {
            Debug.LogWarning($"[TeacherSpawnManager] Entry '{teacherKey}' thiếu prefab hoặc spawnPoint");
            return null;
        }

        if (_instances.TryGetValue(teacherKey, out var existed) && existed != null)
            return existed;

        var rot = entry.keepUprightOnSpawn
            ? Quaternion.Euler(0f, entry.spawnPoint.rotation.eulerAngles.y, 0f)
            : entry.spawnPoint.rotation;

        var go = Instantiate(entry.prefab, entry.spawnPoint.position, rot, transform);
        go.name = string.IsNullOrWhiteSpace(entry.displayName) ? entry.prefab.name : entry.displayName;

        SetupComponents(go, entry);
        _instances[teacherKey] = go;
        return go;
    }

    public void Despawn(string teacherKey)
    {
        if (_instances.TryGetValue(teacherKey, out var inst) && inst != null)
            Destroy(inst);
        _instances.Remove(teacherKey);
    }

    public void Respawn(string teacherKey)
    {
        Despawn(teacherKey);
        Spawn(teacherKey);
    }

    public GameObject GetInstance(string teacherKey)
    {
        return _instances.TryGetValue(teacherKey, out var inst) ? inst : null;
    }

    public TeacherEntry GetEntry(string teacherKey)
    {
        if (string.IsNullOrWhiteSpace(teacherKey)) return null;

        var e = teachers.Find(t => t != null && string.Equals(MakeTeacherKey(t), teacherKey, StringComparison.Ordinal));
        if (e != null) return e;

        return teachers.Find(t => t != null && string.Equals(t.displayName, teacherKey, StringComparison.Ordinal));
    }

    // ---------------- Term logic ----------------
    private void HandleTermChanged()
    {
        if (!respawnOnTermChanged) return;

        var term = _clock != null ? _clock.Term : -1;
        Debug.Log($"[TeacherSpawnManager] Term changed → T{term}. Respawn theo kỳ mới.");
        SpawnAllForCurrentTerm();
    }

    private bool IsTeacherInCurrentTerm(TeacherEntry e)
    {
        if (_clock == null) _clock = GameClock.Ins;

        if (e.semesterConfigs == null || e.semesterConfigs.Count == 0)
            return true; // Không gán config => hợp lệ mọi kỳ

        int currentTerm = _clock != null ? _clock.Term : -1;

        foreach (var sem in e.semesterConfigs)
            if (sem != null && sem.Semester == currentTerm)
                return true;

        return false;
    }

    // ---------------- Instance setup ----------------
    private void SetupComponents(GameObject go, TeacherEntry e)
    {
        if (e.spawnPoint)
        {
            var pos = e.spawnPoint.position;
            var rot = e.keepUprightOnSpawn
                ? Quaternion.Euler(0f, e.spawnPoint.rotation.eulerAngles.y, 0f)
                : e.spawnPoint.rotation;

            go.transform.SetPositionAndRotation(pos, rot);

            if (e.snapOnStart)
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb)
                {
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector3.zero;
#else
                    rb.velocity = Vector3.zero;
#endif
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        var teacher = go.GetComponent<TeacherAction>();
        if (teacher != null)
        {
            // ====== CÁCH 1: gán đúng SemesterConfig theo GameClock.Term ======
            if (_clock == null) _clock = GameClock.Ins;

            SemesterConfig matched = null;
            if (e.semesterConfigs != null && e.semesterConfigs.Count > 0)
            {
                int term = _clock != null ? _clock.Term : -1;
                matched = e.semesterConfigs.Find(s => s != null && s.Semester == term);
                if (matched == null) matched = e.semesterConfigs[0]; // fallback
            }
            teacher.semesterConfig = matched;

            // Sao chép danh sách môn
            if (e.subjects != null && e.subjects.Count > 0)
            {
                teacher.subjects = new List<SubjectEntry>(e.subjects.Count);
                foreach (var s in e.subjects)
                {
                    if (s == null) continue;
                    teacher.subjects.Add(new SubjectEntry
                    {
                        subjectName = s.subjectName,
                        subjectKeyForNotes = s.subjectKeyForNotes,
                        maxSessions = s.maxSessions,
                        currentSessionIndex = 0
                    });
                }
            }
        }

        if (e.disableDialogueActionOnSpawn)
        {
            var dlg = go.GetComponent<DialogueAction>();
            if (dlg != null) dlg.enabled = false;
        }
    }

    // ---------------- Helpers ----------------
    private static string MakeTeacherKey(TeacherEntry e)
    {
        if (!string.IsNullOrWhiteSpace(e.teacherId)) return e.teacherId.Trim();
        return string.IsNullOrWhiteSpace(e.displayName) ? "Teacher" : e.displayName.Trim();
    }

#if UNITY_EDITOR
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
            UnityEditor.Handles.Label(
                t.spawnPoint.position + Vector3.up * 0.25f,
                $"Teacher: {MakeTeacherKey(t)} ({t.displayName})"
            );
# endif
        }
    }
#endif
}

[Serializable]
public class TeacherEntry
{
    [Header("Định danh")]
    [Tooltip("ID duy nhất cho giáo viên (có thể bỏ trống, sẽ dùng displayName làm key)")]
    public string teacherId = "";
    [Tooltip("Tên hiển thị trong Hierarchy")]
    public string displayName = "Teacher";

    [Header("Prefab & Vị trí")]
    public GameObject prefab;
    public Transform spawnPoint;
    [Tooltip("Giữ trục đứng khi spawn")]
    public bool keepUprightOnSpawn = true;
    [Tooltip("Đặt velocity=0 khi spawn nếu có Rigidbody")]
    public bool snapOnStart = true;

    [Header("Lịch / Học kỳ")]
    [Tooltip("Danh sách các học kỳ mà giáo viên này dạy. Để trống = mọi kỳ.")]
    public List<SemesterConfig> semesterConfigs = new List<SemesterConfig>();

    [Header("Môn giảng dạy")]
    public List<SubjectEntry> subjects = new List<SubjectEntry>();

    [Header("Khác")]
    [Tooltip("Tắt DialogueAction khi spawn")]
    public bool disableDialogueActionOnSpawn = true;
}
