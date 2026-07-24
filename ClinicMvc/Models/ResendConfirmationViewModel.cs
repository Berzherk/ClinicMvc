using System.ComponentModel.DataAnnotations;

namespace ClinicMvc.Models;

/// <summary>ViewModel за страницата "Испрати повторно линк за потврда".</summary>
public class ResendConfirmationViewModel
{
    [Required(ErrorMessage = "Внесете ја е-поштата")]
    [EmailAddress(ErrorMessage = "Внесете валидна е-пошта адреса")]
    [Display(Name = "Е-пошта")]
    public string Email { get; set; } = string.Empty;
}
