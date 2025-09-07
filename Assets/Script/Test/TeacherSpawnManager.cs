using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TeacherSpawnManager : MonoBehaviour
{
    public static TeacherSpawnManager I;

    [Header("Setup")]
    [Tooltip("Danh sách các giáo viên cần spawn")]
    public List<TeacherEntry> teachers = new List<TeacherEntry>();

    [Header("Optional Debug")]
    public bool spawnOnStart = true;

    // instance cache
    private readonly Dictionary<string, GameObject> _instances = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    void Start()
    {
        if (spawnOnStart) SpawnAll();
    }

    /// <summary>
    /// Spawn tất cả entries chưa có instance.
    /// </summary>
    [ContextMenu("Spawn All Now")]
    public void SpawnAll()
    {
        foreach (var t in teachers)
        {
            if (string.IsNullOrWhiteSpace(t.id)) continue;
            if (!_instances.ContainsKey(t.id) || _instances[t.id] == null)
                Spawn(t.id);
        }
    }

    /// <summary>
    /// Despawn toàn bộ giáo viên đang tồn tại.
    /// </summary>
    [ContextMenu("Despawn All Now")]
    public void DespawnAll()
    {
        foreach (var kv in _instances)
        {
            if (kv.Value) Destroy(kv.Value);
        }
        _instances.Clear();
    }

    /// <summary>
    /// Spawn theo id (được đặt trong TeacherEntry.id).
    /// </summary>
    public GameObject Spawn(string id)
    {
        var entry = GetEntry(id);
        if (entry == null)
        {
            Debug.LogWarning($"[TeacherSpawnManager] Không tìm thấy entry id='{id}'");
            return null;
        }

        if (entry.prefab == null || entry.spawnPoint == null)
        {
            Debug.LogWarning($"[TeacherSpawnManager] Entry '{id}' thiếu prefab hoặc spawnPoint");
            return null;
        }

        // nếu đã có instance thì trả về
        if (_instances.TryGetValue(id, out var existed) && existed != null)
            return existed;

        // Tạo mới
        var rot = entry.keepUprightOnSpawn
            ? Quaternion.Euler(0f, entry.spawnPoint.rotation.eulerAngles.y, 0f)
            : entry.spawnPoint.rotation;

        var go = Instantiate(entry.prefab, entry.spawnPoint.position, rot, transform);
        go.name = string.IsNullOrEmpty(entry.customName) ? $"Teacher_{id}" : entry.customName;

        // Cấu hình InteractableNPC / TeacherAction nếu có
        SetupComponents(go, entry);

        _instances[id] = go;
        return go;
    }

    /// <summary>
    /// Xoá instance theo id (nếu có).
    /// </summary>
    public void Despawn(string id)
    {
        if (_instances.TryGetValue(id, out var inst) && inst != null)
            Destroy(inst);
        _instances.Remove(id);
    }

    /// <summary>
    /// Despawn rồi spawn lại theo id.
    /// </summary>
    [ContextMenu("Respawn Selected (First)")]
    public void Respawn(string id)
    {
        Despawn(id);
        Spawn(id);
    }

    /// <summary>
    /// Lấy instance hiện tại theo id.
    /// </summary>
    public GameObject GetInstance(string id)
    {
        return _instances.TryGetValue(id, out var inst) ? inst : null;
    }

    /// <summary>
    /// Tìm entry theo id.
    /// </summary>
    public TeacherEntry GetEntry(string id)
    {
        return teachers.Find(t => t != null && t.id == id);
    }

    private void SetupComponents(GameObject go, TeacherEntry e)
    {
        // Snap vị trí khi spawn
        if (e.spawnPoint)
        {
            var pos = e.spawnPoint.position;
            var rot = e.keepUprightOnSpawn
                ? Quaternion.Euler(0f, e.spawnPoint.rotation.eulerAngles.y, 0f)
                : e.spawnPoint.rotation;

            go.transform.SetPositionAndRotation(pos, rot);

            var rb = go.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // TeacherAction: gán môn + semester config
        var action = go.GetComponent<TeacherAction>();
        if (action != null)
        {
            if (e.semesterConfig) action.semesterConfig = e.semesterConfig;
            if (!string.IsNullOrEmpty(e.subjectName)) action.subjectName = e.subjectName;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Tự động điền id nếu để trống
        foreach (var t in teachers)
        {
            if (t == null) continue;
            if (string.IsNullOrWhiteSpace(t.id))
            {
                if (t.prefab) t.id = t.prefab.name;
                else if (t.spawnPoint) t.id = t.spawnPoint.name;
            }
        }
    }

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

            // Nhãn
            var label = string.IsNullOrEmpty(t.id) ? "(no id)" : t.id;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(t.spawnPoint.position + Vector3.up * 0.25f, $"Teacher: {label}");
#endif
        }
    }
#endif
}

[Serializable]
public class TeacherEntry
{
    [Header("Identity")]
    [Tooltip("Khoá duy nhất để tham chiếu teacher này")]
    public string id;

    [Tooltip("Tên GameObject sau khi spawn (tuỳ chọn)")]
    public string customName;

    [Header("Prefab & Vị trí")]
    public GameObject prefab;
    [Tooltip("Điểm đứng của giáo viên")]
    public Transform spawnPoint;

    [Tooltip("Giữ đứng thẳng khi spawn (chỉ lấy rotation Y)")]
    public bool keepUprightOnSpawn = true;

    [Tooltip("Snap ngay khi spawn xong (và khi Start của InteractableNPC)")]
    public bool snapOnStart = true;

    [Header("Lịch / Môn (bơm vào TeacherAction)")]
    public SemesterConfig semesterConfig;
    [Tooltip("Tên môn khớp với Subjects.Name trong SemesterConfig")]
    public string subjectName = "Toan";
}
