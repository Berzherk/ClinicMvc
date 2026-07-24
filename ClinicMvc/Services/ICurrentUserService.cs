namespace ClinicMvc.Services;

/// <summary>
/// Помошен сервис за читање на податоците за најавениот корисник од HttpContext.
/// Ги избегнува повторените ClaimsPrincipal повици во секој контролер.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Корисничко ime на најавениот корисник (се користи за CreatedBy/ModifiedBy)</summary>
    string Username { get; }

    /// <summary>"Administrator", "Doctor" или "Patient"</summary>
    string? Role { get; }

    /// <summary>ID на докторот поврзан со сметката (null ако корисникот не е доктор)</summary>
    int? DoctorId { get; }

    /// <summary>ID на пациентот поврзан со сметката (null ако корисникот не е пациент)</summary>
    int? PatientId { get; }

    bool IsAdministrator { get; }
    bool IsDoctor { get; }
    bool IsPatient { get; }
}
