using System.Collections.Generic;
using UnityEngine;

// Enum Thứ trong tuần
public enum Weekday { Mon, Tue, Wed, Thu, Fri, Sat, Sun }

// Enum ca trong ngày
public enum DaySlot { MorningA, MorningB, AfternoonA, AfternoonB, Evening }

[CreateAssetMenu(menuName = "Configs/CalendarConfig", fileName = "CalendarConfig")]
public class CalendarConfig : ScriptableObject
{
    [Header("Cấu hình năm học")]
    [Min(1)] public int termsPerYear = 2; // Năm học có mấy học kỳ
    [Min(1)] public int weeksPerTerm = 5; // Mỗi học kỳ có mấy tuần
    [Min(1)] public int daysPerWeek = 7; // Mỗi tuần có mấy ngày
    [Min(1)] public int slotsPerDay = 5; // Mỗi ngày có mấy ca

    [Header("Quy tắc xếp lớp")] // Các ngày trong tuần có thể dạy học
    [SerializeField]
    private List<Weekday> teachingDays = new()
    { Weekday.Mon, Weekday.Tue, Weekday.Wed, Weekday.Thu, Weekday.Fri };

    [Min(1)] public int maxSessionsPerCoursePerWeek = 3; // Tối đa số buổi học trên mỗi môn trong 1 tuần

    [Header("Slot bị chặn")] // Ca tối không thể dạy học
    [SerializeField] private List<DaySlot> blockedSlots = new() { DaySlot.Evening };

    public IReadOnlyList<Weekday> TeachingDays => teachingDays;
    public IReadOnlyList<DaySlot> BlockedSlots => blockedSlots;

#if UNITY_EDITOR
    private void OnValidate()
    {
        termsPerYear = Mathf.Max(1, termsPerYear); // Ít nhất 1 học kỳ
        weeksPerTerm = Mathf.Max(1, weeksPerTerm); // Ít nhất 1 tuần
        daysPerWeek = Mathf.Clamp(daysPerWeek, 1, 7); // Tối đa 7 ngày/tuần
        slotsPerDay = Mathf.Clamp(slotsPerDay, 1, 5); // Tối đa 5 ca/ngày
    }
#endif
}

