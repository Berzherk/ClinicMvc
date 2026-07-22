using ClinicMvc.Models;
using ClinicMvc.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за приказ на слободни термини.
/// Достапен и за Administrator и за Doctor.
/// Забелешка: нема самостојна страница - LoadSlots се повикува преку AJAX
/// директно од панелот "Слободни термини" на страницата /Appointments.
/// </summary>
[Authorize(Roles = "Administrator,Doctor")]
public class FreeSlotsController : Controller
{
    private readonly IDoctorRepository      _doctorRepository;
    private readonly IAppointmentRepository _appointmentRepository;

    // Работно време на ординацијата - од 08:00 до 16:00
    private static readonly TimeSpan WorkDayStart = new(8, 0, 0);
    private static readonly TimeSpan WorkDayEnd   = new(16, 0, 0);

    // Должина на еден термин - 30 минути
    private static readonly TimeSpan SlotDuration = TimeSpan.FromMinutes(30);

    public FreeSlotsController(
        IDoctorRepository doctorRepository,
        IAppointmentRepository appointmentRepository)
    {
        _doctorRepository      = doctorRepository;
        _appointmentRepository = appointmentRepository;
    }

    /// <summary>
    /// GET: /FreeSlots/LoadSlots
    /// AJAX endpoint кој ги пресметува и враќа слободните термини според филтрите.
    /// Ако е избран конкретен доктор - прикажува само за него.
    /// Ако е избрана само специјалност (без конкретен доктор) - прикажува за сите
    /// активни доктори од таа специјалност.
    /// Датумот е задолжителен за да се пресметаат термините.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> LoadSlots(FreeSlotsFilter filter)
    {
        // Без датум не може да се пресметаат слободни термини
        if (!filter.Date.HasValue)
        {
            return PartialView("_FreeSlotsTable", Enumerable.Empty<FreeSlot>());
        }

        // Не прикажувај слободни термини за минати датуми
        if (filter.Date.Value.Date < DateTime.Today)
        {
            return PartialView("_FreeSlotsTable", Enumerable.Empty<FreeSlot>());
        }

        var allDoctors = await _doctorRepository.GetAllAsync();
        var activeDoctors = allDoctors.Where(d => d.IsActive);

        // Стесни ја листата на доктори според избраните филтри
        if (filter.DoctorId.HasValue)
        {
            activeDoctors = activeDoctors.Where(d => d.Id == filter.DoctorId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.Specialty))
        {
            activeDoctors = activeDoctors.Where(d => d.Specialty == filter.Specialty);
        }

        var freeSlots = new List<FreeSlot>();

        // За секој доктор пресметај ги слободните термини за избраниот датум
        foreach (var doctor in activeDoctors)
        {
            var bookedTimes = await _appointmentRepository.GetBookedTimesAsync(doctor.Id, filter.Date.Value);
            var bookedSet   = bookedTimes.ToHashSet();

            // Генерирај ги сите можни термини во работното време
            for (var time = WorkDayStart; time < WorkDayEnd; time += SlotDuration)
            {
                // Прескокни го терминот ако веќе е зафатен
                if (bookedSet.Contains(time))
                    continue;

                // Ако датумот е денес, прескокни ги термините што веќе поминале
                if (filter.Date.Value.Date == DateTime.Today && time < DateTime.Now.TimeOfDay)
                    continue;

                freeSlots.Add(new FreeSlot
                {
                    DoctorId        = doctor.Id,
                    DoctorName      = doctor.FullName,
                    DoctorSpecialty = doctor.Specialty,
                    Date            = filter.Date.Value,
                    Time            = time
                });
            }
        }

        // Подреди по доктор, па по време
        var sorted = freeSlots.OrderBy(s => s.DoctorName).ThenBy(s => s.Time);

        return PartialView("_FreeSlotsTable", sorted);
    }
}
