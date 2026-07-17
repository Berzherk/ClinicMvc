using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

/// <summary>
/// Интерфејс за репозиториумот на корисници (за автентикација и авторизација).
/// </summary>
public interface IUserRepository
{
    /// <summary>Го враќа корисникот според корисничко ime - се користи при најава</summary>
    Task<User?> GetByUsernameAsync(string username);

    /// <summary>Го враќа корисникот според DoctorId - се користи за поврзување Doctor со User</summary>
    Task<User?> GetByDoctorIdAsync(int doctorId);

    Task<IEnumerable<User>> GetAllAsync();

    /// <summary>Странирана листа на кориснички сметки. page почнува од 1.</summary>
    Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize);

    /// <summary>Вкупен број кориснички сметки.</summary>
    Task<int> CountAsync();

    Task<User?> GetByIdAsync(int id);
    Task<int> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(int id);

    /// <summary>Ја менува само лозинката на корисникот - се користи при ресетирање лозинка.</summary>
    Task UpdatePasswordAsync(int id, string newPasswordHash, string modifiedBy);
}
