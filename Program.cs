using Microsoft.AspNetCore.DataProtection;
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
builder.Services.AddHostedService<BackgroundJobService>();
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

// ✅ Store DataProtection keys in PostgreSQL so sessions survive Render redeploys
// Without this, every redeploy wipes the keys and logs out all users
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("SmartSlot");

var app = builder.Build();

// ✅ Run migrations once on startup (kept single — removed duplicate below)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

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
