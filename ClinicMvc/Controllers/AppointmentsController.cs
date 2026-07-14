using ClinicMvc.Models;
using ClinicMvc.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за управување со термини.
/// Ги обработува сите CRUD операции и AJAX барања за страницата со термини.
/// </summary>
public class AppointmentsController : Controller
{
    // Репозитории за пристап до базата на податоци
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorRepository      _doctorRepository;
    private readonly IPatientRepository     _patientRepository;

    // Дозволени вредности за статусот на термин
    // Мора да се совпаѓаат со CHECK constraint-от во базата
    private static readonly string[] StatusOptions =
        { "Zakazan", "Vo tek", "Zavrsen", "Otkazen" };

    /// <summary>
    /// Конструктор - зависностите се инјектираат автоматски преку DI контејнерот
    /// </summary>
    public AppointmentsController(
        IAppointmentRepository appointmentRepository,
        IDoctorRepository      doctorRepository,
        IPatientRepository     patientRepository)
    {
        _appointmentRepository = appointmentRepository;
        _doctorRepository      = doctorRepository;
        _patientRepository     = patientRepository;
    }

    /// <summary>
    /// GET: /Appointments
    /// Ја вчитува главната страна со филтри и dropdown опции.
    /// Табелата се вчитува посебно преку AJAX (LoadTable акција).
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Вчитај ги лекарите и специјалностите за dropdown филтрите
        var doctors     = await _doctorRepository.GetAllAsync();
        var specialties = await _doctorRepository.GetSpecialtiesAsync();

        // Подготви го ViewModel-от со сите потребни податоци за View-от
        var vm = new AppointmentIndexViewModel
        {
            Filter       = new AppointmentFilter(),
            Appointments = Enumerable.Empty<Appointment>(),
            Doctors      = doctors.Select(d =>
                new SelectListItem(d.FullName, d.Id.ToString())).ToList(),
            Specialties  = specialties.Select(s =>
                new SelectListItem(s, s)).ToList(),
            Statuses     = StatusOptions.Select(s =>
                new SelectListItem(s, s)).ToList()
        };

        return View(vm);
    }

    /// <summary>
    /// GET: /Appointments/LoadTable
    /// AJAX endpoint кој враќа само Partial View со табелата на термини.
    /// Се повикува при: почетно вчитување, промена на филтри, по Create/Edit/Delete.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> LoadTable(AppointmentFilter filter)
    {
        // Пребарај ги термините според зададените филтри
        var appointments = await _appointmentRepository.SearchAsync(filter);
        // Врати само HTML на табелата, не целата страна
        return PartialView("_AppointmentsTable", appointments);
    }

    /// <summary>
    /// GET: /Appointments/GetById/5
    /// Враќа JSON со податоците за еден термин.
    /// Се користи за полнење на Edit модалот преку AJAX.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null) return NotFound();
        return Json(appointment);
    }

    /// <summary>
    /// GET: /Appointments/GetDropdowns
    /// Враќа JSON со листи за dropdown-ите во модалот.
    /// Само активни лекари може да добијат нови термини.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDropdowns()
    {
        var allDoctors = await _doctorRepository.GetAllAsync();
        var patients   = await _patientRepository.GetAllAsync();

        // Филтрирај само активни лекари - неактивни не може да закажуваат термини
        var activeDoctors = allDoctors.Where(d => d.IsActive);

        return Json(new
        {
            doctors  = activeDoctors.Select(d => new { d.Id, name = d.FullName }),
            patients = patients.Select(p => new { p.Id, name = $"{p.FirstName} {p.LastName}" }),
            statuses = StatusOptions
        });
    }

    /// <summary>
    /// POST: /Appointments/Create
    /// Креира нов термин по успешна валидација.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Appointment appointment)
    {
        // Провери дали избраниот лекар е активен
        var doctor = await _doctorRepository.GetByIdAsync(appointment.DoctorId);
        if (doctor == null || !doctor.IsActive)
            ModelState.AddModelError("DoctorId", "Избраниот лекар не е активен.");

        // Не дозволи закажување за минат датум
        if (appointment.AppointmentDate.Date < DateTime.Today)
            ModelState.AddModelError("AppointmentDate", "Не може да се закаже термин за минат датум.");

        // Провери дали докторот веќе има термин во истото датум и време
        if (await _appointmentRepository.HasConflictAsync(
                appointment.DoctorId, appointment.AppointmentDate, appointment.AppointmentTime))
            ModelState.AddModelError("AppointmentTime", "Докторот веќе има термин во тоа датум и време.");

        // Ако има грешки, врати ги назад до клиентот
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        // Зачувај го терминот во базата
        await _appointmentRepository.CreateAsync(appointment);
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Appointments/Edit
    /// Ажурира постоечки термин по успешна валидација.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Appointment appointment)
    {
        // Провери дали избраниот лекар е активен
        var doctor = await _doctorRepository.GetByIdAsync(appointment.DoctorId);
        if (doctor == null || !doctor.IsActive)
            ModelState.AddModelError("DoctorId", "Избраниот лекар не е активен.");

        // Не дозволи измена на датум во минатото
        if (appointment.AppointmentDate.Date < DateTime.Today)
            ModelState.AddModelError("AppointmentDate", "Не може да се закаже термин за минат датум.");

        // Провери конфликт, но исклучи го тековниот термин (по ID)
        if (await _appointmentRepository.HasConflictAsync(
                appointment.DoctorId, appointment.AppointmentDate,
                appointment.AppointmentTime, appointment.Id))
            ModelState.AddModelError("AppointmentTime", "Докторот веќе има термин во тоа датум и време.");

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        // Ажурирај го терминот во базата
        await _appointmentRepository.UpdateAsync(appointment);
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Appointments/Delete
    /// Брише термин според ID.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _appointmentRepository.DeleteAsync(id);
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Appointments/StartExam
    /// Task 8 - Почеток на преглед.
    /// Го менува статусот на терминот од "Zakazan" во "Vo tek".
    /// Дозволено е само ако терминот моментално е во статус "Zakazan".
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartExam(int id)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return NotFound(new { success = false, errors = new[] { "Терминот не постои." } });

        // Дозволи промена само од Zakazan во Vo tek
        if (appointment.Status != "Zakazan")
            return BadRequest(new { success = false, errors = new[] { "Прегледот може да започне само за закажани термини." } });

        await _appointmentRepository.UpdateStatusAsync(id, "Vo tek");
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Appointments/FinishExam
    /// Task 9 - Завршување на преглед.
    /// Го менува статусот на терминот од "Vo tek" во "Zavrsen" и ги зачувува белешките.
    /// Дозволено е само ако терминот моментално е во статус "Vo tek".
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinishExam(int id, string? notes)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id);
        if (appointment == null)
            return NotFound(new { success = false, errors = new[] { "Терминот не постои." } });

        // Дозволи промена само од Vo tek во Zavrsen
        if (appointment.Status != "Vo tek")
            return BadRequest(new { success = false, errors = new[] { "Прегледот може да заврши само ако е во тек." } });

        await _appointmentRepository.UpdateStatusAsync(id, "Zavrsen", notes);
        return Ok(new { success = true });
    }
}
