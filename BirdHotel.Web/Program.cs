using BirdHotel.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // ログインページ以外はすべて認証が必要
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<OwnerRepository>();
builder.Services.AddScoped<SpeciesRepository>();
builder.Services.AddScoped<BirdRepository>();
builder.Services.AddScoped<CageRepository>();
builder.Services.AddScoped<ReservationRepository>();
builder.Services.AddScoped<ReservationExportService>();

var app = builder.Build();

// 起動時にテーブルを用意する
app.Services.GetRequiredService<Database>().Initialize();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
