using lms.Interfaces;
using lms.Mappings;
using lms.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PackageMapper>();
builder.Services.AddScoped<IPackageService, PackageService>();

var app = builder.Build();

app.MapControllers();

app.Run();
