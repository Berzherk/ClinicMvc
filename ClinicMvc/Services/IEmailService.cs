namespace ClinicMvc.Services;

/// <summary>
/// Интерфејс за испраќање е-пошта. Имплементацијата НЕ смее да фрла исклучок кон
/// повикувачкиот код при неуспех - грешките се логираат преку IErrorLogger, а
/// повикувачката операција (закажување/откажување термин) продолжува нормално.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Испраќа е-пошта. Враќа true ако успешно е испратена, false ако не успеала
    /// (веќе логирано внатрешно) - повикувачот може да прикаже предупредување на корисникот.
    /// </summary>
    Task<bool> SendEmailAsync(string toAddress, string subject, string htmlBody);
}
