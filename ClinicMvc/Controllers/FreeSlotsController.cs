using ClinicMvc.Models;
using ClinicMvc.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за приказ на слободни термински слотови на административниот/докторскиот панел.
/// Достапен и за Administrator и за Doctor.
///
/// НАПОМЕНА (нов модел на закажување): слободните слотови сега се реални, зачувани
/// редови во APPOINTMENTS (Status = Free), креирани однапред од администратор/доктор -
/// НЕ се пресметуваат динамички според работно време. LoadSlots едноставно ги
/// пребарува преку IAppointmentRepository.SearchFreeSlotsAsync.
/// Нема самостојна страница - LoadSlots се повикува преку AJAX директно од
/// панелот "Слободни термини" на страницата /Appointments.
/// </summary>
[Authorize(Roles = "Administrator,Doctor")]
public class FreeSlotsController : Controller
{
    private readonly IAppointmentRepository _appointmentRepository;

    public FreeSlotsController(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    /// <summary>
    /// GET: /FreeSlots/LoadSlots
    /// AJAX endpoint кој ги враќа слободните (Status = Free) термински слотови според филтрите.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> LoadSlots(FreeSlotsFilter filter)
    {
        var freeSlots = await _appointmentRepository.SearchFreeSlotsAsync(filter);
        return PartialView("_FreeSlotsTable", freeSlots);
    }
}
