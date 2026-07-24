namespace ClinicMvc.Services;

/// <summary>
/// Дефинира го стандардното работно време и должината на еден термински слот -
/// истите вредности што порано ги користеше динамичкото пресметување на слободни
/// термини (08:00-16:00, слотови од по 30 минути). Термински слотови сега пак
/// мора да паѓаат точно на оваа мрежа - администраторот/докторот повеќе НЕ може
/// рачно да внесе произволно време (пр. 10:07).
/// </summary>
public static class SlotSchedule
{
    public static readonly TimeSpan WorkDayStart = new(8, 0, 0);
    public static readonly TimeSpan WorkDayEnd   = new(16, 0, 0);
    public static readonly TimeSpan SlotDuration = TimeSpan.FromMinutes(30);

    /// <summary>Ги враќа сите дозволени времиња за термин во текот на еден работен ден (08:00, 08:30, ..., 15:30).</summary>
    public static IEnumerable<TimeSpan> GetAllSlotTimes()
    {
        for (var time = WorkDayStart; time < WorkDayEnd; time += SlotDuration)
        {
            yield return time;
        }
    }

    /// <summary>Дали даденото време точно паѓа на дозволената 30-минутна мрежа во работното време.</summary>
    public static bool IsValidSlotTime(TimeSpan time) =>
        time >= WorkDayStart && time < WorkDayEnd &&
        (time - WorkDayStart).Ticks % SlotDuration.Ticks == 0;
}
