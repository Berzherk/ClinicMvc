using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ClinicMvc.Services;

/// <summary>
/// Испраќа реални е-пораки преку SMTP (Gmail, Outlook или кој било друг SMTP провајдер),
/// користејќи ги вградените .NET System.Net.Mail класи - нема потреба од дополнителен NuGet пакет.
///
/// Конфигурацијата (Host/Port/Username/Password) доаѓа од IOptions&lt;EmailSettings&gt;,
/// поврзана со секцијата "Smtp" во appsettings.json / User Secrets / environment variables.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly IErrorLogger _errorLogger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SmtpEmailService(IOptions<EmailSettings> settings, IErrorLogger errorLogger,
        IHttpContextAccessor httpContextAccessor)
    {
        _settings = settings.Value;
        _errorLogger = errorLogger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Ако праќањето не успее (погрешни SMTP акредитиви, недостапен сервер итн.), исклучокот
    /// се логира и методот враќа false - НИКОГАШ не фрла натаму, за да не ја урне операцијата
    /// (закажување/откажување термин) која го предизвикала испраќањето.
    /// </summary>
    public async Task<bool> SendEmailAsync(string toAddress, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
            return false;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toAddress);

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = _settings.EnableSsl
            };

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            // Не смееме да ја урнеме апликацијата поради грешка при праќање е-пошта -
            // ја логираме грешката и продолжуваме (повикувачот ќе прикаже предупредување).
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                await _errorLogger.LogErrorAsync(ex, context);
            }
            return false;
        }
    }
}
