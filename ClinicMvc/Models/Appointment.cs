using System.ComponentModel.DataAnnotations;

namespace ClinicMvc.Models;

/// <summary>
/// Модел кој ги претставува податоците за термин.
/// Одговара на табелата APPOINTMENTS во Firebird базата.
/// </summary>
public class Appointment
{
    /// <summary>Уникатен идентификатор - auto-increment во базата</summary>
    public int Id { get; set; }

    /// <summary>ID на докторот - странски клуч кон табелата DOCTORS</summary>
    [Display(Name = "Лекар")]
    public int DoctorId { get; set; }

    /// <summary>ID на пациентот - странски клуч кон табелата PATIENTS</summary>
    [Display(Name = "Пациент")]
    public int PatientId { get; set; }

    /// <summary>Датум на терминот - не смее да биде во минатото</summary>
    [Display(Name = "Датум")]
    [DataType(DataType.Date)]
    public DateTime AppointmentDate { get; set; }

    /// <summary>Време на терминот</summary>
    [Display(Name = "Време")]
    [DataType(DataType.Time)]
    public TimeSpan AppointmentTime { get; set; }

    /// <summary>
    /// Статус на терминот.
    /// Дозволени вредности: Zakazan, Vo tek, Zavrsen, Otkazen
    /// (дефинирани со CHECK constraint во базата)
    /// </summary>
    [Display(Name = "Статус")]
    public string Status { get; set; } = "Zakazan";

    /// <summary>Дополнителни белешки или забелешки за терминот</summary>
    [Display(Name = "Белешки")]
    public string? Notes { get; set; }

    // Пополнети преку JOIN во репозиторито - само за приказ, не се зачувуваат во базата
    /// <summary>Целосно ime на докторот (FirstName + LastName)</summary>
    public string? DoctorName { get; set; }

    /// <summary>Целосно ime на пациентот (FirstName + LastName)</summary>
    public string? PatientName { get; set; }

    /// <summary>Специјалност на докторот</summary>
    public string? DoctorSpecialty { get; set; }
}
