using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<IdleTrackingService>();

// Enable Windows Service mode
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Idle Tracker Service";
});

// Bind config from appsettings.json
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<IdleOptions>(builder.Configuration.GetSection("Idle"));

var host = builder.Build();
host.Run();
