using System.Globalization;
using System.Text;
using UnityEngine;

public static class ScheduleResolver
{
    /// <summary>
    /// Trả về true nếu tại (weekday, slot) hiện tại trong SemesterConfig
    /// có môn trùng với subjectName (so khớp bỏ dấu & không phân biệt hoa/thường).
    /// </summary>
    public static bool IsSessionMatch(
        SemesterConfig sem, string subjectName, Weekday today, int slotIndex1Based)
    {
        var subjectNow = FindSubjectAt(sem, today, slotIndex1Based);
        return NameEquals(subjectNow, subjectName);
    }

    /// <summary>
    /// Đọc SemesterConfig để tìm tên môn tại (weekday, slot). Trả về "" nếu không có.
    /// </summary>
    public static string FindSubjectAt(SemesterConfig sem, Weekday day, int slotIndex1Based)
    {
        if (!sem || sem.Subjects == null) return string.Empty;

        foreach (var sub in sem.Subjects)
        {
            if (sub == null || sub.Sessions == null) continue;

            foreach (var ses in sub.Sessions)
            {
                if (ses == null) continue;

                if (TryParseWeekday(ses.Day, out var d))
                {
                    if (d == day && ses.Slot == slotIndex1Based)
                        return sub.Name ?? string.Empty; // lấy TEXT trong ScriptableObject
                }
            }
        }
        return string.Empty;
    }

    /// <summary>So sánh bỏ dấu + không phân biệt hoa/thường.</summary>
    public static bool NameEquals(string a, string b) => Normalize(a) == Normalize(b);

    /// <summary>Chuẩn hoá chuỗi: trim, lower, bỏ dấu tiếng Việt.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var nf = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nf.Length);
        foreach (var ch in nf)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Parse thứ từ chuỗi trong ScriptableObject (EN hoặc VI).</summary>
    public static bool TryParseWeekday(string dayStr, out Weekday d)
    {
        d = Weekday.Mon;
        if (string.IsNullOrWhiteSpace(dayStr)) return false;
        var s = Normalize(dayStr);

        // en
        if (s is "mon" or "monday") { d = Weekday.Mon; return true; }
        if (s is "tue" or "tuesday") { d = Weekday.Tue; return true; }
        if (s is "wed" or "wednesday") { d = Weekday.Wed; return true; }
        if (s is "thu" or "thursday") { d = Weekday.Thu; return true; }
        if (s is "fri" or "friday") { d = Weekday.Fri; return true; }
        if (s is "sat" or "saturday") { d = Weekday.Sat; return true; }
        if (s is "sun" or "sunday") { d = Weekday.Sun; return true; }

        // vi
        if (s is "thu 2" or "thu2" or "t2") { d = Weekday.Mon; return true; }
        if (s is "thu 3" or "thu3" or "t3") { d = Weekday.Tue; return true; }
        if (s is "thu 4" or "thu4" or "t4") { d = Weekday.Wed; return true; }
        if (s is "thu 5" or "thu5" or "t5") { d = Weekday.Thu; return true; }
        if (s is "thu 6" or "thu6" or "t6") { d = Weekday.Fri; return true; }
        if (s is "thu 7" or "thu7" or "t7") { d = Weekday.Sat; return true; }
        if (s is "chu nhat" or "chunhat" or "cn") { d = Weekday.Sun; return true; }

        return false;
    }
}
