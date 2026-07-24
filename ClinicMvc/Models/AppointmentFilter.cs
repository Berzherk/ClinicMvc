using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClinicMvc.Models;

/// <summary>
/// Филтри, сортирање и пагинација за пребарување на термини.
/// Се врзува директно од query string-от при AJAX повиците (GET).
/// </summary>
public class AppointmentFilter
{
    // ── Филтри за пребарување ──
    /// <summary>Ime на пациент - делумно пребарување (LIKE)</summary>
    public string? PatientFirstName { get; set; }

    /// <summary>Презиме на пациент - делумно пребарување (LIKE)</summary>
    public string? PatientLastName { get; set; }

    /// <summary>ЕМБГ на пациент - делумно пребарување (LIKE)</summary>
    public string? PatientEmbg { get; set; }

    /// <summary>Ime и/или презиме на лекар - делумно пребарување (LIKE)</summary>
    public string? DoctorName { get; set; }

    /// <summary>Специјалност на лекар - точно совпаѓање (dropdown)</summary>
    public string? Specialty { get; set; }

    /// <summary>Конкретен датум на термин</summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// НЕ доаѓа од корисничкиот интерфејс - контролерот го поставува ова автоматски
    /// кога најавениот корисник е Doctor, за да гледа само свои термини.
    /// Администраторите го оставаат ова null (гледаат сè).
    /// </summary>
    public int? RestrictToDoctorId { get; set; }

    // ── Сортирање ──
    /// <summary>Колона по која се сортира: Date, Time, Patient, Doctor, Status</summary>
    public string SortBy { get; set; } = "Date";

    /// <summary>Насока на сортирање: asc или desc</summary>
    public string SortDirection { get; set; } = "asc";

    // ── Пагинација ──
    /// <summary>Тековна страница (започнува од 1)</summary>
    public int Page { get; set; } = 1;

    /// <summary>Број на записи по страница - фиксно 10 според барањата</summary>
    public const int PageSize = 10;
}

/// <summary>
/// Статистички податоци кои се прикажуваат над табелата со термини.
/// Се пресметуваат според тековно активните филтри.
/// </summary>
public class AppointmentStatistics
{
    /// <summary>Вкупен број на термини кои одговараат на филтрите</summary>
    public int Total { get; set; }

    /// <summary>Број на слободни термини (Status = Free)</summary>
    public int Free { get; set; }

    /// <summary>Број на резервирани термини (Status = Booked)</summary>
    public int Booked { get; set; }

    /// <summary>Број на термини во статус "Completed"</summary>
    public int Completed { get; set; }

    /// <summary>Број на термини во статус "Cancelled"</summary>
    public int Cancelled { get; set; }
}

/// <summary>
/// Информации за пагинација - тековна страница, вкупен број на страници и записи.
/// Се користи за прикажување на Previous/Next контролите.
/// </summary>
public class PaginationInfo
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// ViewModel за главната страница со термини (dashboard).
/// Содржи филтри и dropdown опции потребни за почетниот приказ.
/// Самата табела, статистиката и пагинацијата се вчитуваат посебно преку AJAX.
/// </summary>
public class AppointmentIndexViewModel
{
    public AppointmentFilter Filter { get; set; } = new();
    public List<SelectListItem> Specialties { get; set; } = new();

    /// <summary>Активни доктори - се користи за dropdown-от во панелот за слободни термини.</summary>
    public List<SelectListItem> Doctors { get; set; } = new();
}
