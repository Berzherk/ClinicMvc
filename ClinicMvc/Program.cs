using ClinicMvc.Data;
using ClinicMvc.Middleware;
using ClinicMvc.Repositories;
using ClinicMvc.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// MVC - со глобален филтер кој бара најава за СИТЕ акции по default.
// Контролери/акции означени со [AllowAnonymous] (Login, AccessDenied, Error) остануваат отворени.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));
});

// Потребно за да ICurrentUserService може да пристапи до HttpContext
builder.Services.AddHttpContextAccessor();

// Firebird конекција
builder.Services.AddSingleton<IDbConnectionFactory, FirebirdConnectionFactory>();

// Репозитории
builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IEmailConfirmationTokenRepository, EmailConfirmationTokenRepository>();

// Сервиси за автентикација/авторизација
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ── Е-пошта - поставките доаѓаат од секцијата "Smtp" (appsettings / User Secrets / env vars) ──
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();

// Services слој - ја содржи бизнис логиката, контролерите само ги повикуваат
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IPatientAccountService, PatientAccountService>();
builder.Services.AddScoped<IExportService, ExportService>();

// Логер за грешки
builder.Services.AddSingleton<IErrorLogger, FileErrorLogger>();

// ── Автентикација - Cookie-based (класична MVC веб апликација) ──
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// ── Авторизација - улогите се проверуваат преку [Authorize(Roles = "...")] во контролерите ──
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseGlobalExceptionHandling();

// ВАЖНО: Authentication МОРА да е пред Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
