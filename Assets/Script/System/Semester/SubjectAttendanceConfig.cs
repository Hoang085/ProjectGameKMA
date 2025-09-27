using UnityEngine;

[System.Serializable]
public struct SlotCheckInRule
{
    public DaySlot slot;               // MorningA, MorningB, AfternoonA, AfternoonB
    [Min(0)] public int startOffsetMinutes; // offset từ đầu ca
    [Min(0)] public int endOffsetMinutes;   // end-exclusive
}

[CreateAssetMenu(fileName = "SubjectAttendanceConfig", menuName = "Configs/AttendanceBySlot")]
public class SubjectAttendanceConfig : ScriptableObject
{
    [Header("Quy định khung giờ điểm danh cho TỪNG CA (trừ ca tối)")]
    public SlotCheckInRule[] rules;

    /// <summary>
    /// Lấy cửa sổ điểm danh tuyệt đối (minute-of-day) cho 1 ca.
    /// Trả false nếu không có rule (ví dụ ca tối).
    /// </summary>
    public bool TryGetWindow(DaySlot slot, int slotStartMinute,
                             out int absStart, out int absEnd)
    {
        // Không cho điểm danh ca tối
        if (slot == DaySlot.Evening)
        {
            absStart = absEnd = 0;
            return false;
        }

        if (rules != null)
        {
            foreach (var r in rules)
            {
                if (r.slot == slot)
                {
                    absStart = slotStartMinute + r.startOffsetMinutes;
                    absEnd = slotStartMinute + r.endOffsetMinutes;
                    return absEnd > absStart;
                }
            }
        }

        absStart = absEnd = 0;
        return false;
    }
}
