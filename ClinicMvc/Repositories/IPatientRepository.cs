using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

public interface IPatientRepository
{
    Task<IEnumerable<Patient>> GetAllAsync();

    /// <summary>Странирана листа на активни пациенти. page почнува од 1.</summary>
    Task<IEnumerable<Patient>> GetPagedAsync(int page, int pageSize);

    /// <summary>Вкупен број активни (не-избришани) пациенти.</summary>
    Task<int> CountAsync();

    /// <summary>Странирано пребарување со филтри (Ime/Презиме/ЕМБГ).</summary>
    Task<IEnumerable<Patient>> SearchPagedAsync(PatientFilter filter, int page, int pageSize);

    /// <summary>Вкупен број пациенти кои одговараат на филтрите.</summary>
    Task<int> SearchCountAsync(PatientFilter filter);

    Task<Patient?> GetByIdAsync(int id);
    Task<int> CreateAsync(Patient patient, string createdBy);
    Task UpdateAsync(Patient patient, string modifiedBy);
    Task DeleteAsync(int id, string modifiedBy);
    Task<bool> EmbgExistsAsync(string embg, int excludeId = 0);

    /// <summary>
    /// Ги враќа сите претходни термини/прегледи за конкретен пациент - медицинска историја.
    /// </summary>
    Task<IEnumerable<Appointment>> GetMedicalHistoryAsync(int patientId);
}
