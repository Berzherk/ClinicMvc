using ClinicMvc.Models;
using ClinicMvc.Repositories;
using ClinicMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за управување со доктори.
/// [Authorize(Roles = "Administrator")] на класата значи целиот контролер
/// е достапен само за администратори - докторите не смеат да додаваат/бришат доктори.
/// </summary>
[Authorize(Roles = "Administrator")]
public class DoctorsController : Controller
{
    private readonly IDoctorRepository    _doctorRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorService       _doctorService;
    private readonly ICurrentUserService  _currentUser;

    public DoctorsController(
        IDoctorRepository doctorRepository,
        IAppointmentRepository appointmentRepository,
        IDoctorService doctorService,
        ICurrentUserService currentUser)
    {
        _doctorRepository      = doctorRepository;
        _appointmentRepository = appointmentRepository;
        _doctorService          = doctorService;
        _currentUser            = currentUser;
    }

    private const int PageSize = 10;

    /// <summary>GET: /Doctors?page=2 - листа со Ime, Презиме, Специјалност, Акции, странирана 10 по страница.</summary>
    public async Task<IActionResult> Index(int page = 1)
    {
        var validPage = page < 1 ? 1 : page;

        var doctors     = await _doctorRepository.GetPagedAsync(validPage, PageSize);
        var totalCount  = await _doctorRepository.CountAsync();
        var totalPages  = (int)Math.Ceiling(totalCount / (double)PageSize);

        ViewBag.Pagination = new PaginationInfo
        {
            CurrentPage = validPage,
            TotalPages  = totalPages == 0 ? 1 : totalPages,
            TotalCount  = totalCount,
            PageSize    = PageSize
        };

        return View(doctors);
    }

    /// <summary>GET: /Doctors/Details/5 - детали за доктор и неговиот денешен распоред.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var doctor = await _doctorRepository.GetByIdAsync(id);
        if (doctor == null) return NotFound();

        var todaySchedule = await _doctorRepository.GetTodayScheduleAsync(id);
        ViewBag.TodaySchedule = todaySchedule;

        return View(doctor);
    }

    /// <summary>
    /// GET: /Doctors/DailySchedule/5?date=2026-07-20
    /// Целосен распоред на докторот за конкретен избран датум (не само денес).
    /// </summary>
    public async Task<IActionResult> DailySchedule(int id, DateTime? date)
    {
        var doctor = await _doctorRepository.GetByIdAsync(id);
        if (doctor == null) return NotFound();

        var selectedDate = date ?? DateTime.Today;

        var filter = new AppointmentFilter
        {
            RestrictToDoctorId = id,
            Date = selectedDate,
            SortBy = "Time",
            SortDirection = "asc",
            Page = 1
        };

        var appointments = await _appointmentRepository.SearchAsync(filter);

        ViewBag.SelectedDate = selectedDate;
        ViewBag.Doctor = doctor;

        return View(appointments);
    }

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
    {
        var doctor = await _doctorRepository.GetByIdAsync(id);
        if (doctor == null) return NotFound();
        return Json(doctor);
    }

    /// <summary>
    /// POST: /Doctors/Create
    /// Креира доктор и неговата корисничка сметка во исто барање, преку IDoctorService.
    /// Само Administrator може да ја повика оваа акција - докторите никогаш не можат самите да се регистрираат.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DoctorCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var modelErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors = modelErrors });
        }

        var (success, errors) = await _doctorService.CreateDoctorWithAccountAsync(model, _currentUser.Username);
        if (!success)
            return BadRequest(new { success = false, errors });

        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Doctor doctor)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        await _doctorService.UpdateDoctorAsync(doctor, _currentUser.Username);
        return Ok(new { success = true });
    }

    /// <summary>POST: /Doctors/Delete/5 - soft delete, записот не се брише физички.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _doctorService.DeleteDoctorAsync(id, _currentUser.Username);
        return Ok(new { success = true });
    }
}
