using ClinicMvc.Models;
using ClinicMvc.Repositories;
using ClinicMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за управување со доктори.
/// [Authorize(Roles = "Administrator")] на класата значи ЦЕЛИОТ контролер
/// е достапен само за администратори - докторите не смеат да додаваат/бришат доктори.
/// </summary>
[Authorize(Roles = "Administrator")]
public class DoctorsController : Controller
{
    private readonly IDoctorRepository    _doctorRepository;
    private readonly IUserRepository      _userRepository;
    private readonly IPasswordHasher      _passwordHasher;
    private readonly IAuditLogRepository  _auditLogRepository;
    private readonly ICurrentUserService  _currentUser;

    public DoctorsController(
        IDoctorRepository doctorRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuditLogRepository auditLogRepository,
        ICurrentUserService currentUser)
    {
        _doctorRepository   = doctorRepository;
        _userRepository     = userRepository;
        _passwordHasher     = passwordHasher;
        _auditLogRepository = auditLogRepository;
        _currentUser        = currentUser;
    }

    /// <summary>GET: /Doctors - листа со Ime, Презиме, Специјалност, Акции.</summary>
    public async Task<IActionResult> Index()
    {
        var doctors = await _doctorRepository.GetAllAsync();
        return View(doctors);
    }

    /// <summary>
    /// GET: /Doctors/Details/5
    /// Детали за доктор и неговиот денешен распоред.
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var doctor = await _doctorRepository.GetByIdAsync(id);
        if (doctor == null) return NotFound();

        var todaySchedule = await _doctorRepository.GetTodayScheduleAsync(id);
        ViewBag.TodaySchedule = todaySchedule;

        return View(doctor);
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
    /// Креира доктор И неговата корисничка сметка во исто барање.
    /// Само Administrator може да ја повика оваа акција (класата е [Authorize(Roles = "Administrator")]) -
    /// докторите никогаш не можат самите да се регистрираат.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DoctorCreateViewModel model)
    {
        // Провери дали корисничкото ime веќе е зафатено
        var existingUser = await _userRepository.GetByUsernameAsync(model.Username);
        if (existingUser != null)
            ModelState.AddModelError(nameof(model.Username), "Ова корисничко ime веќе постои.");

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        // 1) Прво го креираме докторот - ни треба неговиот ID за да го поврземе со корисникот
        var doctor = new Doctor
        {
            FirstName = model.FirstName,
            LastName  = model.LastName,
            Specialty = model.Specialty,
            Phone     = model.Phone,
            IsActive  = model.IsActive
        };
        var doctorId = await _doctorRepository.CreateAsync(doctor, _currentUser.Username);

        // 2) Ја хешираме лозинката со постоечкиот BCryptPasswordHasher - НИКОГАШ plain-text
        var passwordHash = _passwordHasher.HashPassword(model.Password);

        // 3) Ја креираме корисничката сметка, поврзана со новиот доктор преку DoctorId,
        //    со улога "Doctor" (иста улога која веќе ја проверува [Authorize] низ апликацијата)
        var user = new User
        {
            Username     = model.Username,
            PasswordHash = passwordHash,
            Role         = "Doctor",
            DoctorId     = doctorId,
            CreatedBy    = _currentUser.Username
        };
        await _userRepository.CreateAsync(user);

        // Логирај ги двете акции одделно за јасна историја во AuditLogs
        await _auditLogRepository.LogAsync("CREATE", "Doctor", doctorId, _currentUser.Username,
            $"Креиран доктор {doctor.FirstName} {doctor.LastName}");
        await _auditLogRepository.LogAsync("CREATE", "User", doctorId, _currentUser.Username,
            $"Креирана корисничка сметка '{model.Username}' за доктор {doctor.FirstName} {doctor.LastName}");

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

        await _doctorRepository.UpdateAsync(doctor, _currentUser.Username);

        await _auditLogRepository.LogAsync("UPDATE", "Doctor", doctor.Id, _currentUser.Username,
            $"Изменет доктор {doctor.FirstName} {doctor.LastName}");

        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Doctors/Delete/5
    /// SOFT DELETE - записот не се брише физички.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _doctorRepository.DeleteAsync(id, _currentUser.Username);

        await _auditLogRepository.LogAsync("DELETE", "Doctor", id, _currentUser.Username,
            "Soft delete на доктор");

        return Ok(new { success = true });
    }
}
