using Aloe.Apps.DummyService;

// WindowsサービスはSCM起動時に作業ディレクトリがSystem32になるため、
// 実行ファイルのディレクトリに明示的に変更する
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => { options.ServiceName = "Aloe.Apps.DummyService"; });
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
