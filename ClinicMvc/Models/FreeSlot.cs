namespace ClinicMvc.Models;

/// <summary>
/// Претставува еден слободен термин за конкретен доктор на конкретен датум.
/// Не е зачуван во база - се пресметува динамички при секое барање.
/// </summary>
public class FreeSlot
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? DoctorSpecialty { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
}

/// <summary>
/// Филтри за пребарување на слободни термини - Лекар, Датум, Специјалност.
/// </summary>
public class FreeSlotsFilter
{
    public int? DoctorId { get; set; }
    public string? Specialty { get; set; }
    public DateTime? Date { get; set; }
}
