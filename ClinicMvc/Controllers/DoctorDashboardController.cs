using ClinicMvc.Repositories;
using ClinicMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Dashboard за најавен доктор - прикажува само негови/нејзини податоци.
/// </summary>
[Authorize(Roles = "Doctor")]
public class DoctorDashboardController : Controller
{
    private readonly IDoctorRepository  _doctorRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly ICurrentUserService _currentUser;

    public DoctorDashboardController(
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        ICurrentUserService currentUser)
    {
        _doctorRepository  = doctorRepository;
        _patientRepository = patientRepository;
        _currentUser       = currentUser;
    }

    /// <summary>
    /// GET: /DoctorDashboard
    /// Прикажува: Добредојде порака, денешен распоред, пациенти доделени на докторот.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Ако корисникот некако нема поврзан DoctorId (грешка во податоците), одбиј пристап
        if (!_currentUser.DoctorId.HasValue)
            return Forbid();

        var doctorId = _currentUser.DoctorId.Value;

        var doctor = await _doctorRepository.GetByIdAsync(doctorId);
        if (doctor == null) return NotFound();

        var todaySchedule = await _doctorRepository.GetTodayScheduleAsync(doctorId);

        ViewBag.TodaySchedule = todaySchedule;
        return View(doctor);
    }
}
