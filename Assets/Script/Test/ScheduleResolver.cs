using System;
using System.Collections.Generic;
using UnityEngine;

public static class ScheduleResolver
{
    // Hỗ trợ cả Day dạng enum (Mon/Tue/...) và string (EN/VN)
    public static bool IsSessionMatch(SemesterConfig sem, string subjectName, Weekday today, int slotIndex1Based)
    {
        if (sem == null || sem.Subjects == null) return false;

        foreach (var sub in sem.Subjects)
        {
            if (!string.Equals(sub.Name, subjectName, StringComparison.OrdinalIgnoreCase)) continue;
            if (sub.Sessions == null) continue;

            foreach (var ses in sub.Sessions)
            {
                if (TryParseWeekday(ses.Day, out var d))
                {
                    if (d == today && ses.Slot == slotIndex1Based) return true;
                }
            }
        }
        return false;
    }

    public static bool TryParseWeekday(string dayStr, out Weekday d)
    {
        d = Weekday.Mon;
        if (string.IsNullOrWhiteSpace(dayStr)) return false;

        // Chuẩn hóa
        var s = dayStr.Trim().ToLowerInvariant();
        // EN short
        if (s is "mon" or "monday") { d = Weekday.Mon; return true; }
        if (s is "tue" or "tuesday") { d = Weekday.Tue; return true; }
        if (s is "wed" or "wednesday") { d = Weekday.Wed; return true; }
        if (s is "thu" or "thursday") { d = Weekday.Thu; return true; }
        if (s is "fri" or "friday") { d = Weekday.Fri; return true; }
        if (s is "sat" or "saturday") { d = Weekday.Sat; return true; }
        if (s is "sun" or "sunday") { d = Weekday.Sun; return true; }

        // VN
        if (s is "thứ 2" or "thu 2" or "thu2" or "t2") { d = Weekday.Mon; return true; }
        if (s is "thứ 3" or "thu 3" or "thu3" or "t3") { d = Weekday.Tue; return true; }
        if (s is "thứ 4" or "thu 4" or "thu4" or "t4") { d = Weekday.Wed; return true; }
        if (s is "thứ 5" or "thu 5" or "thu5" or "t5") { d = Weekday.Thu; return true; }
        if (s is "thứ 6" or "thu 6" or "thu6" or "t6") { d = Weekday.Fri; return true; }
        if (s is "thứ 7" or "thu 7" or "thu7" or "t7") { d = Weekday.Sat; return true; }
        if (s is "chủ nhật" or "chunhat" or "cn") { d = Weekday.Sun; return true; }

        return false;
    }
}
