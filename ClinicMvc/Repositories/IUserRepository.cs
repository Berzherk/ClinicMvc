using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

/// <summary>
/// Интерфејс за репозиториумот на корисници (за автентикација и авторизација).
/// </summary>
public interface IUserRepository
{
    /// <summary>Го враќа корисникот според корисничко ime - се користи при најава</summary>
    Task<User?> GetByUsernameAsync(string username);

    /// <summary>Го враќа корисникот според е-пошта - се користи за проверка на дупликати.</summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>Дали веќе постои корисник со дадената е-пошта.</summary>
    Task<bool> EmailExistsAsync(string email);

    /// <summary>Странирана листа на кориснички сметки. page почнува од 1.</summary>
    Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize);

    /// <summary>Вкупен број кориснички сметки.</summary>
    Task<int> CountAsync();

    Task<User?> GetByIdAsync(int id);
    Task<int> CreateAsync(User user);
    Task DeleteAsync(int id);

    /// <summary>Ја менува само лозинката на корисникот - се користи при ресетирање лозинка.</summary>
    Task UpdatePasswordAsync(int id, string newPasswordHash, string modifiedBy);

    /// <summary>Го означува е-поштата на корисникот како потврдена (по клик на линкот за потврда).</summary>
    Task ConfirmEmailAsync(int userId);

    /// <summary>Ја враќа корисничката сметка поврзана со конкретен доктор - се користи за е-пошта известувања.</summary>
    Task<User?> GetByDoctorIdAsync(int doctorId);
}
