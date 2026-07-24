namespace ClinicMvc.Models;

/// <summary>
/// Претставува еден слободен термински слот за конкретен доктор на конкретен датум.
/// Ова е проекција на ред од APPOINTMENTS со Status = Free (слотовите СЕ зачувани
/// во базата - ги креира администраторот/докторот однапред, не се пресметуваат динамички).
/// </summary>
public class FreeSlot
{
    /// <summary>ID на редот во APPOINTMENTS - потребен за резервација (Book).</summary>
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? DoctorSpecialty { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
}

/// <summary>
/// Филтри за пребарување на слободни термини - Лекар, Датум, Специјалност.
/// Ги користат и админ/докторскиот панел за слободни термини и пациентскиот
/// панел за прелистување и резервирање термини.
/// </summary>
public class FreeSlotsFilter
{
    public int? DoctorId { get; set; }
    public string? Specialty { get; set; }
    public DateTime? Date { get; set; }
}

/// <summary>ViewModel за креирање нов термински слот (Administrator, опционо Doctor).</summary>
public class CreateSlotViewModel
{
    public int DoctorId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
}
