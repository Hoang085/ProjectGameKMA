using System;
using System.Linq;

// SemesterConfigUtil cung cap cac phuong thuc xu ly SemesterConfig
public class SemesterConfigUtil
{
    private static SemesterConfigUtil _instance;
    public static SemesterConfigUtil instance => _instance ??= new SemesterConfigUtil();

    private SemesterConfigUtil() { }

    // Chuan hoa chuoi: trim va chuyen thanh chu thuong
    private string N(string s) => (s ?? "").Trim().ToLowerInvariant();

    // Tim mon hoc tai ngay va ca, ho tro dinh dang ngay
    public SubjectData GetSubjectAt(SemesterConfig cfg, Weekday day, int slot)
    {
        if (cfg?.Subjects == null) return null;

        string dayEnumName = day.ToString(); // Ten enum: "Mon", "Tue", ...
        string dayEnglish = GameClock.WeekdayToEN(day); // Ten tieng Anh: "Monday", "Tuesday", ...
        string dayNum = ((int)day + 1).ToString(); // So thu tu ngay: 1..7

        foreach (var sub in cfg.Subjects)
        {
            if (sub?.Sessions == null) continue;

            // Kiem tra co phien hoc khop voi ngay va ca
            bool any = sub.Sessions.Any(s =>
            {
                string d = N(s.Day);
                return (d == N(dayEnumName) || d == N(dayEnglish) || d == N(dayNum))
                       && s.Slot == slot;
            });

            if (any) return sub; // Tra ve mon hoc neu tim thay
        }
        return null;
    }

    // Ho tro nguoc cho code cu, chuyen ten ngay thanh enum Weekday
    public SubjectData GetSubjectAt(SemesterConfig cfg, string dayName, int slot)
    {
        Weekday parsed = Weekday.Mon;
        string d = N(dayName);

        if (Enum.TryParse<Weekday>(dayName, true, out var byEnum))
            parsed = byEnum;
        else
        {
            foreach (Weekday w in Enum.GetValues(typeof(Weekday)))
                if (N(GameClock.WeekdayToEN(w)) == d) { parsed = w; break; }

            if (int.TryParse(dayName, out int num) && num >= 1 && num <= 7)
                parsed = (Weekday)(num - 1);
        }

        return GetSubjectAt(cfg, parsed, slot); // Goi phuong thuc chinh voi enum
    }
}