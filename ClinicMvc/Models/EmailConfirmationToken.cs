namespace ClinicMvc.Models;

/// <summary>
/// Токен за потврда на е-пошта - одговара на табелата EMAILCONFIRMATIONTOKENS.
/// Се генерира при регистрација/креирање сметка и се брише откако ќе се искористи.
/// </summary>
public class EmailConfirmationToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresOn { get; set; }
    public DateTime CreatedOn { get; set; }
}
