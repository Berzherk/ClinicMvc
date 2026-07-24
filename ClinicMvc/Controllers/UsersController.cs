using ClinicMvc.Models;
using ClinicMvc.Repositories;
using ClinicMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Управување со кориснички сметки - достапно само за Administrator.
/// Овозможува преглед на сите сметки, ресетирање лозинка и бришење сметка.
/// </summary>
[Authorize(Roles = "Administrator")]
public class UsersController : Controller
{
    private readonly IUserRepository     _userRepository;
    private readonly IDoctorRepository   _doctorRepository;
    private readonly IPatientRepository  _patientRepository;
    private readonly IPasswordHasher     _passwordHasher;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUser;

    public UsersController(
        IUserRepository userRepository,
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        IPasswordHasher passwordHasher,
        IAuditLogRepository auditLogRepository,
        ICurrentUserService currentUser)
    {
        _userRepository      = userRepository;
        _doctorRepository    = doctorRepository;
        _patientRepository   = patientRepository;
        _passwordHasher      = passwordHasher;
        _auditLogRepository  = auditLogRepository;
        _currentUser         = currentUser;
    }

    private const int PageSize = 10;

    /// <summary>GET: /Users?page=2 - листа на кориснички сметки, странирана 10 по страница.</summary>
    public async Task<IActionResult> Index(int page = 1)
    {
        var validPage = page < 1 ? 1 : page;

        var users      = await _userRepository.GetPagedAsync(validPage, PageSize);
        var totalCount = await _userRepository.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        // Ги полниме поврзаните доктори/пациенти само за записите прикажани на тековната страница
        var allDoctors  = await _doctorRepository.GetAllAsync();
        var allPatients = await _patientRepository.GetAllAsync();
        foreach (var user in users)
        {
            if (user.DoctorId.HasValue)
            {
                user.Doctor = allDoctors.FirstOrDefault(d => d.Id == user.DoctorId.Value);
            }
            if (user.PatientId.HasValue)
            {
                user.Patient = allPatients.FirstOrDefault(p => p.Id == user.PatientId.Value);
            }
        }

        ViewBag.Pagination = new PaginationInfo
        {
            CurrentPage = validPage,
            TotalPages  = totalPages == 0 ? 1 : totalPages,
            TotalCount  = totalCount,
            PageSize    = PageSize
        };

        return View(users);
    }

    /// <summary>POST: /Users/ResetPassword - генерира нов BCrypt hash за корисникот.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        var user = await _userRepository.GetByIdAsync(model.UserId);
        if (user == null)
            return NotFound(new { success = false, errors = new[] { "Корисникот не постои." } });

        var newHash = _passwordHasher.HashPassword(model.NewPassword);
        await _userRepository.UpdatePasswordAsync(model.UserId, newHash, _currentUser.Username);

        await _auditLogRepository.LogAsync("UPDATE", "User", model.UserId, _currentUser.Username,
            $"Ресетирана лозинка за корисник '{user.Username}'");

        return Ok(new { success = true });
    }

    /// <summary>POST: /Users/Delete/5 - трајно ја брише корисничката сметка (не влијае на медицинската историја).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return NotFound(new { success = false, errors = new[] { "Корисникот не постои." } });

        await _userRepository.DeleteAsync(id);

        await _auditLogRepository.LogAsync("DELETE", "User", id, _currentUser.Username,
            $"Избришана корисничка сметка '{user.Username}'");

        return Ok(new { success = true });
    }
}
