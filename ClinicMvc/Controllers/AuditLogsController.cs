using ClinicMvc.Models;
using ClinicMvc.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>Преглед на сите CRUD активности - достапен само за Administrator.</summary>
[Authorize(Roles = "Administrator")]
public class AuditLogsController : Controller
{
    private readonly IAuditLogRepository _auditLogRepository;

    private const int PageSize = 15;

    public AuditLogsController(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    /// <summary>GET: /AuditLogs?page=2&amp;ActionType=... - филтрирана и странирана листа, 15 по страница.</summary>
    public async Task<IActionResult> Index(AuditLogFilter filter, int page = 1)
    {
        var validPage = page < 1 ? 1 : page;

        var logs       = await _auditLogRepository.SearchPagedAsync(filter, validPage, PageSize);
        var totalCount = await _auditLogRepository.SearchCountAsync(filter);
        var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        ViewBag.Filter = filter;
        ViewBag.Pagination = new PaginationInfo
        {
            CurrentPage = validPage,
            TotalPages  = totalPages == 0 ? 1 : totalPages,
            TotalCount  = totalCount,
            PageSize    = PageSize
        };

        return View(logs);
    }
}
