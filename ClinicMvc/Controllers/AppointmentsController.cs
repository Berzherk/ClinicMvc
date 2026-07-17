using ClinicMvc.Models;
using ClinicMvc.Repositories;
using ClinicMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за управување со термини.
/// Ги повикува Services слојот за бизнис логика (валидации, конфликти, извоз) -
/// самиот контролер само ги обликува HTTP одговорите.
/// </summary>
[Authorize(Roles = "Administrator,Doctor")]
public class AppointmentsController : Controller
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorRepository      _doctorRepository;
    private readonly IPatientRepository     _patientRepository;
    private readonly IAppointmentService    _appointmentService;
    private readonly IExportService         _exportService;
    private readonly ICurrentUserService    _currentUser;

    private static readonly string[] StatusOptions =
        { "Zakazan", "Vo tek", "Zavrsen", "Otkazen" };

    public AppointmentsController(
        IAppointmentRepository appointmentRepository,
        IDoctorRepository      doctorRepository,
        IPatientRepository     patientRepository,
        IAppointmentService    appointmentService,
        IExportService         exportService,
        ICurrentUserService    currentUser)
    {
        _appointmentRepository = appointmentRepository;
        _doctorRepository      = doctorRepository;
        _patientRepository     = patientRepository;
        _appointmentService    = appointmentService;
        _exportService         = exportService;
        _currentUser           = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        var specialties  = await _doctorRepository.GetSpecialtiesAsync();
        var activeDoctors = (await _doctorRepository.GetAllAsync()).Where(d => d.IsActive);

        var vm = new AppointmentIndexViewModel
        {
            Filter      = new AppointmentFilter(),
            Specialties = specialties.Select(s => new SelectListItem(s, s)).ToList(),
            Doctors     = activeDoctors.Select(d => new SelectListItem(d.FullName, d.Id.ToString())).ToList()
        };

        return View(vm);
    }

    // Забелешка: LoadTable/LoadPagination/LoadStatistics НЕ ја ограничуваат листата
    // само на сопствениот доктор - секој логиран доктор може да ги гледа и филтрира
    // термините на сите доктори (потребно за филтерот "Ime на лекар" да работи correctно).
    // Ограничувањето по сопственик останува само на акциите кои МЕНУВААТ податоци
    // (Create/Edit/StartExam/FinishExam) - таму доктор сепак не смее да допира термини
    // на друг доктор.

    [HttpGet]
    public async Task<IActionResult> LoadTable(AppointmentFilter filter)
    {
        var appointments = await _appointmentRepository.SearchAsync(filter);
        return PartialView("_AppointmentsTable", appointments);
    }

    [HttpGet]
    public async Task<IActionResult> LoadPagination(AppointmentFilter filter)
    {
        var totalCount = await _appointmentRepository.CountAsync(filter);
        var currentPage = filter.Page < 1 ? 1 : filter.Page;
        var totalPages  = (int)Math.Ceiling(totalCount / (double)AppointmentFilter.PageSize);

        var vm = new PaginationInfo
        {
            CurrentPage = currentPage,
            TotalPages  = totalPages,
            TotalCount  = totalCount,
            PageSize    = AppointmentFilter.PageSize
        };

        return PartialView("_Pagination", vm);
    }

    [HttpGet]
    public async Task<IActionResult> LoadStatistics(AppointmentFilter filter)
    {
        var stats = await _appointmentRepository.GetStatisticsAsync(filter);
        return PartialView("_Statistics", stats);
    }

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null) return NotFound();

        // Прегледот на детали е дозволен за сите - ограничувањето по сопственик
        // е само за менување (Edit/Delete/StartExam/FinishExam подолу).
        return Json(appointment);
    }

    [HttpGet]
    public async Task<IActionResult> GetDropdowns()
    {
        var allDoctors = await _doctorRepository.GetAllAsync();
        var patients   = await _patientRepository.GetAllAsync();
        var activeDoctors = allDoctors.Where(d => d.IsActive);

        if (_currentUser.IsDoctor && _currentUser.DoctorId.HasValue)
        {
            activeDoctors = activeDoctors.Where(d => d.Id == _currentUser.DoctorId.Value);
        }

        return Json(new
        {
            doctors  = activeDoctors.Select(d => new { d.Id, name = d.FullName }),
            patients = patients.Select(p => new { p.Id, name = $"{p.FirstName} {p.LastName}" }),
            statuses = StatusOptions
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Appointment appointment)
    {
        if (_currentUser.IsDoctor && appointment.DoctorId != _currentUser.DoctorId)
            return Forbid();

        var (success, _, errors) = await _appointmentService.CreateAppointmentAsync(appointment, _currentUser.Username);
        if (!success)
            return BadRequest(new { success = false, errors });

        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Appointment appointment)
    {
        if (_currentUser.IsDoctor && appointment.DoctorId != _currentUser.DoctorId)
            return Forbid();

        var (success, errors) = await _appointmentService.UpdateAppointmentAsync(appointment, _currentUser.Username);
        if (!success)
            return BadRequest(new { success = false, errors });

        return Ok(new { success = true });
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _appointmentService.DeleteAppointmentAsync(id, _currentUser.Username);
        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartExam(int id)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return NotFound(new { success = false, errors = new[] { "Терминот не постои." } });

        if (_currentUser.IsDoctor && appointment.DoctorId != _currentUser.DoctorId)
            return Forbid();

        var (success, errors) = await _appointmentService.StartExamAsync(id, _currentUser.Username);
        if (!success)
            return BadRequest(new { success = false, errors });

        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinishExam(int id, string? notes)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return NotFound(new { success = false, errors = new[] { "Терминот не постои." } });

        if (_currentUser.IsDoctor && appointment.DoctorId != _currentUser.DoctorId)
            return Forbid();

        var (success, errors) = await _appointmentService.FinishExamAsync(id, notes, _currentUser.Username);
        if (!success)
            return BadRequest(new { success = false, errors });

        return Ok(new { success = true });
    }

    /// <summary>
    /// GET: /Appointments/ExportExcel
    /// Ги извезува термините кои одговараат на тековните филтри во Excel датотека.
    /// Не се применува пагинација - извозот ги содржи сите филтрирани записи.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportExcel(AppointmentFilter filter)
    {
        filter.Page = 1;

        var appointments = await GetAllFilteredAsync(filter);
        var fileBytes = _exportService.ExportAppointmentsToExcel(appointments);

        return File(fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"termini_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    /// <summary>GET: /Appointments/ExportPdf - извоз на филтрираните термини во PDF.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportPdf(AppointmentFilter filter)
    {
        filter.Page = 1;

        var appointments = await GetAllFilteredAsync(filter);
        var fileBytes = _exportService.ExportAppointmentsToPdf(appointments);

        return File(fileBytes, "application/pdf", $"termini_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    /// <summary>
    /// Ги вчитува СИТЕ записи (без пагинација) кои одговараат на филтрите - за извоз.
    /// Го користи истиот SearchAsync, но со голем PageSize за да ги земе сите одеднаш.
    /// </summary>
    private async Task<IEnumerable<Appointment>> GetAllFilteredAsync(AppointmentFilter filter)
    {
        var totalCount = await _appointmentRepository.CountAsync(filter);
        var exportFilter = new AppointmentFilter
        {
            PatientFirstName   = filter.PatientFirstName,
            PatientLastName    = filter.PatientLastName,
            PatientEmbg        = filter.PatientEmbg,
            DoctorName         = filter.DoctorName,
            Specialty          = filter.Specialty,
            Date               = filter.Date,
            SortBy             = filter.SortBy,
            SortDirection      = filter.SortDirection,
            RestrictToDoctorId = filter.RestrictToDoctorId,
            Page               = 1
        };

        // SearchAsync секогаш странира според AppointmentFilter.PageSize (10) - за извоз
        // го читаме сето со повторни повици наместо да менуваме постојана константа.
        var all = new List<Appointment>();
        var pages = (int)Math.Ceiling(totalCount / (double)AppointmentFilter.PageSize);
        for (var page = 1; page <= Math.Max(pages, 1); page++)
        {
            exportFilter.Page = page;
            var pageItems = await _appointmentRepository.SearchAsync(exportFilter);
            all.AddRange(pageItems);
        }

        return all;
    }
}
