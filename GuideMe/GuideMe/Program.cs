using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});
var app = builder.Build();

// In-memory storage
var tours = new Dictionary<int, Tour>();
var checkpoints = new Dictionary<int, Checkpoint>();
int tourIdCounter = 1;
int checkpointIdCounter = 1;

// Serve static files (for our HTML page)
app.UseStaticFiles();

// Home page
app.MapGet("/", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) => 
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Content(GetHomePage(tokens.RequestToken!), "text/html");
});

// Get all tours
app.MapGet("/tours", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Content(GetToursListHtml(tokens.RequestToken!), "text/html");
});

// Create a new tour
app.MapPost("/tours", (IAntiforgery antiforgery, HttpContext httpContext, [FromForm] string name) =>
{
    var tokens = antiforgery.GetAndStoreTokens(httpContext);
    var tour = new Tour { Id = tourIdCounter++, Name = name };
    tours[tour.Id] = tour;
    
    return Results.Content(GetToursListHtml(tokens.RequestToken!), "text/html");
});

// Delete a tour
app.MapDelete("/tours/{tourId}", (int tourId, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
{
    // Remove all checkpoints for this tour
    var tourCheckpoints = checkpoints.Values.Where(c => c.TourId == tourId).ToList();
    foreach (var cp in tourCheckpoints)
    {
        checkpoints.Remove(cp.Id);
    }
    
    tours.Remove(tourId);
    
    var tokens = antiforgery.GetAndStoreTokens(context);
    // Return updated tours list
    return Results.Content(GetToursListHtml(tokens.RequestToken!), "text/html");
});

// Get checkpoints for a tour
app.MapGet("/tours/{tourId}/checkpoints", (int tourId, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    var tourCheckpoints = checkpoints.Values
        .Where(c => c.TourId == tourId)
        .OrderBy(c => c.Order)
        .ToList();
    
    return Results.Content(GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!), "text/html");
});

// Add a checkpoint
app.MapPost("/tours/{tourId}/checkpoints", (int tourId, string name, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
{
    var maxOrder = checkpoints.Values
        .Where(c => c.TourId == tourId)
        .Select(c => (int?)c.Order)
        .Max() ?? -1;
    
    var checkpoint = new Checkpoint 
    { 
        Id = checkpointIdCounter++, 
        TourId = tourId, 
        Name = name,
        Order = maxOrder + 1
    };
    checkpoints[checkpoint.Id] = checkpoint;
    
    var tokens = antiforgery.GetAndStoreTokens(context);
    var tourCheckpoints = checkpoints.Values
        .Where(c => c.TourId == tourId)
        .OrderBy(c => c.Order)
        .ToList();
    
    return Results.Content(GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!), "text/html");
});

// Delete a checkpoint
app.MapDelete("/checkpoints/{checkpointId}", (int checkpointId, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
{
    if (checkpoints.TryGetValue(checkpointId, out var checkpoint))
    {
        var tourId = checkpoint.TourId;
        checkpoints.Remove(checkpointId);
        
        // Reorder remaining checkpoints
        var remaining = checkpoints.Values
            .Where(c => c.TourId == tourId)
            .OrderBy(c => c.Order)
            .ToList();
        
        for (int i = 0; i < remaining.Count; i++)
        {
            remaining[i].Order = i;
        }
        
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Content(GetCheckpointsHtml(tourId, remaining, tokens.RequestToken!), "text/html");
    }
    
    return Results.NotFound();
});

// Move checkpoint before another
app.MapPost("/checkpoints/{checkpointId}/move-before/{targetId}", (int checkpointId, int targetId, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
{
    if (checkpoints.TryGetValue(checkpointId, out var checkpoint) && 
        checkpoints.TryGetValue(targetId, out var target) &&
        checkpoint.TourId == target.TourId)
    {
        var tourId = checkpoint.TourId;
        var tourCheckpoints = checkpoints.Values
            .Where(c => c.TourId == tourId)
            .OrderBy(c => c.Order)
            .ToList();
        
        tourCheckpoints.Remove(checkpoint);
        var targetIndex = tourCheckpoints.IndexOf(target);
        tourCheckpoints.Insert(targetIndex, checkpoint);
        
        for (int i = 0; i < tourCheckpoints.Count; i++)
        {
            tourCheckpoints[i].Order = i;
        }
        
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Content(GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!), "text/html");
    }
    
    return Results.NotFound();
});

// Move checkpoint after another
app.MapPost("/checkpoints/{checkpointId}/move-after/{targetId}", (int checkpointId, int targetId, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
{
    if (checkpoints.TryGetValue(checkpointId, out var checkpoint) && 
        checkpoints.TryGetValue(targetId, out var target) &&
        checkpoint.TourId == target.TourId)
    {
        var tourId = checkpoint.TourId;
        var tourCheckpoints = checkpoints.Values
            .Where(c => c.TourId == tourId)
            .OrderBy(c => c.Order)
            .ToList();
        
        tourCheckpoints.Remove(checkpoint);
        var targetIndex = tourCheckpoints.IndexOf(target);
        tourCheckpoints.Insert(targetIndex + 1, checkpoint);
        
        for (int i = 0; i < tourCheckpoints.Count; i++)
        {
            tourCheckpoints[i].Order = i;
        }
        
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Content(GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!), "text/html");
    }
    
    return Results.NotFound();
});

app.UseAntiforgery();

app.Run();

// Helper functions
string GetHomePage(string antiforgeryToken)
{
    return $"""

           <!DOCTYPE html>
           <html lang="en">
           <head>
               <meta charset="UTF-8">
               <meta name="viewport" content="width=device-width, initial-scale=1.0">
               <title>Tour Planner</title>
               <script src="https://unpkg.com/htmx.org@1.9.10"></script>
               <script src="https://cdn.tailwindcss.com"></script>
           </head>
           <body class="bg-gray-100 min-h-screen py-8">
               <div class="container mx-auto px-4 max-w-4xl">
                   <h1 class="text-3xl font-bold text-gray-800 mb-8">Tour Planner</h1>
                   
                   <div class="bg-white p-6 rounded-lg shadow mb-6">
                       <h2 class="text-xl font-semibold mb-4">Create New Tour</h2>
                       <form hx-post="/tours" hx-target="#tours-list" hx-swap="innerHTML" hx-on::after-request="this.reset()">
                           <input type="hidden" name="__RequestVerificationToken" value="{antiforgeryToken}" />
                           <div class="flex gap-2">
                               <input type="text" name="name" placeholder="Tour name" required 
                                      class="flex-1 px-4 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500">
                               <button type="submit" class="bg-blue-500 text-white px-6 py-2 rounded hover:bg-blue-600">
                                   Create Tour
                               </button>
                           </div>
                       </form>
                   </div>
                   
                   <div id="tours-list" hx-get="/tours" hx-trigger="load" hx-swap="innerHTML">
                       Loading tours...
                   </div>
               </div>
           </body>
           </html>
           """;
}

string GetToursListHtml(string antiforgeryToken)
{
    var html = "";
    foreach (var tour in tours.Values.OrderBy(t => t.Id))
    {
        html += $@"
            <div class=""bg-white p-4 rounded-lg shadow mb-4"">
                <div class=""flex justify-between items-center mb-2"">
                    <h3 class=""text-lg font-semibold"">{tour.Name}</h3>
                    <button 
                        hx-delete=""/tours/{tour.Id}"" 
                        hx-target=""#tours-list""
                        hx-swap=""innerHTML""
                        hx-headers='{{""X-CSRF-TOKEN"": ""{antiforgeryToken}""}}'
                        class=""bg-red-500 text-white px-3 py-1 rounded hover:bg-red-600"">
                        Delete Tour
                    </button>
                </div>
                <div id=""checkpoints-{tour.Id}"" hx-get=""/tours/{tour.Id}/checkpoints"" hx-trigger=""load"" hx-swap=""innerHTML"">
                    Loading checkpoints...
                </div>
                <form hx-post=""/tours/{tour.Id}/checkpoints"" hx-target=""#checkpoints-{tour.Id}"" hx-swap=""innerHTML"" class=""mt-3"">
                    <input type=""hidden"" name=""__RequestVerificationToken"" value=""{antiforgeryToken}"" />
                    <div class=""flex gap-2"">
                        <input type=""text"" name=""name"" placeholder=""Checkpoint name"" required 
                               class=""flex-1 px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"">
                        <button type=""submit"" class=""bg-green-500 text-white px-4 py-2 rounded hover:bg-green-600"">
                            Add Checkpoint
                        </button>
                    </div>
                </form>
            </div>";
    }
    
    return html;
}

string GetCheckpointsHtml(int tourId, List<Checkpoint> tourCheckpoints, string antiforgeryToken)
{
    if (tourCheckpoints.Count == 0)
    {
        return @"<p class=""text-gray-500 text-sm"">No checkpoints yet. Add your first checkpoint below!</p>";
    }
    
    var html = @"<div class=""space-y-2"">";
    
    foreach (var cp in tourCheckpoints)
    {
        html += $@"
            <div class=""flex items-center gap-2 bg-gray-50 p-3 rounded"">
                <span class=""flex-1 font-medium"">{cp.Order + 1}. {cp.Name}</span>
                <div class=""flex gap-1"">
                    <select onchange=""if(this.value) {{ htmx.ajax('POST', this.value, {{target: '#checkpoints-{tourId}', swap: 'innerHTML', headers: {{'X-CSRF-TOKEN': '{antiforgeryToken}'}}}}); this.value=''; }}"" 
                            class=""text-sm px-2 py-1 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"">
                        <option value="""">Move...</option>";
        
        foreach (var other in tourCheckpoints.Where(c => c.Id != cp.Id))
        {
            html += $@"
                        <option value=""/checkpoints/{cp.Id}/move-before/{other.Id}"">Before: {other.Name}</option>
                        <option value=""/checkpoints/{cp.Id}/move-after/{other.Id}"">After: {other.Name}</option>";
        }
        
        html += $@"
                    </select>
                    <button 
                        hx-delete=""/checkpoints/{cp.Id}"" 
                        hx-target=""#checkpoints-{tourId}""
                        hx-swap=""innerHTML""
                        hx-headers='{{""X-CSRF-TOKEN"": ""{antiforgeryToken}""}}'
                        class=""bg-red-500 text-white px-3 py-1 rounded hover:bg-red-600 text-sm"">
                        Remove
                    </button>
                </div>
            </div>";
    }
    
    html += "</div>";
    return html;
}

// Models
record Tour
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

record Checkpoint
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public string Name { get; set; } = "";
    public int Order { get; set; }
}