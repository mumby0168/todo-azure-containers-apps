var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "ToBeDone.Services.Work");

app.Run();
