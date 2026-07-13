using System.ComponentModel.DataAnnotations;

namespace ClinicMvc.Models;

/// <summary>
/// Модел кој ги претставува податоците за доктор.
/// Одговара на табелата DOCTORS во Firebird базата.
/// </summary>
public class Doctor
{
    /// <summary>Уникатен идентификатор - auto-increment во базата</summary>
    public int Id { get; set; }

    /// <summary>Ime на докторот - задолжително поле</summary>
    [Required(ErrorMessage = "Името е задолжително")]
    [Display(Name = "Ime")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Презиме на докторот - задолжително поле</summary>
    [Required(ErrorMessage = "Презимето е задолжително")]
    [Display(Name = "Презиме")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Медицинска специјалност на докторот</summary>
    [Display(Name = "Специјалност")]
    public string? Specialty { get; set; }

    /// <summary>Телефонски број за контакт</summary>
    [Display(Name = "Телефон")]
    public string? Phone { get; set; }

    /// <summary>Дали докторот е активен и може да закажува термини</summary>
    [Display(Name = "Активен")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Пресметано поле - целосно ime на докторот.
    /// Не се зачувува во базата, само за приказ.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";
}
