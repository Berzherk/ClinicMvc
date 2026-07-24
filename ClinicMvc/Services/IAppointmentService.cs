using ClinicMvc.Models;

namespace ClinicMvc.Services;

/// <summary>
/// Бизнис логика за термини/слотови - валидации, координација со Doctor/Patient
/// репозиториумите, и известувања по е-пошта.
/// </summary>
public interface IAppointmentService
{
    /// <summary>Креира нов слободен термински слот (Administrator, опционо Doctor).</summary>
    Task<(bool Success, int NewId, IEnumerable<string> Errors)> CreateSlotAsync(CreateSlotViewModel model, string username);

    /// <summary>Ги менува основните податоци на слот - дозволено само додека статусот е Free.</summary>
    Task<(bool Success, IEnumerable<string> Errors)> UpdateSlotAsync(int id, CreateSlotViewModel model, string username);

    /// <summary>Soft delete на термин/слот - само Administrator.</summary>
    Task DeleteAppointmentAsync(int id, string username);

    /// <summary>
    /// Пациентот резервира слободен слот. Ја вклучува атомската проверка за двојна резервација
    /// и испраќа е-пошта за потврда до пациентот при успех.
    /// </summary>
    Task<(bool Success, IEnumerable<string> Errors)> BookAppointmentAsync(int slotId, int patientId, string username);

    /// <summary>
    /// Откажува термин. cancelledByRole е "Patient", "Doctor" или "Administrator" - според тоа
    /// се испраќа известување до другата страна (доктор ако откажал пациентот, и обратно).
    /// </summary>
    Task<(bool Success, IEnumerable<string> Errors)> CancelAppointmentAsync(int id, string username, string cancelledByRole, string? reason = null);

    /// <summary>Докторот го означува терминот како завршен (Booked -&gt; Completed), со опционални белешки.</summary>
    Task<(bool Success, IEnumerable<string> Errors)> CompleteAppointmentAsync(int id, string? notes, string username);
}
