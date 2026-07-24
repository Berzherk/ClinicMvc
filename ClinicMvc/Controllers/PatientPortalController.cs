using ClinicMvc.Models;
using ClinicMvc.Repositories;
using ClinicMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClinicMvc.Controllers;

/// <summary>
/// Портал за пациенти - достапен само за улогата Patient. Пациентот гледа и управува
/// исклучиво со сопствените податоци (профил, термини) - никогаш со туѓи.
/// </summary>
[Authorize(Roles = "Patient")]
public class PatientPortalController : Controller
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorRepository      _doctorRepository;
    private readonly IPatientRepository     _patientRepository;
    private readonly IAppointmentService     _appointmentService;
    private readonly ICurrentUserService     _currentUser;
    private readonly IAuditLogRepository     _auditLogRepository;

    public PatientPortalController(
        IAppointmentRepository appointmentRepository,
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        IAppointmentService appointmentService,
        ICurrentUserService currentUser,
        IAuditLogRepository auditLogRepository)
    {
        _appointmentRepository = appointmentRepository;
        _doctorRepository      = doctorRepository;
        _patientRepository     = patientRepository;
        _appointmentService    = appointmentService;
        _currentUser           = currentUser;
        _auditLogRepository    = auditLogRepository;
    }

    /// <summary>Го враќа PatientId на најавениот пациент, или null ако сметката нема поврзан пациент.</summary>
    private int? MyPatientId => _currentUser.PatientId;

    /// <summary>
    /// GET: /PatientPortal - Dashboard: идни термини + историја на термини.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        if (!MyPatientId.HasValue) return Forbid();

        var vm = new PatientDashboardViewModel
        {
            UpcomingAppointments = await _appointmentRepository.GetPatientAppointmentsAsync(MyPatientId.Value, upcomingOnly: true),
            AppointmentHistory   = await _appointmentRepository.GetPatientAppointmentsAsync(MyPatientId.Value, upcomingOnly: false)
        };

        return View(vm);
    }

    /// <summary>
    /// GET: /PatientPortal/AvailableSlots - страница за прелистување на слободни термини
    /// со филтри по специјалност/лекар/датум. Резервацијата се прави преку AJAX (Book).
    /// </summary>
    public async Task<IActionResult> AvailableSlots()
    {
        var specialties = await _doctorRepository.GetSpecialtiesAsync();
        var activeDoctors = (await _doctorRepository.GetAllAsync()).Where(d => d.IsActive);

        var vm = new AvailableSlotsViewModel
        {
            Filter = new FreeSlotsFilter(),
            Specialties = specialties.Select(s => new SelectListItem(s, s)).ToList(),
            Doctors = activeDoctors.Select(d => new SelectListItem(d.FullName, d.Id.ToString())).ToList()
        };

        return View(vm);
    }

    /// <summary>GET: /PatientPortal/LoadSlots - AJAX, ги враќа слободните слотови според филтрите.</summary>
    [HttpGet]
    public async Task<IActionResult> LoadSlots(FreeSlotsFilter filter)
    {
        var slots = await _appointmentRepository.SearchFreeSlotsAsync(filter);
        return PartialView("_AvailableSlotsTable", slots);
    }

    /// <summary>
    /// POST: /PatientPortal/Book - резервира слободен слот за најавениот пациент.
    /// Клиентот никогаш не испраќа датум/време рачно - само ID на слотот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int slotId)
    {
        if (!MyPatientId.HasValue)
            return BadRequest(new { success = false, errors = new[] { "Вашата сметка нема поврзан пациентски профил." } });

        var (success, errors) = await _appointmentService.BookAppointmentAsync(slotId, MyPatientId.Value, _currentUser.Username);
        if (!success)
            return BadRequest(new { success = false, errors });

        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /PatientPortal/Cancel - пациентот го откажува СВОЈОТ термин.
    /// Прво се проверува дека терминот навистина е негов - друг пациент не смее да го откаже.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        if (!MyPatientId.HasValue)
            return Forbid();

        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return NotFound(new { success = false, errors = new[] { "Терминот не постои." } });

        if (appointment.PatientId != MyPatientId.Value)
            return Forbid();

        var (success, errors) = await _appointmentService.CancelAppointmentAsync(id, _currentUser.Username, "Patient");
        if (!success)
            return BadRequest(new { success = false, errors });

        return Ok(new { success = true });
    }

    /// <summary>GET: /PatientPortal/Profile - преглед/уредување на сопствениот профил.</summary>
    public async Task<IActionResult> Profile()
    {
        if (!MyPatientId.HasValue) return Forbid();

        var patient = await _patientRepository.GetByIdAsync(MyPatientId.Value);
        if (patient == null) return NotFound();

        var vm = new PatientProfileViewModel
        {
            Id        = patient.Id,
            FirstName = patient.FirstName,
            LastName  = patient.LastName,
            Embg      = patient.Embg,
            Phone     = patient.Phone,
            Email     = patient.Email
        };

        return View(vm);
    }

    /// <summary>POST: /PatientPortal/Profile - го зачувува измененото сопствено профил.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(PatientProfileViewModel model)
    {
        if (!MyPatientId.HasValue || model.Id != MyPatientId.Value)
            return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        if (await _patientRepository.EmbgExistsAsync(model.Embg, model.Id))
        {
            ModelState.AddModelError(nameof(model.Embg), "ЕМБГ веќе постои во системот.");
            return View(model);
        }

        var patient = new Patient
        {
            Id        = model.Id,
            FirstName = model.FirstName,
            LastName  = model.LastName,
            Embg      = model.Embg,
            Phone     = model.Phone,
            Email     = model.Email
        };

        await _patientRepository.UpdateAsync(patient, _currentUser.Username);
        await _auditLogRepository.LogAsync("UPDATE", "Patient", model.Id, _currentUser.Username,
            "Пациентот го измени сопствениот профил");

        ViewBag.Saved = true;
        return View(model);
    }
}
