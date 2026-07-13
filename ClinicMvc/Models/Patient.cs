using System.ComponentModel.DataAnnotations;

namespace ClinicMvc.Models;

/// <summary>
/// Модел кој ги претставува податоците за пациент.
/// Одговара на табелата PATIENTS во Firebird базата.
/// </summary>
public class Patient
{
    /// <summary>Уникатен идентификатор - auto-increment во базата</summary>
    public int Id { get; set; }

    /// <summary>Ime на пациентот - задолжително поле</summary>
    [Required(ErrorMessage = "Името е задолжително")]
    [Display(Name = "Ime")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Презиме на пациентот - задолжително поле</summary>
    [Required(ErrorMessage = "Презимето е задолжително")]
    [Display(Name = "Презиме")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Единствен матичен број на граѓанинот - мора да биде уникатен во системот.
    /// Содржи точно 13 цифри.
    /// </summary>
    [Required(ErrorMessage = "ЕМБГ е задолжителен")]
    [StringLength(13, MinimumLength = 13, ErrorMessage = "ЕМБГ мора да содржи точно 13 цифри")]
    [Display(Name = "ЕМБГ")]
    public string Embg { get; set; } = string.Empty;

    /// <summary>Телефонски број за контакт</summary>
    [Display(Name = "Телефон")]
    public string? Phone { get; set; }

    /// <summary>Е-пошта адреса - мора да биде во валиден формат</summary>
    [EmailAddress(ErrorMessage = "Внесете валидна е-пошта адреса")]
    [Display(Name = "Е-пошта")]
    public string? Email { get; set; }
}
