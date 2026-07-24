using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClinicMvc.Models;

/// <summary>ViewModel за пациентскиот почетен dashboard - идни термини + историја.</summary>
public class PatientDashboardViewModel
{
    public IEnumerable<Appointment> UpcomingAppointments { get; set; } = Enumerable.Empty<Appointment>();
    public IEnumerable<Appointment> AppointmentHistory { get; set; } = Enumerable.Empty<Appointment>();
}

/// <summary>ViewModel за страницата за прелистување и резервирање слободни термини.</summary>
public class AvailableSlotsViewModel
{
    public FreeSlotsFilter Filter { get; set; } = new();
    public List<SelectListItem> Specialties { get; set; } = new();
    public List<SelectListItem> Doctors { get; set; } = new();
}

/// <summary>ViewModel за уредување на сопствен профил (пациент).</summary>
public class PatientProfileViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Името е задолжително")]
    [Display(Name = "Ime")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Презимето е задолжително")]
    [Display(Name = "Презиме")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ЕМБГ е задолжителен")]
    [StringLength(13, MinimumLength = 13, ErrorMessage = "ЕМБГ мора да содржи точно 13 цифри")]
    [Display(Name = "ЕМБГ")]
    public string Embg { get; set; } = string.Empty;

    [Display(Name = "Телефон")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Внесете валидна е-пошта адреса")]
    [Display(Name = "Е-пошта")]
    public string? Email { get; set; }
}
