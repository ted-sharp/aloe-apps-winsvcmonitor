using Aloe.Apps.DummyService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => { options.ServiceName = "Aloe.Apps.DummyService"; });
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
