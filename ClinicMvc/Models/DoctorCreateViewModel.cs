using System.ComponentModel.DataAnnotations;

namespace ClinicMvc.Models;

/// <summary>
/// ViewModel за креирање доктор ЗАЕДНО со неговата корисничка сметка.
/// Се користи само во DoctorsController.Create - Edit продолжува да го користи
/// обичниот Doctor модел бидејќи не се менуваат акредитивите при измена.
/// </summary>
public class DoctorCreateViewModel
{
    // ── Податоци за докторот ──
    [Required(ErrorMessage = "Името е задолжително")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Презимето е задолжително")]
    public string LastName { get; set; } = string.Empty;

    public string? Specialty { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    // ── Податоци за корисничката сметка (само при креирање) ──
    [Required(ErrorMessage = "Корисничкото ime е задолжително")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Корисничкото ime мора да има од 3 до 50 карактери")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Лозинката е задолжителна")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Лозинката мора да има најмалку 6 карактери")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
