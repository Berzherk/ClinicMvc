using System.ComponentModel.DataAnnotations;

namespace ClinicMvc.Models;

/// <summary>
/// Модел за корисник (Administrator или Doctor) - одговара на табелата USERS.
/// Се користи за автентикација и авторизација.
/// </summary>
public class User
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Корисничкото ime е задолжително")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// BCrypt хеш на лозинката - НИКОГАШ не се чува plain-text лозинка.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Улога: "Administrator", "Doctor" или "Patient" (CHECK constraint во базата)</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// NULL за администратори и пациенти. Поставено за докторски сметки - врска кон DOCTORS.ID.
    /// </summary>
    public int? DoctorId { get; set; }

    /// <summary>
    /// NULL за администратори и доктори. Поставено за пациентски сметки - врска кон PATIENTS.ID.
    /// </summary>
    public int? PatientId { get; set; }

    /// <summary>Е-пошта адреса на корисникот - задолжителна за Doctor и Patient сметки.</summary>
    [Required(ErrorMessage = "Е-поштата е задолжителна")]
    [EmailAddress(ErrorMessage = "Внесете валидна е-пошта адреса")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Дали е-поштата е потврдена преку линкот испратен при регистрација.
    /// Сметките со EmailConfirmed = false не смеат да се најават.
    /// </summary>
    public bool EmailConfirmed { get; set; }

    // Audit полиња
    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Навигационо поле - целосните податоци за поврзаниот доктор.
    /// Не се вчитува автоматски преку Dapper - го полни репозиториумот рачно по потреба.
    /// </summary>
    public Doctor? Doctor { get; set; }

    /// <summary>
    /// Навигационо поле - целосните податоци за поврзаниот пациент.
    /// Не се вчитува автоматски преку Dapper - го полни репозиториумот рачно по потреба.
    /// </summary>
    public Patient? Patient { get; set; }
}
