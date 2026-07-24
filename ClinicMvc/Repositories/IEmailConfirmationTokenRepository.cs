using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

/// <summary>Репозиториум за токени за потврда на е-пошта.</summary>
public interface IEmailConfirmationTokenRepository
{
    Task<int> CreateAsync(int userId, string token, DateTime expiresOn);
    Task<EmailConfirmationToken?> GetByTokenAsync(string token);
    Task DeleteAsync(int id);
    Task DeleteAllForUserAsync(int userId);
}
