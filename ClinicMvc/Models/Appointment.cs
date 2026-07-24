using System.ComponentModel.DataAnnotations;

namespace ClinicMvc.Models;

/// <summary>
/// Модел кој ги претставува податоците за термин / термински слот.
/// Одговара на табелата APPOINTMENTS во Firebird базата.
///
/// НАПОМЕНА (нов модел на закажување): термините повеќе не се внесуваат рачно.
/// Администраторот (или докторот) прво креира "слот" - термин со Status = Free
/// и PatientId = NULL. Пациентот подоцна го резервира слотот, при што PatientId
/// се поставува, а статусот станува Booked.
/// </summary>
public class Appointment
{
    public int Id { get; set; }

    [Display(Name = "Лекар")]
    public int DoctorId { get; set; }

    /// <summary>
    /// NULL додека терминот е слободен слот (Status = Free).
    /// Се поставува во моментот кога пациент го резервира терминот.
    /// </summary>
    [Display(Name = "Пациент")]
    public int? PatientId { get; set; }

    [Display(Name = "Датум")]
    [DataType(DataType.Date)]
    public DateTime AppointmentDate { get; set; }

    [Display(Name = "Време")]
    [DataType(DataType.Time)]
    public TimeSpan AppointmentTime { get; set; }

    /// <summary>Дозволени вредности: Free, Booked, Completed, Cancelled (CHECK constraint)</summary>
    [Display(Name = "Статус")]
    public string Status { get; set; } = AppointmentStatus.Free;

    [Display(Name = "Белешки")]
    public string? Notes { get; set; }

    /// <summary>Soft delete - true значи терминот е "избришан" но записот сепак постои во базата</summary>
    public bool IsDeleted { get; set; } = false;

    // Audit полиња
    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public string? ModifiedBy { get; set; }

    // Пополнети преку JOIN во репозиторито - само за приказ, не се зачувуваат во базата
    public string? DoctorName { get; set; }
    public string? PatientName { get; set; }
    public string? PatientEmail { get; set; }
    public string? DoctorSpecialty { get; set; }
}

/// <summary>Статусни константи за термини - ги заменуваат старите Zakazan/Vo tek/Zavrsen/Otkazen.</summary>
public static class AppointmentStatus
{
    public const string Free = "Free";
    public const string Booked = "Booked";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Free, Booked, Completed, Cancelled };
}
