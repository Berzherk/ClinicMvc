using ClinicMvc.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>Преглед на сите CRUD активности - достапен само за Administrator.</summary>
[Authorize(Roles = "Administrator")]
public class AuditLogsController : Controller
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogsController(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<IActionResult> Index()
    {
        var logs = await _auditLogRepository.GetAllAsync();
        return View(logs);
    }
}
