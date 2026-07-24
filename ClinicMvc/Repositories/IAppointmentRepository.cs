using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

public interface IAppointmentRepository
{
    Task<IEnumerable<Appointment>> SearchAsync(AppointmentFilter filter);
    Task<int> CountAsync(AppointmentFilter filter);
    Task<AppointmentStatistics> GetStatisticsAsync(AppointmentFilter filter);
    Task<Appointment?> GetByIdAsync(int id);

    /// <summary>Креира нов слободен термински слот (Status = Free, PatientId = NULL).</summary>
    Task<int> CreateSlotAsync(int doctorId, DateTime date, TimeSpan time, string createdBy);

    /// <summary>Ги менува основните податоци на слот (доктор/датум/време) - дозволено само додека е Free.</summary>
    Task UpdateSlotAsync(int id, int doctorId, DateTime date, TimeSpan time, string modifiedBy);

    Task DeleteAsync(int id, string modifiedBy);

    /// <summary>
    /// Атомски ја резервира слободниот слот за пациентот - го проверува условот STATUS = 'Free'
    /// во самиот UPDATE, за да се спречи двојна резервација при паралелни барања.
    /// Враќа false ако слотот веќе не е слободен (некој друг го зазел во меѓувреме).
    /// </summary>
    Task<bool> BookSlotAsync(int id, int patientId, string modifiedBy);

    Task UpdateStatusAsync(int id, string newStatus, string modifiedBy, string? notes = null);

    /// <summary>Ги враќа слободните термински слотови (Status = Free) според филтрите - за прикажување и резервација.</summary>
    Task<IEnumerable<FreeSlot>> SearchFreeSlotsAsync(FreeSlotsFilter filter);

    /// <summary>Ги враќа термините на конкретен пациент - идни (Booked) или историски (Completed/Cancelled).</summary>
    Task<IEnumerable<Appointment>> GetPatientAppointmentsAsync(int patientId, bool upcomingOnly);

    /// <summary>
    /// Дали веќе постои (не-избришан) термин за дадениот лекар на точно тој датум/време -
    /// спречува создавање на два слотови во ист термин за истиот лекар.
    /// excludeId се користи при измена на постоечки слот - за да не се спореди со самиот себе.
    /// </summary>
    Task<bool> ExistsAtSlotAsync(int doctorId, DateTime date, TimeSpan time, int excludeId);
}
