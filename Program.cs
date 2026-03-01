using Microsoft.EntityFrameworkCore;
using SmartSlot.Data;
using SmartSlot.Models;
using SmartSlot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

builder.Services.Configure<TwilioSettings>(
    builder.Configuration.GetSection("Twilio"));

builder.Services.AddScoped<SmsService>();
builder.Services.AddHostedService<ReviewBackgroundService>();
builder.Services.AddScoped<SmartSlot.Services.DistanceService>();
builder.Services.AddScoped<SmartSlot.Services.VerificationService>();
builder.Services.AddHttpClient<SmartSlot.Services.ParkingAIService>();
builder.Services.AddScoped<SmartSlot.Services.ParkingAIService>();


// ✅ Register Brevo EmailService
builder.Services.AddHttpClient<SmartSlot.Services.EmailService>();

builder.Services.AddSession();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Index}/{id?}");

if (app.Environment.IsProduction())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    app.Run();
}
