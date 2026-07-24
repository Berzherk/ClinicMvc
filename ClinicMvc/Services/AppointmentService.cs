using ClinicMvc.Models;
using ClinicMvc.Repositories;
using Microsoft.Extensions.Options;

namespace ClinicMvc.Services;

/// <summary>
/// Бизнис логика за термини/слотови. НАПОМЕНА: старото правило за минимум 15 минути
/// разлика меѓу термините на ист доктор е целосно отстрането - веќе не е потребно
/// бидејќи термините повеќе не се внесуваат рачно, туку се бираат од однапред
/// дефинирани слотови (кои по дефиниција не можат да се преклопуваат - секој слот
/// може да биде резервиран само еднаш, проверено атомски во репозиториумот).
/// </summary>
public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorRepository      _doctorRepository;
    private readonly IPatientRepository     _patientRepository;
    private readonly IUserRepository        _userRepository;
    private readonly IAuditLogRepository    _auditLogRepository;
    private readonly IEmailService          _emailService;
    private readonly EmailSettings          _emailSettings;

    public AppointmentService(
        IAppointmentRepository appointmentRepository,
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IEmailService emailService,
        IOptions<EmailSettings> emailSettings)
    {
        _appointmentRepository = appointmentRepository;
        _doctorRepository      = doctorRepository;
        _patientRepository     = patientRepository;
        _userRepository        = userRepository;
        _auditLogRepository    = auditLogRepository;
        _emailService          = emailService;
        _emailSettings         = emailSettings.Value;
    }

    public async Task<(bool Success, int NewId, IEnumerable<string> Errors)> CreateSlotAsync(CreateSlotViewModel model, string username)
    {
        var errors = new List<string>();

        var doctor = await _doctorRepository.GetByIdAsync(model.DoctorId);
        if (doctor == null || !doctor.IsActive)
            errors.Add("Избраниот лекар не е активен.");

        if (model.Date.Date < DateTime.Today)
            errors.Add("Не може да се креира термински слот за минат датум.");

        if (!SlotSchedule.IsValidSlotTime(model.Time))
            errors.Add("Времето мора да биде во работно време (08:00-16:00), на секои 30 минути.");

        if (errors.Count == 0 && await _appointmentRepository.ExistsAtSlotAsync(model.DoctorId, model.Date, model.Time, excludeId: 0))
            errors.Add("Веќе постои термин за овој лекар на избраниот датум и време.");

        if (errors.Count > 0)
            return (false, 0, errors);

        var newId = await _appointmentRepository.CreateSlotAsync(model.DoctorId, model.Date, model.Time, username);
        await _auditLogRepository.LogAsync("CREATE", "Appointment", newId, username,
            $"Креиран термински слот за {model.Date:dd.MM.yyyy} {model.Time}");

        return (true, newId, errors);
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> UpdateSlotAsync(int id, CreateSlotViewModel model, string username)
    {
        var errors = new List<string>();

        var existing = await _appointmentRepository.GetByIdAsync(id);
        if (existing == null)
            return (false, new[] { "Терминот не постои." });

        if (existing.Status != AppointmentStatus.Free)
            return (false, new[] { "Може да се менуваат само слободни (нерезервирани) слотови." });

        var doctor = await _doctorRepository.GetByIdAsync(model.DoctorId);
        if (doctor == null || !doctor.IsActive)
            errors.Add("Избраниот лекар не е активен.");

        if (model.Date.Date < DateTime.Today)
            errors.Add("Не може да се закаже термин за минат датум.");

        if (!SlotSchedule.IsValidSlotTime(model.Time))
            errors.Add("Времето мора да биде во работно време (08:00-16:00), на секои 30 минути.");

        if (errors.Count == 0 && await _appointmentRepository.ExistsAtSlotAsync(model.DoctorId, model.Date, model.Time, excludeId: id))
            errors.Add("Веќе постои термин за овој лекар на избраниот датум и време.");

        if (errors.Count > 0)
            return (false, errors);

        await _appointmentRepository.UpdateSlotAsync(id, model.DoctorId, model.Date, model.Time, username);
        await _auditLogRepository.LogAsync("UPDATE", "Appointment", id, username, "Изменет термински слот");

        return (true, errors);
    }

    public async Task DeleteAppointmentAsync(int id, string username)
    {
        await _appointmentRepository.DeleteAsync(id, username);
        await _auditLogRepository.LogAsync("DELETE", "Appointment", id, username, "Soft delete на термин");
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> BookAppointmentAsync(int slotId, int patientId, string username)
    {
        var slot = await _appointmentRepository.GetByIdAsync(slotId);
        if (slot == null)
            return (false, new[] { "Терминот не постои." });

        if (slot.Status != AppointmentStatus.Free)
            return (false, new[] { "Терминот веќе е резервиран или не е достапен." });

        if (slot.AppointmentDate.Date < DateTime.Today)
            return (false, new[] { "Не може да се резервира термин за минат датум." });

        // Атомска резервација - го спречува двојното резервирање дури и при паралелни барања.
        var booked = await _appointmentRepository.BookSlotAsync(slotId, patientId, username);
        if (!booked)
            return (false, new[] { "За жал, некој друг веќе го резервираше овој термин. Изберете друг слободен слот." });

        await _auditLogRepository.LogAsync("UPDATE", "Appointment", slotId, username,
            $"Термин резервиран за {slot.AppointmentDate:dd.MM.yyyy} {slot.AppointmentTime}");

        // Известување до пациентот - грешката при праќање НЕ ја враќа операцијата назад.
        var patient = await _patientRepository.GetByIdAsync(patientId);
        var doctor  = await _doctorRepository.GetByIdAsync(slot.DoctorId);

        if (patient != null && !string.IsNullOrWhiteSpace(patient.Email) && doctor != null)
        {
            var subject = $"{_emailSettings.ClinicName} - Потврда на закажан термин";
            var body = $@"
                <p>Здраво {Encode(patient.FullName)},</p>
                <p>Вашиот термин е успешно закажан:</p>
                <ul>
                    <li><strong>Клиника:</strong> {Encode(_emailSettings.ClinicName)}</li>
                    <li><strong>Лекар:</strong> {Encode(doctor.FullName)}</li>
                    <li><strong>Специјалност:</strong> {Encode(doctor.Specialty ?? "-")}</li>
                    <li><strong>Датум:</strong> {slot.AppointmentDate:dd.MM.yyyy}</li>
                    <li><strong>Време:</strong> {slot.AppointmentTime:hh\\:mm}</li>
                    <li><strong>Статус:</strong> Booked</li>
                </ul>
                <p>{_emailSettings.ClinicContact}</p>";

            await _emailService.SendEmailAsync(patient.Email, subject, body);
        }

        return (true, Enumerable.Empty<string>());
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> CancelAppointmentAsync(int id, string username, string cancelledByRole, string? reason = null)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return (false, new[] { "Терминот не постои." });

        if (appointment.Status != AppointmentStatus.Booked)
            return (false, new[] { "Може да се откажат само резервирани термини." });

        await _appointmentRepository.UpdateStatusAsync(id, AppointmentStatus.Cancelled, username);
        await _auditLogRepository.LogAsync("UPDATE", "Appointment", id, username,
            $"Термин откажан ({cancelledByRole})" + (string.IsNullOrWhiteSpace(reason) ? "" : $" - {reason}"));

        var doctor  = await _doctorRepository.GetByIdAsync(appointment.DoctorId);
        Patient? patient = appointment.PatientId.HasValue
            ? await _patientRepository.GetByIdAsync(appointment.PatientId.Value)
            : null;

        if (cancelledByRole == "Patient")
        {
            // Пациентот откажал - известуваме го докторот преку неговата кориснич сметка.
            var doctorUser = doctor != null ? await _userRepository.GetByDoctorIdAsync(doctor.Id) : null;
            if (doctorUser != null && !string.IsNullOrWhiteSpace(doctorUser.Email) && patient != null)
            {
                var subject = $"{_emailSettings.ClinicName} - Пациент откажа термин";
                var body = $@"
                    <p>Здраво д-р {Encode(doctor!.FullName)},</p>
                    <p>Пациентот <strong>{Encode(patient.FullName)}</strong> го откажа следниот термин:</p>
                    <ul>
                        <li><strong>Датум:</strong> {appointment.AppointmentDate:dd.MM.yyyy}</li>
                        <li><strong>Време:</strong> {appointment.AppointmentTime:hh\\:mm}</li>
                        <li><strong>Време на откажување:</strong> {DateTime.Now:dd.MM.yyyy HH:mm}</li>
                    </ul>";

                await _emailService.SendEmailAsync(doctorUser.Email, subject, body);
            }
        }
        else
        {
            // Доктор/администратор откажал - известуваме го пациентот.
            if (patient != null && !string.IsNullOrWhiteSpace(patient.Email) && doctor != null)
            {
                var subject = $"{_emailSettings.ClinicName} - Терминот е откажан";
                var body = $@"
                    <p>Здраво {Encode(patient.FullName)},</p>
                    <p>За жал, вашиот термин со д-р {Encode(doctor.FullName)} закажан за
                       {appointment.AppointmentDate:dd.MM.yyyy} во {appointment.AppointmentTime:hh\\:mm} е откажан.</p>
                    {(string.IsNullOrWhiteSpace(reason) ? "" : $"<p><strong>Причина:</strong> {Encode(reason)}</p>")}
                    <p>{Encode(_emailSettings.ClinicContact)}</p>
                    <p>Ве покануваме да закажете нов термин преку вашиот пациентски профил кога ќе ви одговара.</p>";

                await _emailService.SendEmailAsync(patient.Email, subject, body);
            }
        }

        return (true, Enumerable.Empty<string>());
    }

    public async Task<(bool Success, IEnumerable<string> Errors)> CompleteAppointmentAsync(int id, string? notes, string username)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return (false, new[] { "Терминот не постои." });

        if (appointment.Status != AppointmentStatus.Booked)
            return (false, new[] { "Може да се завршат само резервирани термини." });

        await _appointmentRepository.UpdateStatusAsync(id, AppointmentStatus.Completed, username, notes);
        await _auditLogRepository.LogAsync("UPDATE", "Appointment", id, username, "Термин означен како завршен");

        return (true, Enumerable.Empty<string>());
    }

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
