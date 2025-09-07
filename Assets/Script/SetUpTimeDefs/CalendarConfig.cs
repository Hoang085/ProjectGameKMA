using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enum ngày trong tuần (Mon = Thứ 2)
/// </summary>
public enum Weekday { Mon, Tue, Wed, Thu, Fri, Sat, Sun }

/// <summary>
/// Enum ca trong ngày
/// </summary>
// CalendarConfig.cs (chỉ phần cần thay đổi)

public enum DaySlot { MorningA, MorningB, AfternoonA, AfternoonB, Evening } // + Evening

[CreateAssetMenu(menuName = "Configs/CalendarConfig", fileName = "CalendarConfig")]
public class CalendarConfig : ScriptableObject
{
    [Header("Cấu hình năm học")]
    [Min(1)] public int termsPerYear = 2;
    [Min(1)] public int weeksPerTerm = 5;
    [Min(1)] public int daysPerWeek = 7;
    [Min(1)] public int slotsPerDay = 5; // = 5 sẽ có Tối; để 4 nếu chưa cần

    [Header("Quy tắc xếp lớp")]
    [SerializeField]
    private List<Weekday> teachingDays = new()
    { Weekday.Mon, Weekday.Tue, Weekday.Wed, Weekday.Thu, Weekday.Fri };

    [Min(1)] public int maxSessionsPerCoursePerWeek = 2;

    [Header("Slot bị chặn (không cho xếp hoạt động)")]
    [SerializeField] private List<DaySlot> blockedSlots = new() { DaySlot.Evening };

    public IReadOnlyList<Weekday> TeachingDays => teachingDays;
    public IReadOnlyList<DaySlot> BlockedSlots => blockedSlots;

#if UNITY_EDITOR
    private void OnValidate()
    {
        termsPerYear = Mathf.Max(1, termsPerYear);
        weeksPerTerm = Mathf.Max(1, weeksPerTerm);
        daysPerWeek = Mathf.Clamp(daysPerWeek, 1, 7);
        slotsPerDay = Mathf.Clamp(slotsPerDay, 1, 5); // tối đa 5 vì có Evening
    }
#endif
}

