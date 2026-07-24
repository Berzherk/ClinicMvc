using System.Security.Claims;

namespace ClinicMvc.Services;

/// <summary>
/// Имплементација на ICurrentUserService - ги чита податоците од
/// IHttpContextAccessor.HttpContext.User (Claims поставени при најава).
/// Регистриран е како Scoped бидејќи зависи од тековното HTTP барање.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string Username => User?.Identity?.Name ?? "system";

    public string? Role => User?.FindFirst(ClaimTypes.Role)?.Value;

    public int? DoctorId
    {
        get
        {
            var value = User?.FindFirst("DoctorId")?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public int? PatientId
    {
        get
        {
            var value = User?.FindFirst("PatientId")?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAdministrator => Role == "Administrator";
    public bool IsDoctor        => Role == "Doctor";
    public bool IsPatient       => Role == "Patient";
}
