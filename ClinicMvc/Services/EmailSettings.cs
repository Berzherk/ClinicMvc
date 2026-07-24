namespace ClinicMvc.Services;

/// <summary>
/// Поставки за SMTP испраќање на е-пошта - се читаат од конфигурацијата (appsettings.json /
/// appsettings.Development.json / User Secrets / environment variables), секција "Smtp".
///
/// ВАЖНО: Host/Username/Password НИКОГАШ не се хардкодираат во кодот. За development
/// најдобро да се постават преку `dotnet user-secrets set "Smtp:Password" "..."`,
/// а во продукција преку environment variables (Smtp__Host, Smtp__Username, Smtp__Password итн.).
/// </summary>
public class EmailSettings
{
    /// <summary>SMTP сервер, пр. smtp.gmail.com или smtp-mail.outlook.com</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP порта - обично 587 (STARTTLS) или 465 (SSL)</summary>
    public int Port { get; set; } = 587;

    /// <summary>Корисничко ime за SMTP автентикација (обично целосната е-пошта адреса)</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Лозинка / App Password за SMTP автентикација</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Адреса која се прикажува како испраќач</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Ime кое се прикажува како испраќач, пр. "Клиника - Известувања"</summary>
    public string FromName { get; set; } = "Клиника";

    /// <summary>Дали да се користи SSL/TLS при поврзување (за повеќето провајдери: true)</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>Име на клиниката - се користи во содржината на е-пораките</summary>
    public string ClinicName { get; set; } = "Клиника";

    /// <summary>Контакт информации на клиниката - се вклучуваат во е-пораките за откажување</summary>
    public string ClinicContact { get; set; } = string.Empty;

    /// <summary>Базен URL на апликацијата (за линкот за потврда на е-пошта), пр. https://localhost:5001</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
