using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BasicResetApp.Data;
using BasicResetApp.Models;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// =====================================
// SERVICES
// =====================================
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// =====================================
// SECURITY HEADERS (Disable Back Cache)
// =====================================
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    
    await next();
});

// =====================================
// MIDDLEWARE
// =====================================
// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Home/Error");
//     app.UseHsts();
// }

// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// ✅ Start directly at Identify page
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Identify}/{id?}");


// =====================================
// 🔥 SEED USERS (REAL + BULK)
// =====================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hasher = new PasswordHasher<User>();

    db.Database.EnsureCreated();

    // 🔐 REAL EMAILS
    string[] realEmails =
    {
        "securecheck20s2@gmail.com",
        "maviyamustahsin.edunet@gmail.com",
        "zoyatahreen786@gmail.com",
        "maviya.mustahsin@gmail.com",
        "nabeelamakki@gmail.com",
        "sanakauser017@gmail.com",
        "sfaooratasheen@gmail.com"
    };

    foreach (var email in realEmails)
    {
        if (!db.Users.Any(u => u.Email == email))
        {
            var user = new User { Email = email };
            user.Password = hasher.HashPassword(user, "123456");
            db.Users.Add(user);
        }
    }

    // Seeding skipped for faster cloud booting

    db.SaveChanges();
}

app.Run();
