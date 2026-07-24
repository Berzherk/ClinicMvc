using ClinicMvc.Models;

namespace ClinicMvc.Services;

/// <summary>Бизнис логика за самостојна регистрација на пациенти.</summary>
public interface IPatientAccountService
{
    Task<(bool Success, IEnumerable<string> Errors)> RegisterAsync(RegisterViewModel model);
}
