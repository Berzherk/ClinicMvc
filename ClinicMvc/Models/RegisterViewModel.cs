using System.ComponentModel.DataAnnotations;

namespace ClinicMvc.Models;

/// <summary>
/// ViewModel за самостојна регистрација на пациент (јавно достапна страница).
/// Креира и Patient и User запис во исто барање, испраќа е-пошта за потврда.
/// </summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "Името е задолжително")]
    [Display(Name = "Ime")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Презимето е задолжително")]
    [Display(Name = "Презиме")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ЕМБГ е задолжителен")]
    [StringLength(13, MinimumLength = 13, ErrorMessage = "ЕМБГ мора да содржи точно 13 цифри")]
    [Display(Name = "ЕМБГ")]
    public string Embg { get; set; } = string.Empty;

    [Display(Name = "Телефон")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Е-поштата е задолжителна")]
    [EmailAddress(ErrorMessage = "Внесете валидна е-пошта адреса")]
    [Display(Name = "Е-пошта")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Корисничкото ime е задолжително")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Корисничкото ime мора да има од 3 до 50 карактери")]
    [Display(Name = "Корисничко ime")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Лозинката е задолжителна")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Лозинката мора да има најмалку 6 карактери")]
    [DataType(DataType.Password)]
    [Display(Name = "Лозинка")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Потврдете ја лозинката")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Лозинките не се совпаѓаат")]
    [Display(Name = "Потврди лозинка")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
