using ClinicMvc.Models;
using ClinicMvc.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за управување со доктори.
/// Ги обработува сите CRUD операции преку AJAX и Bootstrap модали.
/// </summary>
public class DoctorsController : Controller
{
    // Репозиториум за пристап до табелата DOCTORS во базата
    private readonly IDoctorRepository _doctorRepository;

    /// <summary>
    /// Конструктор - репозиториумот се инјектира автоматски преку DI
    /// </summary>
    public DoctorsController(IDoctorRepository doctorRepository)
    {
        _doctorRepository = doctorRepository;
    }

    /// <summary>
    /// GET: /Doctors
    /// Ја прикажува страницата со листа на сите доктори.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Вчитај ги сите доктори од базата
        var doctors = await _doctorRepository.GetAllAsync();
        return View(doctors);
    }

    /// <summary>
    /// GET: /Doctors/GetById/5
    /// Враќа JSON со податоците за еден доктор.
    /// Се користи за полнење на Edit модалот преку AJAX.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetById(int id)
    {
        var doctor = await _doctorRepository.GetByIdAsync(id);
        if (doctor == null) return NotFound();
        return Json(doctor);
    }

    /// <summary>
    /// POST: /Doctors/Create
    /// Креира нов доктор по успешна валидација.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Doctor doctor)
    {
        // Провери ги задолжителните полиња (FirstName, LastName)
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        // Зачувај го новиот доктор во базата
        await _doctorRepository.CreateAsync(doctor);
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Doctors/Edit
    /// Ажурира постоечки доктор по успешна валидација.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Doctor doctor)
    {
        // Провери ги задолжителните полиња
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        // Ажурирај го докторот во базата
        await _doctorRepository.UpdateAsync(doctor);
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST: /Doctors/Delete/5
    /// Брише доктор според ID.
    /// Враќа JSON одговор за AJAX повикувачот.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _doctorRepository.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
