using ClinicMvc.Models;
using ClinicMvc.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

public class DoctorsController : Controller
{
    private readonly IDoctorRepository _doctorRepository;

    public DoctorsController(IDoctorRepository doctorRepository)
    {
        _doctorRepository = doctorRepository;
    }

    // GET: /Doctors
    public async Task<IActionResult> Index()
    {
        var doctors = await _doctorRepository.GetAllAsync();
        return View(doctors);
    }

    // GET: /Doctors/GetById/5  (за Edit Modal)
    [HttpGet]
    public async Task<IActionResult> GetById(int id)
    {
        var doctor = await _doctorRepository.GetByIdAsync(id);
        if (doctor == null) return NotFound();
        return Json(doctor);
    }

    // POST: /Doctors/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Doctor doctor)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        await _doctorRepository.CreateAsync(doctor);
        return Ok(new { success = true });
    }

    // POST: /Doctors/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Doctor doctor)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, errors });
        }

        await _doctorRepository.UpdateAsync(doctor);
        return Ok(new { success = true });
    }

    // POST: /Doctors/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _doctorRepository.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
