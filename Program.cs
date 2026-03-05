using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SmartSlot.Data;
using SmartSlot.Hubs;
using SmartSlot.Models;
using SmartSlot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// ✅ SignalR — real-time slot updates
builder.Services.AddSignalR();

builder.Services.Configure<TwilioSettings>(
    builder.Configuration.GetSection("Twilio"));

builder.Services.AddScoped<SmsService>();

builder.Services.AddHostedService<BackgroundJobService>();
builder.Services.AddScoped<SmartSlot.Services.DistanceService>();
builder.Services.AddScoped<SmartSlot.Services.VerificationService>();
builder.Services.AddHttpClient<SmartSlot.Services.ParkingAIService>();
builder.Services.AddScoped<SmartSlot.Services.ParkingAIService>();
builder.Services.AddHttpClient<SmartSlot.Services.EmailService>();

// ✅ Browser Web Push notifications (VAPID)
builder.Services.AddScoped<SmartSlot.Services.PushNotificationService>();

builder.Services.AddSession();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// ✅ Store DataProtection keys in PostgreSQL — survives Render redeploys
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("SmartSlot");

var app = builder.Build();

// ✅ Run EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

// ✅ Keep-alive — UptimeRobot pings this every 5 mins to prevent Render sleep
app.MapGet("/ping", () => Results.Ok(new
{
    status = "alive",
    time = DateTime.UtcNow.AddHours(5.5).ToString("hh:mm tt dd MMM")
}));

// ✅ Expose VAPID public key to frontend (so _Layout.cshtml doesn't need to hardcode)
app.MapGet("/push-public-key", (IConfiguration config) =>
    Results.Ok(new { key = config["Push:VapidPublicKey"] ?? "" }));

// ✅ SignalR hub
app.MapHub<ParkingHub>("/parkingHub");

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
