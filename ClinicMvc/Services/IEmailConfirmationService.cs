using ClinicMvc.Models;

namespace ClinicMvc.Services;

/// <summary>
/// Ја координира логиката за потврда на е-пошта - генерирање токен, испраќање линк,
/// и потврдување откако корисникот ќе кликне на линкот. Се користи и за Patient
/// регистрација и за Doctor креирање (обете сметки бараат потврдена е-пошта пред најава).
/// </summary>
public interface IEmailConfirmationService
{
    /// <summary>Генерира токен, го зачувува и испраќа е-пошта со линк за потврда до корисникот.</summary>
    Task SendConfirmationEmailAsync(User user);

    /// <summary>
    /// Го потврдува токенот - ако е валиден и не е истечен, ја означува е-поштата на
    /// сопствениот корисник како потврдена.
    /// </summary>
    Task<(bool Success, string? Error)> ConfirmEmailAsync(string token);

    /// <summary>
    /// Повторно испраќа линк за потврда до дадената е-пошта (ако постои непотврдена сметка).
    /// Секогаш враќа иста генеричка порака без разлика дали сметката постои - за да не се
    /// открива дали е-поштата е регистрирана во системот (заштита од enumeration).
    /// </summary>
    Task<string> ResendConfirmationAsync(string email);
}
