var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content(File.ReadAllText("frontendowiec.html"), "text/html"));
app.MapGet("/current-time", () => Results.Json(new
{
    time = DateTime.UtcNow,
    _links = new
    {
        reload = new { href = "/" }
    }
}));

app.Run();