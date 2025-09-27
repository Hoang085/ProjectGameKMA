using System.Collections.Generic;
using UnityEngine;

// Enum thu trong tuan
public enum Weekday { Mon, Tue, Wed, Thu, Fri, Sat, Sun }

// Enum ca trong ngay
public enum DaySlot { MorningA, MorningB, AfternoonA, AfternoonB, Evening }

// Cau hinh lich hoc, luu tru duoi dang ScriptableObject
[CreateAssetMenu(menuName = "Configs/CalendarConfig", fileName = "CalendarConfig")]
public class CalendarConfig : ScriptableObject
{
    [Header("Cau hinh nam hoc")]
    [Min(1)] public int termsPerYear = 2; // So hoc ky trong nam
    [Min(1)] public int weeksPerTerm = 5; // So tuan trong mot hoc ky
    [Min(1)] public int daysPerWeek = 7; // So ngay trong mot tuan
    [Min(1)] public int slotsPerDay = 5; // So ca trong mot ngay

    [Header("Quy tac xep lop")]
    [SerializeField]
    private List<Weekday> teachingDays = new() // Cac ngay co the day hoc
    { Weekday.Mon, Weekday.Tue, Weekday.Wed, Weekday.Thu, Weekday.Fri };

    [Min(1)] public int maxSessionsPerCoursePerWeek = 3; // So buoi hoc toi da cua mot mon trong 1 tuan

    [Header("Slot bi chan")]
    [SerializeField] private List<DaySlot> blockedSlots = new() { DaySlot.Evening }; // Cac ca khong the xep lich

    // Getter cho danh sach ngay day hoc
    public IReadOnlyList<Weekday> TeachingDays => teachingDays;

    // Getter cho danh sach ca bi chan
    public IReadOnlyList<DaySlot> BlockedSlots => blockedSlots;

#if UNITY_EDITOR
    // Kiem tra va chuan hoa gia tri trong editor
    private void OnValidate()
    {
        termsPerYear = Mathf.Max(1, termsPerYear); // It nhat 1 hoc ky
        weeksPerTerm = Mathf.Max(1, weeksPerTerm); // It nhat 1 tuan
        daysPerWeek = Mathf.Clamp(daysPerWeek, 1, 7); // Gioi han 1-7 ngay/tuan
        slotsPerDay = Mathf.Clamp(slotsPerDay, 1, 5); // Gioi han 1-5 ca/ngay
    }
#endif
}