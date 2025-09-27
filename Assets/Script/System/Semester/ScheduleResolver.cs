using System.Globalization;
using System.Text;
using UnityEngine;

// ScheduleResolver xu ly logic lien quan den lich hoc
public static class ScheduleResolver
{
    // Kiem tra mon hoc tai ngay va ca co khop voi ten mon
    public static bool IsSessionMatch(
        SemesterConfig sem, string subjectName, Weekday today, int slotIndex1Based)
    {
        var subjectNow = FindSubjectAt(sem, today, slotIndex1Based);
        return NameEquals(subjectNow, subjectName);
    }

    // Tim ten mon hoc tai ngay va ca cu the
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
                        return sub.Name ?? string.Empty; // Tra ve ten mon hoc
                }
            }
        }
        return string.Empty;
    }

    // So sanh ten mon hoc, bo qua dau va khong phan biet hoa/thuong
    public static bool NameEquals(string a, string b) => Normalize(a) == Normalize(b);

    // Chuan hoa chuoi: loai bo dau, chuyen thanh thuong, trim
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

    // Parse chuoi ngay thanh enum Weekday
    public static bool TryParseWeekday(string dayStr, out Weekday d)
    {
        d = Weekday.Mon;
        if (string.IsNullOrWhiteSpace(dayStr)) return false;
        var s = Normalize(dayStr);

        if (s is "mon" or "monday") { d = Weekday.Mon; return true; }
        if (s is "tue" or "tuesday") { d = Weekday.Tue; return true; }
        if (s is "wed" or "wednesday") { d = Weekday.Wed; return true; }
        if (s is "thu" or "thursday") { d = Weekday.Thu; return true; }
        if (s is "fri" or "friday") { d = Weekday.Fri; return true; }
        if (s is "sat" or "saturday") { d = Weekday.Sat; return true; }
        if (s is "sun" or "sunday") { d = Weekday.Sun; return true; }

        return false;
    }
}