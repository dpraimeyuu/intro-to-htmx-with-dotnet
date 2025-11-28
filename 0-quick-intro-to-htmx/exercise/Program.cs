var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content("""
    <div id="container">Nothing :-(</div>
    <button
    >Get something</button>
""", "text/html"));

app.Run();