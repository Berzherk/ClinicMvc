namespace ClinicMvc.Models;

/// <summary>Филтри за пребарување на записите во AuditLogs.</summary>
public class AuditLogFilter
{
    /// <summary>CREATE, UPDATE или DELETE</summary>
    public string? ActionType { get; set; }

    /// <summary>Doctor, Patient, Appointment, User</summary>
    public string? EntityName { get; set; }

    /// <summary>Корисничко ime - делумно пребарување</summary>
    public string? Username { get; set; }

    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
