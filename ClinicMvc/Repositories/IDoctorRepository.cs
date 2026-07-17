using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

/// <summary>
/// Интерфејс за репозиториумот на доктори.
/// Create/Update/Delete методите бараат "по кого" параметар за audit колоните.
/// </summary>
public interface IDoctorRepository
{
    Task<IEnumerable<Doctor>> GetAllAsync();

    /// <summary>Странирана листа на активни доктори. page почнува од 1.</summary>
    Task<IEnumerable<Doctor>> GetPagedAsync(int page, int pageSize);

    /// <summary>Вкупен број активни (не-избришани) доктори.</summary>
    Task<int> CountAsync();

    Task<Doctor?> GetByIdAsync(int id);
    Task<int> CreateAsync(Doctor doctor, string createdBy);
    Task UpdateAsync(Doctor doctor, string modifiedBy);
    Task DeleteAsync(int id, string modifiedBy);
    Task<IEnumerable<string>> GetSpecialtiesAsync();
    Task<IEnumerable<Appointment>> GetTodayScheduleAsync(int doctorId);
}
