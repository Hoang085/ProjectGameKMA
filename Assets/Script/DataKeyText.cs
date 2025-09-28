public static class DataKeyText
{
    public const string openText = "Chào em, hôm nay em đến để bắt đầu buổi học đúng không?"; // openText
    public const string text1 = "Em đã điểm danh buổi hôm nay, chúng ta bắt đầu vào học nhé"; // confirmText
    public const string text2 = "Không phải giờ môn này, quay lại đúng ca nhé."; // wrongTimeText
    public const string text3 = "Đang học..."; // learningText 
    public const string text4 = "Môn này không có trong SemesterConfig hiện tại: "; // noSubjectText
    public const string text5 = "Em đã nghỉ quá số buổi quy định cho phép"; // exceedAbsenceText
    public const string text6 = "Ca hiện tại chưa khởi tạo."; // noCurrentSlotText
    public const string text7 = "Không đúng ca học của môn này đang là: "; // wrongSubjectText
    public const string text8 = "Không tìm thấy khung giờ điểm danh cho ca này."; // noTimeWindowText
    public const string text9 = "Đã quá giờ điểm danh vào học, em không thể học môn này ngày hôm nay lần sau hãy đến đúng giờ vào nhé";

    public static string VN_Weekday(Weekday w)
    {
        switch (w)
        {
            case Weekday.Mon: return "Thứ 2";
            case Weekday.Tue: return "Thứ 3";
            case Weekday.Wed: return "Thứ 4";
            case Weekday.Thu: return "Thứ 5";
            case Weekday.Fri: return "Thứ 6";
            case Weekday.Sat: return "Thứ 7";
            case Weekday.Sun: return "Chủ nhật";
            default: return "Thứ ?";
        }
    }

    public static string FormatHM(int minuteOfDay)
    {
        if (minuteOfDay < 0) return "??:??";
        int h = minuteOfDay / 60;
        int m = minuteOfDay % 60;
        return $"{h:00}:{m:00}";
    }

    public static DaySlot SlotFromIndex1Based(int idx)
    {
        switch (idx)
        {
            case 1: return DaySlot.MorningA;
            case 2: return DaySlot.MorningB;
            case 3: return DaySlot.AfternoonA;
            case 4: return DaySlot.AfternoonB;
            case 5: return DaySlot.Evening;
            default: return DaySlot.MorningA;
        }
    }

    // Lấy phút bắt đầu của từng ca từ AttendanceManager
    public static int TryGetSlotStartMinute(DaySlot slot)
    {
        var att = AttendanceManager.Instance;
        if (!att) return -1;

        switch (slot)
        {
            case DaySlot.MorningA: return att.morningAStart;
            case DaySlot.MorningB: return att.morningBStart;
            case DaySlot.AfternoonA: return att.afternoonAStart;
            case DaySlot.AfternoonB: return att.afternoonBStart;
            case DaySlot.Evening: return att.eveningStart;
            default: return -1;
        }
    }
}
