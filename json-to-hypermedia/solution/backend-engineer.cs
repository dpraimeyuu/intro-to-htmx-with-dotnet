var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content(File.ReadAllText("frontend-engineer.html"), "text/html"));
var currentRefreshingSession = new RefreshingSession();
app.MapGet("/current-time", () => Results.Json(new
{
    time = DateTime.UtcNow,
    refreshingSessionsLimitReached = currentRefreshingSession.HasReachedLimit(),
    _links = new
    {
        reload = new { href = "/" }
    }
}));
app.MapPut("/time/refreshing", () =>
{
    try
    {
        currentRefreshingSession.Use();
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }
    
    return Results.Json(new
    {
        refreshingSessionsLimitReached = currentRefreshingSession.HasReachedLimit()
    });
});

app.MapGet("/hypermedia", () => Results.Content("""
    <h1>HATE JSON Example</h1>
    <script src="https://unpkg.com/htmx.org@1.9.10"></script>
    <div id="time-container"
        hx-get="/current-time/load"
        hx-trigger="load"
        hx-swap="innerHTML"
        hx-target="#time-container"
    >
        Loading current time...
    </div>                                                
""", "text/html"));

app.MapGet("/current-time/load", () =>
{
    return Results.Content($"""
    <div
    id="time"
    >Current Time: {DateTime.UtcNow}</div>
        <a
            hx-get="/current-time/load" 
            hx-swap="innerHTML"
            hx-trigger="click"
            hx-target="#time-container"
        >
            reload
        </a>
        <button
        hx-get="/current-time/refreshing/start" 
        hx-swap="innerHTML"
        hx-trigger="click"
        hx-target="#time"
        id="refreshing"
    >
        start refreshing
    </button>
    """, "text/html");
});

app.MapGet("/current-time/refreshing/start", () =>
{
    try
    {
        currentRefreshingSession.Use();
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
    }

    if (currentRefreshingSession.HasReachedLimit())
    {
        return Results.Content($"""
        <div
            id="time"
        >Current Time: {DateTime.UtcNow}</div>
            <button
            hx-get="/current-time/refreshing/start" 
            hx-swap="innerHTML"
            hx-trigger="click"
            hx-target="#time"
            id="refreshing"
            disabled=true
            hx-swap-oob="true"
        >
            start refreshing
        </button>
        """, "text/html");
    }
    
    return Results.Content($"""
        <div
        hx-get="/current-time/refreshing"
        hx-trigger="every 1s"
        hx-swap="innerHTML"
        hx-target="#time"
        id="time"
    >Current Time: {DateTime.UtcNow}</div>
        <button
        hx-get="/current-time/refreshing/stop" 
        hx-swap="innerHTML"
        hx-trigger="click"
        hx-target="#time"
        id="refreshing"
        hx-swap-oob="true"
    >
        stop refreshing
    </button>
    """, "text/html");
});


app.MapGet("/current-time/refreshing", () => Results.Content($"""
  <div
  hx-get="/current-time/refreshing"
  hx-trigger="every 1s"
  hx-swap="innerHTML"
  hx-target="#time"
  id="time"
>Current Time: {DateTime.UtcNow}</div>
""", "text/html"));

app.MapGet("/current-time/refreshing/stop", () =>
{
    return Results.Content($"""
    <div
        id="time"
    >Current Time: {DateTime.UtcNow}</div>
        <button
        hx-get="/current-time/refreshing/start" 
        hx-swap="innerHTML"
        hx-trigger="click"
        hx-target="#time"
        hx-swap-oob="true"
        id="refreshing"
    >
        start refreshing
    </button>
    """, "text/html");
    });

app.Run();

class RefreshingSession()
{
    private int _usage = 0;

    public void Use()
    {
        if(HasReachedLimit()) throw new InvalidOperationException("Refreshing sessions limit reached.");
        _usage++;
    }
    public bool HasReachedLimit() => _usage >= 5;
}
