using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TeacherSpawnManager : Singleton<TeacherSpawnManager>
{
    [Header("Setup")]
    [Tooltip("Danh sách các giáo viên cần spawn")]
    public List<TeacherEntry> teachers = new List<TeacherEntry>();

    [Header("Optional Debug")]
    public bool spawnOnStart = true;

    // instance cache
    private readonly Dictionary<string, GameObject> _instances = new Dictionary<string, GameObject>();

    public override void Awake()
    {
        MakeSingleton(false);
    }

    void Start()
    {
        if (spawnOnStart) SpawnAll();
    }

    //Spawn tất cả entries chưa có instance.
    [ContextMenu("Spawn All Now")]
    public void SpawnAll()
    {
        foreach (var t in teachers)
        {
            if (string.IsNullOrWhiteSpace(t.subjectName)) continue;
            if (!_instances.ContainsKey(t.subjectName) || _instances[t.subjectName] == null)
                Spawn(t.subjectName);
        }
    }

    // Despawn toàn bộ giáo viên đang tồn tại.
    [ContextMenu("Despawn All Now")]
    public void DespawnAll()
    {
        foreach (var kv in _instances)
        {
            if (kv.Value) Destroy(kv.Value);
        }
        _instances.Clear();
    }

    // Spawn theo id (được đặt trong TeacherEntry.id).
    public GameObject Spawn(string subjectName)
    {
        var entry = GetEntry(subjectName);
        if (entry == null)
        {
            Debug.LogWarning($"[TeacherSpawnManager] Không tìm thấy entry subjectName='{subjectName}'");
            return null;
        }

        if (entry.prefab == null || entry.spawnPoint == null)
        {
            Debug.LogWarning($"[TeacherSpawnManager] Entry '{subjectName}' thiếu prefab hoặc spawnPoint");
            return null;
        }

        // nếu đã có instance thì trả về
        if (_instances.TryGetValue(subjectName, out var existed) && existed != null)
            return existed;

        // Tạo mới
        var rot = entry.keepUprightOnSpawn
            ? Quaternion.Euler(0f, entry.spawnPoint.rotation.eulerAngles.y, 0f)
            : entry.spawnPoint.rotation;

        var go = Instantiate(entry.prefab, entry.spawnPoint.position, rot, transform);
        go.name = entry.prefab.name;

        // Cấu hình InteractableNPC / TeacherAction nếu có
        SetupComponents(go, entry);

        _instances[subjectName] = go;
        return go;
    }

    // Xoá instance.
    public void Despawn(string subjectName)
    {
        if (_instances.TryGetValue(subjectName, out var inst) && inst != null)
            Destroy(inst);
        _instances.Remove(subjectName);
    }

    // Despawn rồi spawn lại
    public void Respawn(string subjectName)
    {
        Despawn(subjectName);
        Spawn(subjectName);
    }

    // Lấy instance hiện tại theo id.
    public GameObject GetInstance(string subjectName)
    {
        return _instances.TryGetValue(subjectName, out var inst) ? inst : null;
    }

    // Tìm entry theo id.
    public TeacherEntry GetEntry(string subjectName)
    {
        return teachers.Find(t => t != null && t.subjectName == subjectName);
    }

    private void SetupComponents(GameObject go, TeacherEntry e)
    {
        //  Snap vị trí khi spawn 
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
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        //  Ưu tiên TeacherAction, loại xung đột với DialogueAction 
        var teacher = go.GetComponent<TeacherAction>();
        if (teacher != null)
        {
            // Bơm data từ entry
            if (e.semesterConfig) teacher.semesterConfig = e.semesterConfig;
            teacher.subjectName = e.subjectName;
        }

        // Nếu trên prefab còn DialogueAction thì tắt để tránh mở hộp thoại thứ 2
        var dlg = go.GetComponent<DialogueAction>();
        if (dlg != null)
        {
            // Có thể Destroy component, hoặc chỉ disable để còn dùng sau
            // Destroy(dlg);
            dlg.enabled = false;
        }

        // (Tùy chọn) nếu bạn có InteractableNPC chọn action theo thứ tự,
        // có thể đảm bảo TeacherAction đứng trước:
        // var actions = go.GetComponents<InteractableAction>();
        // => Sắp xếp lại nếu cần (không bắt buộc nếu InteractableNPC đã xử lý tốt).
    }


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (teachers == null) return;
        Gizmos.matrix = Matrix4x4.identity;

        foreach (var t in teachers)
        {
            if (t == null || t.spawnPoint == null) continue;

            // Vẽ vị trí spawn
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(t.spawnPoint.position, 0.25f);
            Gizmos.DrawLine(t.spawnPoint.position, t.spawnPoint.position + t.spawnPoint.forward * 0.8f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(t.spawnPoint.position + Vector3.up * 0.25f, $"Teacher: {t.subjectName}");
#endif
        }
    }
#endif
}

[Serializable]
public class TeacherEntry
{
    [Header("Prefab & Vị trí")]
    public GameObject prefab;
    public Transform spawnPoint;

    public bool keepUprightOnSpawn = true;
    public bool snapOnStart = true;

    [Header("Lịch / Môn (gán vào TeacherAction)")]
    public SemesterConfig semesterConfig;
    public string subjectName = "Toan";
}
