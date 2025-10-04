using UnityEngine;
using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;

[DefaultExecutionOrder(10000)] // đảm bảo chạy sau ExamLoader
public class ExamSceneBoot : MonoBehaviour
{
    void Awake()
    {
        var loader = FindFirstObjectByType<ExamLoader>();
        if (!loader) { Debug.LogError("[ExamSceneBoot] Không tìm thấy ExamLoader trong scene."); return; }

        int idx = -1;

        // 1) Nếu có override từ route → ưu tiên tuyệt đối
        if (ExamRouteData.subjectIndexOverride.HasValue)
        {
            idx = ExamRouteData.subjectIndexOverride.Value;
            Debug.Log($"[ExamSceneBoot] Dùng override index = {idx}");
        }
        else
        {
            // 2) Thử tìm theo route name/key
            string routeName = string.IsNullOrWhiteSpace(ExamRouteData.subjectName)
                ? PlayerPrefs.GetString("LastExam_SubjectName", "")
                : ExamRouteData.subjectName;

            string routeKey = string.IsNullOrWhiteSpace(ExamRouteData.subjectKey)
                ? PlayerPrefs.GetString("LastExam_SubjectKey", "")
                : ExamRouteData.subjectKey;

            idx = FindSubjectIndexOnLoader(loader, routeName, routeKey);

            if (idx < 0)
            {
                // 🚩 Không có route hợp lệ:
                // → Giữ đúng chỉ số đã set ở Inspector để test
                loader.ApplyDefault();
                Debug.Log($"[ExamSceneBoot] Route rỗng/không khớp → dùng Inspector SubjectIndex={loader.subjectIndex}");
                return;
            }
        }

        ApplyIndexToLoader(loader, idx);
        StartCoroutine(ForceReapplyNextFrame(loader, idx));
    }

    IEnumerator ForceReapplyNextFrame(ExamLoader loader, int idx)
    {
        yield return new WaitForEndOfFrame();
        ApplyIndexToLoader(loader, idx);
    }

    // --- Helpers -------------------------------------------------------------

    int FindSubjectIndexOnLoader(ExamLoader loader, string displayName, string subjectKey)
    {
        // Lấy mảng/list "exams" trong ExamLoader (các phần tử có field displayName/resourcePath)
        var fiExams = typeof(ExamLoader).GetField("exams",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var examsList = fiExams?.GetValue(loader) as System.Collections.IList;
        if (examsList == null) return -1;

        // Ưu tiên key (không dấu, viết liền). Nếu không có key thì dùng displayName.
        string targetKey = KeyUtil.MakeKey(!string.IsNullOrWhiteSpace(subjectKey) ? subjectKey : displayName);

        for (int i = 0; i < examsList.Count; i++)
        {
            var item = examsList[i];
            var fDisplay = item.GetType().GetField("displayName",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string disp = fDisplay?.GetValue(item) as string;

            if (string.IsNullOrWhiteSpace(disp)) continue;

            string dispKey = KeyUtil.MakeKey(disp);
            if (dispKey == targetKey || string.Equals(disp, displayName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    void ApplyIndexToLoader(ExamLoader loader, int idx)
    {
        // Set property/field SubjectIndex
        bool setOK = TrySetSubjectIndex(loader, idx);

        // BẮT BUỘC: gọi 1 hàm chọn lại đề theo index
        bool invoked = TryInvokeAny(loader, new[]
        {
            "SelectByIndex",     // ưu tiên
            "LoadByIndex",
            "LoadExamByIndex",
            "ApplyIndex",
        }, idx);

        if (!invoked)
        {
            // Nếu không có hàm nhận index, thử các hàm “apply default / refresh”
            invoked = TryInvokeAny(loader, new[]
            {
                "ApplyDefault",
                "Reload",
                "Refresh",
                "Initialize",
                "Init",
                "Rebuild",
            });
        }

        Debug.Log($"[ExamSceneBoot] Set {(setOK ? "OK" : "FAIL")} & Apply ({(invoked ? "OK" : "MISS")}) SubjectIndex = {idx}.");
    }

    bool TrySetSubjectIndex(ExamLoader loader, int idx)
    {
        var p = typeof(ExamLoader).GetProperty("SubjectIndex",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite) { p.SetValue(loader, idx); return true; }

        var f = typeof(ExamLoader).GetField("subjectIndex",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null) { f.SetValue(loader, idx); return true; }

        // Thêm trường hợp có “currentIndex”
        var f2 = typeof(ExamLoader).GetField("_currentIndex",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f2 != null) { f2.SetValue(loader, idx); return true; }

        Debug.LogWarning("[ExamSceneBoot] Không tìm thấy field/property để set SubjectIndex.");
        return false;
    }

    bool TryInvokeAny(ExamLoader loader, IEnumerable<string> methodNames, params object[] args)
    {
        foreach (var name in methodNames)
        {
            var m = typeof(ExamLoader).GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) continue;

            var pars = m.GetParameters();
            if ((args == null || args.Length == 0) && pars.Length == 0)
            {
                m.Invoke(loader, null);
                return true;
            }
            if (args != null && pars.Length == args.Length)
            {
                m.Invoke(loader, args);
                return true;
            }
        }
        return false;
    }
}
