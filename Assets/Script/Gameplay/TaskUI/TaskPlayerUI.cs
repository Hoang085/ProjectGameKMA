using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class TaskPlayerUI : MonoBehaviour
{
    [Serializable] public class SubjectName { public string key; public string display; }
    [Serializable] public class SubjectNameList { public List<SubjectName> items = new(); }

    [Header("UI parents")]
    [SerializeField] private Transform contentRoot;

    [Header("Prefabs")]
    [SerializeField] private GameObject boxTaskPrefab;

    // UI Task instances - chỉ để hiển thị
    private readonly Dictionary<string, BoxTask> uiTasks = new();
    private GameObject panelRoot;
    private Coroutine refreshCoroutine;

    void Awake()
    {
        InitializeComponents();
    }

    void OnEnable()
    {
        EnsurePanelActive();
        StartRefreshLoop();

        // Immediately sync with TaskManager
        RefreshUIFromTaskManager();
    }

    void OnDisable()
    {
        StopRefreshLoop();
    }

    void InitializeComponents()
    {
        panelRoot = transform.Find("ChildBackground")?.gameObject;
        if (!contentRoot)
            contentRoot = transform.Find("ChildBackground/Scroll View/Viewport/Content");

        EnsurePanelActive();
    }

    void EnsurePanelActive()
    {
        if (panelRoot && !panelRoot.activeSelf)
            panelRoot.SetActive(true);
    }

    void StartRefreshLoop()
    {
        if (gameObject.activeInHierarchy)
            refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    void StopRefreshLoop()
    {
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }

    IEnumerator RefreshLoop()
    {
        yield return null;
        while (gameObject.activeInHierarchy)
        {
            RefreshUIFromTaskManager();
            yield return new WaitForSeconds(2f);
        }
        refreshCoroutine = null;
    }

    /// <summary>
    /// Refresh UI based on TaskManager data
    /// </summary>
    void RefreshUIFromTaskManager()
    {
        if (TaskManager.Instance == null)
        {
            ClearAllUITasks();
            return;
        }

        var activeTasks = TaskManager.Instance.GetActiveTasks();

        // Remove UI tasks that no longer exist in TaskManager
        var keysToRemove = new List<string>();
        foreach (var kvp in uiTasks)
        {
            if (!activeTasks.ContainsKey(kvp.Key))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            RemoveUITask(key);
        }

        // Create or update UI tasks from TaskManager
        foreach (var kvp in activeTasks)
        {
            var taskData = kvp.Value;
            CreateOrUpdateUITask(taskData.key, taskData.title, taskData.detail,
                               taskData.buttonText, taskData.searchData);
        }
    }

    void CreateOrUpdateUITask(string key, string title, string detail, string btnText, string searchData)
    {
        bool isNewTask = !uiTasks.TryGetValue(key, out var task);

        if (isNewTask)
        {
            task = CreateNewUITask();
            if (task == null) return;
            uiTasks[key] = task;
        }

        // Setup task with callback to TaskManager
        task.Setup(key, title, detail, btnText, OnTaskButtonClicked, searchData);
    }

    BoxTask CreateNewUITask()
    {
        if (!boxTaskPrefab || !contentRoot) return null;
        var go = Instantiate(boxTaskPrefab, contentRoot);
        var box = go.GetComponent<BoxTask>();
        if (box == null) { DestroyImmediate(go); return null; }
        go.SetActive(true);
        return box;
    }

    void RemoveUITask(string key)
    {
        if (uiTasks.TryGetValue(key, out var task))
        {
            uiTasks.Remove(key);
            if (task != null && task.IsValid())
            {
                task.Cleanup();
                DestroyImmediate(task.gameObject);
            }
        }
    }

    void ClearAllUITasks()
    {
        foreach (var kvp in uiTasks)
        {
            if (kvp.Value != null && kvp.Value.IsValid())
            {
                kvp.Value.Cleanup();
                DestroyImmediate(kvp.Value.gameObject);
            }
        }
        uiTasks.Clear();
    }

    /// <summary>
    /// Handle task button click - delegate to TaskManager
    /// </summary>
    void OnTaskButtonClicked(string searchData)
    {
        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.HandleTaskAction(searchData);
        }
    }

    // === Public API for backward compatibility ===
    public bool HasPendingTasks()
    {
        return TaskManager.Instance != null ? TaskManager.Instance.HasPendingTasks() : false;
    }

    public int GetActiveTaskCount()
    {
        return TaskManager.Instance != null ? TaskManager.Instance.GetActiveTaskCount() : 0;
    }

    // === For GameUIManager compatibility ===
    public void UpdateTaskNotificationState()
    {
        // Delegate to TaskManager - UI doesn't manage notifications anymore
        if (TaskManager.Instance != null)
        {
            // TaskManager handles its own notifications
        }
    }
}