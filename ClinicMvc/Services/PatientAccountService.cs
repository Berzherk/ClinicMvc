using ClinicMvc.Models;
using ClinicMvc.Repositories;

namespace ClinicMvc.Services;

/// <summary>
/// Ја координира самостојната регистрација на пациент - креира Patient и User запис
/// во исто барање и испраќа е-пошта за потврда. Сметката не може да се најави
/// додека не се потврди е-поштата.
/// </summary>
public class PatientAccountService : IPatientAccountService
{
    private readonly IPatientRepository  _patientRepository;
    private readonly IUserRepository     _userRepository;
    private readonly IPasswordHasher     _passwordHasher;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IEmailConfirmationService _emailConfirmationService;

    public PatientAccountService(
        IPatientRepository patientRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuditLogRepository auditLogRepository,
        IEmailConfirmationService emailConfirmationService)
    {
        _patientRepository  = patientRepository;
        _userRepository     = userRepository;
        _passwordHasher     = passwordHasher;
        _auditLogRepository = auditLogRepository;
        _emailConfirmationService = emailConfirmationService;
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> RegisterAsync(RegisterViewModel model)
    {
        var errors = new List<string>();

        if (await _userRepository.GetByUsernameAsync(model.Username) != null)
            errors.Add("Ова корисничко ime веќе постои.");

        if (await _userRepository.EmailExistsAsync(model.Email))
            errors.Add("Веќе постои сметка со оваа е-пошта адреса.");

        if (await _patientRepository.EmbgExistsAsync(model.Embg))
            errors.Add("ЕМБГ веќе постои во системот.");

        if (errors.Count > 0)
            return (false, errors);

        var patient = new Patient
        {
            FirstName = model.FirstName,
            LastName  = model.LastName,
            Embg      = model.Embg,
            Phone     = model.Phone,
            Email     = model.Email
        };
        var patientId = await _patientRepository.CreateAsync(patient, model.Username);

        var user = new User
        {
            Username       = model.Username,
            PasswordHash   = _passwordHasher.HashPassword(model.Password),
            Role           = "Patient",
            PatientId      = patientId,
            Email          = model.Email,
            EmailConfirmed = false,
            CreatedBy      = model.Username
        };
        var userId = await _userRepository.CreateAsync(user);
        user.Id = userId;

        await _auditLogRepository.LogAsync("CREATE", "Patient", patientId, model.Username,
            $"Самостојна регистрација на пациент {patient.FirstName} {patient.LastName}");
        await _auditLogRepository.LogAsync("CREATE", "User", userId, model.Username,
            $"Креирана пациентска сметка '{model.Username}'");

        await _emailConfirmationService.SendConfirmationEmailAsync(user);

        return (true, Enumerable.Empty<string>());
    }
}
