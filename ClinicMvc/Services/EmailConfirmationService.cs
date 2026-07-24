using ClinicMvc.Models;
using ClinicMvc.Repositories;

namespace ClinicMvc.Services;

public class EmailConfirmationService : IEmailConfirmationService
{
    private readonly IEmailConfirmationTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _settings;

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public EmailConfirmationService(
        IEmailConfirmationTokenRepository tokenRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        Microsoft.Extensions.Options.IOptions<EmailSettings> settings)
    {
        _tokenRepository = tokenRepository;
        _userRepository   = userRepository;
        _emailService     = emailService;
        _settings         = settings.Value;
    }

    public async Task SendConfirmationEmailAsync(User user)
    {
        // Избришете ги старите неискористени токени за овој корисник пред да создадете нов -
        // спречува непотребно натрупување на редови кога корисникот бара повеќе линкови.
        await _tokenRepository.DeleteAllForUserAsync(user.Id);

        var token = Guid.NewGuid().ToString("N");
        await _tokenRepository.CreateAsync(user.Id, token, DateTime.UtcNow.Add(TokenLifetime));

        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var confirmLink = $"{baseUrl}/Account/ConfirmEmail?token={token}";

        var subject = $"{_settings.ClinicName} - Потврдете ја вашата е-пошта";
        var body = $@"
            <p>Здраво {EncodeHtml(user.Username)},</p>
            <p>Ви благодариме што се регистриравте во системот на <strong>{_settings.ClinicName}</strong>.</p>
            <p>За да ја активирате вашата сметка, кликнете на линкот подолу:</p>
            <p><a href=""{confirmLink}"">Потврди ја е-поштата</a></p>
            <p>Ако копчето не работи, копирајте ја оваа адреса во прелистувачот:<br/>{confirmLink}</p>
            <p>Линкот важи 24 часа.</p>
            <p>{_settings.ClinicContact}</p>";

        await _emailService.SendEmailAsync(user.Email, subject, body);
    }

    public async Task<string> ResendConfirmationAsync(string email)
    {
        const string genericMessage =
            "Ако постои сметка со таа е-пошта која сè уште не е потврдена, испративме нов линк за потврда.";

        if (string.IsNullOrWhiteSpace(email))
            return genericMessage;

        var user = await _userRepository.GetByEmailAsync(email.Trim());

        // Намерно иста генеричка порака без разлика дали сметката постои, дали веќе е
        // потврдена, итн. - за да не откриваме дали некоја е-пошта е регистрирана.
        if (user == null || user.EmailConfirmed)
            return genericMessage;

        await SendConfirmationEmailAsync(user);
        return genericMessage;
    }

    public async Task<(bool Success, string? Error)> ConfirmEmailAsync(string token)
    {
        var record = await _tokenRepository.GetByTokenAsync(token);
        if (record == null)
            return (false, "Линкот за потврда не е валиден.");

        if (record.ExpiresOn < DateTime.UtcNow)
        {
            await _tokenRepository.DeleteAsync(record.Id);
            return (false, "Линкот за потврда е истечен. Ве молиме, побарајте нов.");
        }

        await _userRepository.ConfirmEmailAsync(record.UserId);
        await _tokenRepository.DeleteAllForUserAsync(record.UserId);

        return (true, null);
    }

    private static string EncodeHtml(string value) => System.Net.WebUtility.HtmlEncode(value);
}
