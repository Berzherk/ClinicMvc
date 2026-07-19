namespace ClinicMvc.Models;

/// <summary>Филтри за пребарување на пациенти на листата.</summary>
public class PatientFilter
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Embg { get; set; }
}
