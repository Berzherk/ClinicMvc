using ClinicMvc.Models;
using ClinicMvc.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за управување со пациенти.
/// Ги обработува сите CRUD операции преку AJAX и Bootstrap модали.
/// </summary>
public class PatientsController : Controller
{
    // Репозиториум за пристап до табелата PATIENTS во базата
    private readonly IPatientRepository _patientRepository;

    /// <summary>
    /// Конструктор - репозиториумот се инјектира автоматски преку DI
    /// </summary>
    public PatientsController(IPatientRepository patientRepository)
    {
        _patientRepository = patientRepository;
    }

    /// <summary>
    /// GET: /Patients
    /// Ја прикажува страницата со листа на сите пациенти.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Вчитај ги сите пациенти од базата
        var patients = await _patientRepository.GetAllAsync();
        return View(patients);
    }

    /// <summary>
    /// GET: /Patients/GetById/5
    /// Враќа JSON со податоците за еден пациент.
    /// Се користи за полнење на Edit модалот преку AJAX.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetById(int id)
    {
        var patient = await _patientRepository.GetByIdAsync(id);
        if (patient == null) return NotFound();
        return Json(patient);
    }

    /// <summary>
    /// POST: /Patients/Create
    /// Креира нов пациент по успешна валидација.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Patient patient)
    {
        // Провери дали ЕМБГ веќе постои во базата (мора да биде единствен)
        if (await _patientRepository.EmbgExistsAsync(patient.Embg))
            ModelState.AddModelError("Embg", "ЕМБГ веќе постои во системот.");

        // Провери ги задолжителните полиња и форматот на е-пошта
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        // Зачувај го новиот пациент во базата
        await _patientRepository.CreateAsync(patient);
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Patients/Edit
    /// Ажурира постоечки пациент по успешна валидација.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Patient patient)
    {
        // Провери дали ЕМБГ веќе го користи друг пациент (исклучи го тековниот)
        if (await _patientRepository.EmbgExistsAsync(patient.Embg, patient.Id))
            ModelState.AddModelError("Embg", "ЕМБГ веќе постои во системот.");

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        // Ажурирај го пациентот во базата
        await _patientRepository.UpdateAsync(patient);
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Patients/Delete
    /// Брише пациент според ID.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _patientRepository.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
