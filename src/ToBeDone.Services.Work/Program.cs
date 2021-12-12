var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddApplicationInsights();
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

app.MapGet("/", () => "ToBeDone.Services.Work");

app.Run();
