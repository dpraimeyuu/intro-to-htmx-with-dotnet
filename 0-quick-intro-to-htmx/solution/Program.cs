var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content("""
    <script src="https://unpkg.com/htmx.org@1.9.10"></script>
    <div id="container">Nothing :-(</div>
    <button
        hx-trigger="click"
        hx-get="/something"
        hx-swap="innerHTML"
        hx-target="#container"
    >Get something</button>
""", "text/html"));

app.MapGet("/something", () => Results.Content("""
  <div id="container">Something :-)<div>
""", "text/html"));

app.Run();