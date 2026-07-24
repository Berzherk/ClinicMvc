using System.Security.Claims;
using ClinicMvc.Models;
using ClinicMvc.Repositories;
using ClinicMvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicMvc.Controllers;

/// <summary>
/// Контролер за најава, одjava и самостојна регистрација/потврда на е-пошта за пациенти.
/// Користи ASP.NET Core Cookie Authentication (не JWT, бидејќи ова е класична MVC веб апликација).
/// </summary>
public class AccountController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPatientAccountService _patientAccountService;
    private readonly IEmailConfirmationService _emailConfirmationService;

    public AccountController(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPatientAccountService patientAccountService,
        IEmailConfirmationService emailConfirmationService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _patientAccountService = patientAccountService;
        _emailConfirmationService = emailConfirmationService;
    }

    /// <summary>GET: /Account/Login - ја прикажува формата за најава.</summary>
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    /// <summary>
    /// POST: /Account/Login
    /// Ги проверува внесените податоци, и ако се точни И е-поштата е потврдена,
    /// креира автентикациско cookie со Claims (Username, Role, DoctorId/PatientId).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userRepository.GetByUsernameAsync(model.Username);

        // Намерно иста порака за "нема корисник" и "погрешна лозинка" -
        // не откриваме дали корисничкото ime постои (безбедносна практика)
        if (user == null || !_passwordHasher.VerifyPassword(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Погрешно корисничко ime или лозинка.");
            return View(model);
        }

        // Само потврдени сметки смеат да се најават (Doctor и Patient сметки бараат потврда).
        if (!user.EmailConfirmed)
        {
            ModelState.AddModelError(string.Empty,
                "Сметката сè уште не е потврдена. Проверете ја вашата е-пошта за линкот за потврда.");
            return View(model);
        }

        // Градиме ги Claims - податоци кои ќе бидат достапни низ целата апликација
        // преку User.Identity и User.Claims во контролерите/View-ovите
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        // DoctorId се додава само ако корисникот е поврзан со доктор
        if (user.DoctorId.HasValue)
        {
            claims.Add(new Claim("DoctorId", user.DoctorId.Value.ToString()));
        }

        // PatientId се додава само ако корисникот е поврзан со пациент
        if (user.PatientId.HasValue)
        {
            claims.Add(new Claim("PatientId", user.PatientId.Value.ToString()));
        }

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Го креираме автентикациското cookie - IsPersistent = false значи
        // сесијата истекува кога прелистувачот ќе се затвори
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = false });

        // Пренасочи го корисникот кон страницата од каде дошол, или кон почетна
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    /// <summary>GET: /Account/Register - формата за самостојна регистрација на пациент.</summary>
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    /// <summary>
    /// POST: /Account/Register - креира Patient + User сметка (Role = Patient) и
    /// испраќа е-пошта за потврда. Сметката останува неактивна додека не се потврди.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (success, errors) = await _patientAccountService.RegisterAsync(model);
        if (!success)
        {
            foreach (var error in errors)
                ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        return View("RegisterConfirmation");
    }

    /// <summary>
    /// GET: /Account/ResendConfirmation - формата за повторно испраќање линк за потврда,
    /// за случаи кога линкот истечил, е-поштата не пристигнала, или SMTP не бил конфигуриран
    /// во моментот на регистрација.
    /// </summary>
    [AllowAnonymous]
    public IActionResult ResendConfirmation()
    {
        return View(new ResendConfirmationViewModel());
    }

    /// <summary>
    /// POST: /Account/ResendConfirmation - секогаш прикажува иста генеричка порака
    /// (без разлика дали сметката постои) - заштита од откривање регистрирани е-пошта адреси.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        ViewBag.Message = await _emailConfirmationService.ResendConfirmationAsync(model.Email);
        return View(model);
    }

    /// <summary>
    /// GET: /Account/ConfirmEmail?token=... - го потврдува линкот испратен по е-пошта
    /// при регистрација (Doctor или Patient). После ова сметката смее да се најави.
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return View("ConfirmEmail", false);

        var (success, error) = await _emailConfirmationService.ConfirmEmailAsync(token);
        ViewBag.Error = error;
        return View("ConfirmEmail", success);
    }

    /// <summary>POST: /Account/Logout - ја брише автентикациската сесија.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    /// <summary>GET: /Account/AccessDenied - страница за "немате пристап".</summary>
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
