using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

/// <summary>
/// Интерфејс за репозиториумот на термини.
/// Дефинира кои операции се достапни за работа со табелата APPOINTMENTS.
/// </summary>
public interface IAppointmentRepository
{
    Task<IEnumerable<Appointment>> GetAllAsync();
    Task<IEnumerable<Appointment>> SearchAsync(AppointmentFilter filter);
    Task<Appointment?> GetByIdAsync(int id);
    Task<int> CreateAsync(Appointment appointment);
    Task UpdateAsync(Appointment appointment);
    Task DeleteAsync(int id);
    Task<bool> HasConflictAsync(int doctorId, DateTime date, TimeSpan time, int excludeId = 0);

    /// <summary>
    /// Ги менува статусот и (опционално) белешките на еден термин.
    /// Се користи за „Почеток на преглед" и „Завршување на преглед".
    /// </summary>
    Task UpdateStatusAsync(int id, string newStatus, string? notes = null);

    /// <summary>
    /// Ги враќа сите закажани времиња за конкретен доктор на конкретен датум.
    /// Се користи за пресметка на слободни термини (Task 10).
    /// Откажаните термини (Otkazen) не се сметаат за зафатени.
    /// </summary>
    Task<IEnumerable<TimeSpan>> GetBookedTimesAsync(int doctorId, DateTime date);
}
