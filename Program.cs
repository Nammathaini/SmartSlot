using Microsoft.EntityFrameworkCore;
using SmartSlot.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<SmartSlot.Services.DistanceService>();
builder.Services.AddScoped<SmartSlot.Services.PhonePeService>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "parking",
    pattern: "{controller=Parking}/{action=Book}/{slotId?}");

app.Run();