using ClinicMvc.Models;
using ClinicMvc.Repositories;

namespace ClinicMvc.Services;

/// <summary>
/// Бизнис логика за термини - ги извршува проверките (активен доктор, минат датум,
/// конфликт на термин) пред да ги повика соодветните репозиториумски операции.
/// </summary>
public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorRepository      _doctorRepository;
    private readonly IAuditLogRepository    _auditLogRepository;

    public AppointmentService(
        IAppointmentRepository appointmentRepository,
        IDoctorRepository doctorRepository,
        IAuditLogRepository auditLogRepository)
    {
        _appointmentRepository = appointmentRepository;
        _doctorRepository      = doctorRepository;
        _auditLogRepository    = auditLogRepository;
    }

    public async Task<(bool Success, int NewId, IEnumerable<string> Errors)> CreateAppointmentAsync(Appointment appointment, string username)
    {
        var errors = new List<string>();

        var doctor = await _doctorRepository.GetByIdAsync(appointment.DoctorId);
        if (doctor == null || !doctor.IsActive)
            errors.Add("Избраниот лекар не е активен.");

        if (appointment.AppointmentDate.Date < DateTime.Today)
            errors.Add("Не може да се закаже термин за минат датум.");

        if (await _appointmentRepository.HasConflictAsync(
                appointment.DoctorId, appointment.AppointmentDate, appointment.AppointmentTime))
            errors.Add("Не може да се закаже овој термин. Докторот веќе има термин во период од 15 минути од избраното време.");

        if (errors.Count > 0)
            return (false, 0, errors);

        var newId = await _appointmentRepository.CreateAsync(appointment, username);
        await _auditLogRepository.LogAsync("CREATE", "Appointment", newId, username,
            $"Закажан термин за {appointment.AppointmentDate:dd.MM.yyyy} {appointment.AppointmentTime}");

        return (true, newId, errors);
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> UpdateAppointmentAsync(Appointment appointment, string username)
    {
        var errors = new List<string>();

        var doctor = await _doctorRepository.GetByIdAsync(appointment.DoctorId);
        if (doctor == null || !doctor.IsActive)
            errors.Add("Избраниот лекар не е активен.");

        if (appointment.AppointmentDate.Date < DateTime.Today)
            errors.Add("Не може да се закаже термин за минат датум.");

        if (await _appointmentRepository.HasConflictAsync(
                appointment.DoctorId, appointment.AppointmentDate, appointment.AppointmentTime, appointment.Id))
            errors.Add("Не може да се закаже овој термин. Докторот веќе има термин во период од 15 минути од избраното време.");

        if (errors.Count > 0)
            return (false, errors);

        await _appointmentRepository.UpdateAsync(appointment, username);
        await _auditLogRepository.LogAsync("UPDATE", "Appointment", appointment.Id, username, "Изменет термин");

        return (true, errors);
    }

    public async Task DeleteAppointmentAsync(int id, string username)
    {
        await _appointmentRepository.DeleteAsync(id, username);
        await _auditLogRepository.LogAsync("DELETE", "Appointment", id, username, "Soft delete на термин");
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> StartExamAsync(int id, string username)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return (false, new[] { "Терминот не постои." });

        if (appointment.Status != "Zakazan")
            return (false, new[] { "Прегледот може да започне само за закажани термини." });

        await _appointmentRepository.UpdateStatusAsync(id, "Vo tek", username);
        await _auditLogRepository.LogAsync("UPDATE", "Appointment", id, username, "Почеток на преглед");

        return (true, Enumerable.Empty<string>());
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> FinishExamAsync(int id, string? notes, string username)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return (false, new[] { "Терминот не постои." });

        if (appointment.Status != "Vo tek")
            return (false, new[] { "Прегледот може да заврши само ако е во тек." });

        await _appointmentRepository.UpdateStatusAsync(id, "Zavrsen", username, notes);
        await _auditLogRepository.LogAsync("UPDATE", "Appointment", id, username, "Завршување на преглед");

        return (true, Enumerable.Empty<string>());
    }
}
