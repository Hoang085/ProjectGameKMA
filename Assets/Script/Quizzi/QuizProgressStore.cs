using System;
using System.Collections.Generic;
using UnityEngine;

public static class QuizProgressStore
{
    static string KeyOrder(string k) => $"QuizOrder_{k}";
    static string KeyOffset(string k) => $"QuizOffset_{k}";
    static string KeyHash(string k) => $"QuizHash_{k}";

    public static void EnsureOrder(string subjectKey, int count, string dataHash)
    {
        var curHash = PlayerPrefs.GetString(KeyHash(subjectKey), "");
        var curOrder = PlayerPrefs.GetString(KeyOrder(subjectKey), "");
        if (curHash != dataHash || string.IsNullOrEmpty(curOrder))
        {
            // tạo thứ tự xáo trộn mới
            var order = new List<int>(count);
            for (int i = 0; i < count; i++) order.Add(i);
            var rng = new System.Random();
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            PlayerPrefs.SetString(KeyOrder(subjectKey), string.Join(",", order));
            PlayerPrefs.SetInt(KeyOffset(subjectKey), 0);
            PlayerPrefs.SetString(KeyHash(subjectKey), dataHash);
            PlayerPrefs.Save();
        }
    }

    public static List<int> TakeNext(string subjectKey, int need)
    {
        var orderStr = PlayerPrefs.GetString(KeyOrder(subjectKey), "");
        var offset = PlayerPrefs.GetInt(KeyOffset(subjectKey), 0);

        var order = new List<int>();
        foreach (var s in orderStr.Split(',')) if (!string.IsNullOrWhiteSpace(s)) order.Add(int.Parse(s));

        var picked = new List<int>(need);
        for (int k = 0; k < need; k++)
        {
            if (offset >= order.Count) offset = 0; // quay vòng sau khi dùng hết
            picked.Add(order[offset]);
            offset++;
        }
        PlayerPrefs.SetInt(KeyOffset(subjectKey), offset);
        PlayerPrefs.Save();
        return picked;
    }
}
