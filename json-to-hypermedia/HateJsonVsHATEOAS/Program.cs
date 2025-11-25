var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content(File.ReadAllText("index.html"), "text/html"));
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

app.MapGet("/current-time/load", () => Results.Content($"""
    {Components.CurrentTime.Static()}
    <a
        hx-get="/current-time/load" 
        hx-swap="innerHTML"
        hx-trigger="click"
        hx-target="#time-container"
    >
        reload
    </a>
    {Components.StartRefreshingLink()}
""", "text/html"));

app.MapGet("/current-time/refreshing/start", () =>
{
    return Results.Content($"""
                                {Components.CurrentTime.Refreshing()}
                                {Components.StopRefreshingLink()}
                            """, "text/html");
});


app.MapGet("/current-time/refreshing", () => Results.Content($"""
    {Components.CurrentTime.Refreshing()}
""", "text/html"));

app.MapGet("/current-time/refreshing/stop", () =>
{
    return Results.Content($"""
                                {Components.CurrentTime.Static()}
                                {Components.StartRefreshingLink(forSwapping: true)}
                            """, "text/html");

});

app.Run();

class RefreshingSession(int Usage = 0)
{
    public void Use()
    {
        if(HasReachedLimit()) throw new InvalidOperationException("Refreshing sessions limit reached.");
        Usage++;
    }
    public bool HasReachedLimit() => Usage >= 5;
}

public static class Components
{
    public static string StartRefreshingLink(bool forSwapping = false)
    {
        string swapping = forSwapping? "hx-swap-oob=\"true\"" : "";
        return $"""
                <button
                    hx-get="/current-time/refreshing/start" 
                    hx-swap="innerHTML"
                    hx-trigger="click"
                    hx-target="#time"
                    {swapping}
                    id="refreshing"
                >
                    start refreshing
                </button>
                """;
    }

    public static string StopRefreshingLink()
    {
        return """
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
               """;
    }

    public static class CurrentTime
    {
        public static string Refreshing()
        {
            return $"""
                    <div
                        hx-get="/current-time/refreshing"
                        hx-trigger="every 1s"
                        hx-swap="innerHTML"
                        hx-target="#time"
                        id="time"
                    >Current Time: {DateTime.UtcNow}</div>
                    """;
        }

        public static string Static()
        {
            return $"""
                    <div
                        id="time"
                    >Current Time: {DateTime.UtcNow}</div>
                    """;
        }
    }
}
