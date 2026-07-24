using ClinicMvc.Models;
using ClinicMvc.Repositories;

namespace ClinicMvc.Services;

/// <summary>
/// Бизнис логика за доктори - го координира креирањето на доктор
/// заедно со неговата корисничка сметка (два репозиториума во еден чекор),
/// и испраќа е-пошта за потврда (сметката е неактивна додека не се потврди).
/// </summary>
public class DoctorService : IDoctorService
{
    private readonly IDoctorRepository   _doctorRepository;
    private readonly IUserRepository     _userRepository;
    private readonly IPasswordHasher     _passwordHasher;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IEmailConfirmationService _emailConfirmationService;

    public DoctorService(
        IDoctorRepository doctorRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuditLogRepository auditLogRepository,
        IEmailConfirmationService emailConfirmationService)
    {
        _doctorRepository   = doctorRepository;
        _userRepository     = userRepository;
        _passwordHasher     = passwordHasher;
        _auditLogRepository = auditLogRepository;
        _emailConfirmationService = emailConfirmationService;
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> CreateDoctorWithAccountAsync(DoctorCreateViewModel model, string createdBy)
    {
        var errors = new List<string>();

        var existingUser = await _userRepository.GetByUsernameAsync(model.Username);
        if (existingUser != null)
            errors.Add("Ова корисничко ime веќе постои.");

        if (await _userRepository.EmailExistsAsync(model.Email))
            errors.Add("Веќе постои сметка со оваа е-пошта адреса.");

        if (errors.Count > 0)
            return (false, errors);

        var doctor = new Doctor
        {
            FirstName = model.FirstName,
            LastName  = model.LastName,
            Specialty = model.Specialty,
            Phone     = model.Phone,
            IsActive  = model.IsActive
        };
        var doctorId = await _doctorRepository.CreateAsync(doctor, createdBy);

        var passwordHash = _passwordHasher.HashPassword(model.Password);

        var user = new User
        {
            Username       = model.Username,
            PasswordHash   = passwordHash,
            Role           = "Doctor",
            DoctorId       = doctorId,
            Email          = model.Email,
            EmailConfirmed = false,
            CreatedBy      = createdBy
        };
        var userId = await _userRepository.CreateAsync(user);
        user.Id = userId;

        await _auditLogRepository.LogAsync("CREATE", "Doctor", doctorId, createdBy,
            $"Креиран доктор {doctor.FirstName} {doctor.LastName}");
        await _auditLogRepository.LogAsync("CREATE", "User", doctorId, createdBy,
            $"Креирана корисничка сметка '{model.Username}' за доктор {doctor.FirstName} {doctor.LastName}");

        // Испрати е-пошта за потврда - докторот не може да се најави додека не ја потврди.
        await _emailConfirmationService.SendConfirmationEmailAsync(user);

        return (true, errors);
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> UpdateDoctorAsync(Doctor doctor, string modifiedBy)
    {
        await _doctorRepository.UpdateAsync(doctor, modifiedBy);
        await _auditLogRepository.LogAsync("UPDATE", "Doctor", doctor.Id, modifiedBy,
            $"Изменет доктор {doctor.FirstName} {doctor.LastName}");

        return (true, Enumerable.Empty<string>());
    }

    public async Task DeleteDoctorAsync(int id, string modifiedBy)
    {
        await _doctorRepository.DeleteAsync(id, modifiedBy);
        await _auditLogRepository.LogAsync("DELETE", "Doctor", id, modifiedBy, "Soft delete на доктор");
    }
}
