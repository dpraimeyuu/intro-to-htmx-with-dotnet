using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddSingleton<ToursRepository, FileToursRepository>();
var app = builder.Build();

// Serve static files (for our HTML page)
app.UseStaticFiles();

// Home page
app.MapGet("/", (IAntiforgery antiforgery, HttpContext context) => 
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Content(GetHomePage(tokens.RequestToken!), "text/html");
});

// Handle /tours/{tourId} URLs (for map browsing)
app.MapGet("/tours/{tourId}", (int tourId, IAntiforgery antiforgery, HttpContext context) => 
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Content(GetHomePage(tokens.RequestToken!), "text/html");
});

// Get all tours
app.MapGet("/tours", (ToursRepository repository, IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Content(GetToursListHtml(repository, tokens.RequestToken!), "text/html");
});

// Create a new tour
app.MapPost("/tours", (ToursRepository repository, IAntiforgery antiforgery, HttpContext httpContext, [FromForm] string name) =>
{
    var tokens = antiforgery.GetAndStoreTokens(httpContext);
    repository.CreateTour(name);
    
    return Results.Content(GetToursListHtml(repository, tokens.RequestToken!), "text/html");
});

// Delete a tour
app.MapDelete("/tours/{tourId}", (ToursRepository repository, int tourId, IAntiforgery antiforgery, HttpContext context) =>
{
    repository.DeleteTour(tourId);
    
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Content(GetToursListHtml(repository, tokens.RequestToken!), "text/html");
});

// Get checkpoints for a tour
app.MapGet("/tours/{tourId}/checkpoints", (ToursRepository repository, int tourId, IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    var tourCheckpoints = repository.GetCheckpointsForTour(tourId).ToList();
    
    return Results.Content(GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!), "text/html");
});

// Get checkpoints as JSON for map display
app.MapGet("/tours/{tourId}/checkpoints-json", (ToursRepository repository, int tourId) =>
{
    var tourCheckpoints = repository.GetCheckpointsForTour(tourId)
        .Select(cp => new {
            id = cp.Id,
            name = cp.Name,
            latitude = cp.Latitude,
            longitude = cp.Longitude
        })
        .ToList();
    
    return Results.Json(tourCheckpoints);
});

// Add a checkpoint
app.MapPost("/tours/{tourId}/checkpoints", (ToursRepository repository, int tourId, [FromForm] string name, IAntiforgery antiforgery, HttpContext context) =>
{
    repository.CreateCheckpoint(tourId, name);
    
    var tokens = antiforgery.GetAndStoreTokens(context);
    var tourCheckpoints = repository.GetCheckpointsForTour(tourId).ToList();
    
    return Results.Content(GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!), "text/html");
});

// Delete a checkpoint
app.MapDelete("/checkpoints/{checkpointId}", (ToursRepository repository, int checkpointId, IAntiforgery antiforgery, HttpContext context) =>
{
    var checkpoint = repository.GetCheckpoint(checkpointId);
    if (checkpoint == null)
        return Results.NotFound();
    
    var tourId = checkpoint.TourId;
    repository.DeleteCheckpoint(checkpointId);
    
    var tokens = antiforgery.GetAndStoreTokens(context);
    var remaining = repository.GetCheckpointsForTour(tourId).ToList();
    
    // Add HX-Trigger header to trigger notification event
    context.Response.Headers["HX-Trigger"] = """{"showNotification": {"message": "Checkpoint deleted", "duration": 2000}}""";
    
    return Results.Content(GetCheckpointsHtml(tourId, remaining, tokens.RequestToken!), "text/html");
});

// Move checkpoint before another
app.MapPost("/checkpoints/{checkpointId}/move-before/{targetId}", (ToursRepository repository, int checkpointId, int targetId, IAntiforgery antiforgery, HttpContext context) =>
{
    if (!repository.MoveCheckpointBefore(checkpointId, targetId))
        return Results.NotFound();
    
    var checkpoint = repository.GetCheckpoint(checkpointId);
    var tourId = checkpoint!.TourId;
    var tourCheckpoints = repository.GetCheckpointsForTour(tourId).ToList();
    
    var tokens = antiforgery.GetAndStoreTokens(context);
    var html = GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!);
    
    // Add HX-Trigger header to trigger notification event
    context.Response.Headers["HX-Trigger"] = """{"showNotification": {"message": "Successfully moved checkpoint up", "duration": 2000}}""";
    
    return Results.Content(html, "text/html");
});

// Move checkpoint after another
app.MapPost("/checkpoints/{checkpointId}/move-after/{targetId}", (ToursRepository repository, int checkpointId, int targetId, IAntiforgery antiforgery, HttpContext context) =>
{
    if (!repository.MoveCheckpointAfter(checkpointId, targetId))
        return Results.NotFound();
    
    var checkpoint = repository.GetCheckpoint(checkpointId);
    var tourId = checkpoint!.TourId;
    var tourCheckpoints = repository.GetCheckpointsForTour(tourId).ToList();
    
    var tokens = antiforgery.GetAndStoreTokens(context);
    var html = GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!);
    
    // Add HX-Trigger header to trigger notification event
    context.Response.Headers["HX-Trigger"] = """{"showNotification": {"message": "Successfully moved checkpoint down", "duration": 2000}}""";
    
    return Results.Content(html, "text/html");
});

// Set checkpoint location
app.MapPost("/checkpoints/{checkpointId}/location", async (ToursRepository repository, int checkpointId, HttpContext context, IAntiforgery antiforgery) =>
{
    var checkpoint = repository.GetCheckpoint(checkpointId);
    if (checkpoint == null)
        return Results.NotFound();
    
    var json = await JsonDocument.ParseAsync(context.Request.Body);
    var latitude = json.RootElement.GetProperty("latitude").GetDouble();
    var longitude = json.RootElement.GetProperty("longitude").GetDouble();
    
    repository.UpdateCheckpointLocation(checkpointId, latitude, longitude);
    
    var tourId = checkpoint.TourId;
    var tokens = antiforgery.GetAndStoreTokens(context);
    var tourCheckpoints = repository.GetCheckpointsForTour(tourId).ToList();
    
    return Results.Content(GetCheckpointsHtml(tourId, tourCheckpoints, tokens.RequestToken!), "text/html");
});

app.UseAntiforgery();

app.Run();

// Helper functions
string GetHomePage(string antiforgeryToken)
{
    return @$"
           <!DOCTYPE html>
           <html lang=""en"">
           <head>
               <meta charset=""UTF-8"">
               <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
               <title>Tour Planner</title>
               <script src=""https://unpkg.com/htmx.org@1.9.10""></script>
               <script src=""https://cdn.tailwindcss.com""></script>
               <link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
               <script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
               <style>
                   #map {{ height: calc(100vh - 4rem); }}
               </style>
           </head>
           <body class=""bg-gray-100 min-h-screen py-8"" hx-ext=""debug"">
               <script>
                   // Enable htmx logging
                   htmx.logAll();
                   
                   // Add custom event listeners for debugging
                   document.body.addEventListener('htmx:beforeRequest', function(evt) {{
                       console.log('HTMX Request Starting:', {{
                           target: evt.detail.target,
                           verb: evt.detail.verb,
                           path: evt.detail.path,
                           headers: evt.detail.headers,
                           parameters: evt.detail.parameters
                       }});
                   }});
                   
                   document.body.addEventListener('htmx:afterRequest', function(evt) {{
                       console.log('HTMX Request Complete:', {{
                           successful: evt.detail.successful,
                           status: evt.detail.xhr.status,
                           response: evt.detail.xhr.responseText.substring(0, 200) + '...'
                       }});
                   }});
                   
                   document.body.addEventListener('htmx:responseError', function(evt) {{
                       console.error('HTMX Response Error:', {{
                           status: evt.detail.xhr.status,
                           statusText: evt.detail.xhr.statusText,
                           response: evt.detail.xhr.responseText
                       }});
                   }});
                   
                   document.body.addEventListener('htmx:sendError', function(evt) {{
                       console.error('HTMX Send Error:', evt.detail);
                   }});
                   
                   document.body.addEventListener('htmx:swapError', function(evt) {{
                       console.error('HTMX Swap Error:', evt.detail);
                   }});
                   
                   // Listen for custom showNotification events triggered by HX-Trigger header
                   document.body.addEventListener('showNotification', function(evt) {{
                       if (evt.detail && evt.detail.message) {{
                           showNotification(evt.detail.message, evt.detail.duration || 3000);
                       }}
                   }});
               </script>
               <div class=""container mx-auto px-4"" style=""max-width: 1400px;"">
                   <h1 class=""text-3xl font-bold text-gray-800 mb-8"">Tour Planner</h1>
                   
                   <div class=""flex gap-6 transition-all duration-500 ease-in-out"" id=""main-container"">
                       <!-- Left Panel: Tours and Checkpoints -->
                       <div id=""tours-panel"" class=""transition-all duration-500 ease-in-out"" style=""flex: 1; max-width: 100%; margin: 0 auto;"">
                           <div class=""bg-white p-6 rounded-lg shadow mb-6"">
                               <h2 class=""text-xl font-semibold mb-4"">Create New Tour</h2>
                               <form hx-post=""/tours"" hx-target=""#tours-list"" hx-swap=""innerHTML"" hx-on::after-request=""this.reset()"">
                                   <input type=""hidden"" name=""__RequestVerificationToken"" value=""{antiforgeryToken}"" />
                                   <div class=""flex gap-2"">
                                       <input type=""text"" name=""name"" placeholder=""Tour name"" required 
                                              class=""flex-1 px-4 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"">
                                       <button type=""submit"" class=""bg-blue-500 text-white px-6 py-2 rounded hover:bg-blue-600"">
                                           Create Tour
                                       </button>
                                   </div>
                               </form>
                           </div>
                           
                           <div id=""tours-list"" hx-get=""/tours"" hx-trigger=""load"" hx-swap=""innerHTML"">
                               Loading tours...
                           </div>
                       </div>
                       
                       <!-- Right Panel: Map -->
                       <div id=""map-panel"" class=""bg-white rounded-lg shadow overflow-hidden transition-all duration-500 ease-in-out"" 
                            style=""flex: 0 0 0; opacity: 0; overflow: hidden;"">
                           <div class=""p-4 border-b bg-gray-50 flex justify-between items-center"">
                               <h3 id=""map-panel-title"" class=""font-semibold text-gray-700"">Tour Map</h3>
                               <button onclick=""hideMap()"" 
                                       class=""text-gray-500 hover:text-gray-700"">
                                   ✕
                               </button>
                           </div>
                           <div id=""map"" style=""height: calc(100vh - 12rem);""></div>
                       </div>
                   </div>
               </div>
               
               <!-- Notification toast -->
               <div id=""notification"" class=""fixed bottom-8 left-1/2 transform -translate-x-1/2 bg-blue-600 text-white px-6 py-3 rounded-lg shadow-lg hidden transition-opacity duration-300"" style=""z-index: 9999;"">
                   <span id=""notification-text""></span>
               </div>
               
               <!-- Hidden div for OOB notification triggers -->
               <div id=""notification-trigger"" style=""display:none;""></div>
               
               <script>
                   // Show notification
                   function showNotification(message, duration = 3000) {{
                       var notif = document.getElementById('notification');
                       var text = document.getElementById('notification-text');
                       text.textContent = message;
                       notif.classList.remove('hidden');
                       setTimeout(function() {{
                           notif.classList.add('hidden');
                       }}, duration);
                   }}
               </script>
               
               <script>
                   // Initialize map centered on Gdańsk Old Town
                   var map = null;
                   var markers = {{}};
                   var selectedCheckpoint = null;
                   var activeTourId = null;
                   
                   // Function to initialize map (lazy loading)
                   function initializeMap() {{
                       if (!map) {{
                           map = L.map('map').setView([54.3520, 18.6466], 13);
                           L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                               attribution: '© OpenStreetMap contributors',
                               maxZoom: 19
                           }}).addTo(map);
                           
                           // Map click handler for setting checkpoint locations
                           map.on('click', function(e) {{
                               if (selectedCheckpoint) {{
                                   // Send location update
                                   fetch('/checkpoints/' + selectedCheckpoint.id + '/location', {{
                                       method: 'POST',
                                       headers: {{
                                           'Content-Type': 'application/json',
                                           'X-CSRF-TOKEN': document.querySelector('input[name=""__RequestVerificationToken""]').value
                                       }},
                                       body: JSON.stringify({{
                                           latitude: e.latlng.lat,
                                           longitude: e.latlng.lng
                                       }})
                                   }})
                                   .then(response => response.text())
                                   .then(html => {{
                                       // Parse the HTML to extract OOB swaps
                                       var parser = new DOMParser();
                                       var doc = parser.parseFromString(html, 'text/html');
                                       
                                       // Update checkpoints list (main content)
                                       var mainContent = doc.querySelector('div.space-y-2');
                                       if (mainContent) {{
                                           document.getElementById('checkpoints-' + selectedCheckpoint.tourId).innerHTML = mainContent.outerHTML;
                                       }}
                                       
                                       // Execute OOB swap scripts
                                       var oobDiv = doc.querySelector('#map-update');
                                       if (oobDiv) {{
                                           var scripts = oobDiv.getElementsByTagName('script');
                                           for (var i = 0; i < scripts.length; i++) {{
                                               eval(scripts[i].textContent);
                                           }}
                                       }}
                                       
                                       selectedCheckpoint = null;
                                       document.body.style.cursor = 'default';
                                       showNotification('Location set successfully!', 2000);
                                   }})
                                   .catch(error => {{
                                       console.error('Error setting location:', error);
                                       showNotification('Failed to set location', 3000);
                                       selectedCheckpoint = null;
                                       document.body.style.cursor = 'default';
                                   }});
                               }}
                           }});
                       }}
                   }}
                   
                   // Function to show tour on map
                   window.showTourOnMap = function(tourId) {{
                       activeTourId = tourId;
                       
                       // Get tour div and tour name
                       var tourDiv = document.querySelector('[data-tour-id=""' + tourId + '""]');
                       if (!tourDiv) return;
                       
                       var tourName = tourDiv.querySelector('h3').textContent;
                       
                       // Update URL to /tours/{{tourId}}?mode=map-browsing
                       var newUrl = '/tours/' + tourId + '?mode=map-browsing';
                       window.history.pushState({{ tourId: tourId }}, '', newUrl);
                       
                       // Update map panel heading with tour name
                       document.getElementById('map-panel-title').textContent = 'Tour Map: ' + tourName;
                       
                       // Animate the layout
                       var toursPanel = document.getElementById('tours-panel');
                       var mapPanel = document.getElementById('map-panel');
                       
                       // Adjust tours panel to take half width
                       toursPanel.style.maxWidth = '50%';
                       toursPanel.style.margin = '0';
                       
                       // Show and expand map panel
                       mapPanel.style.flex = '1';
                       mapPanel.style.opacity = '1';
                       
                       // Initialize map if needed
                       initializeMap();
                       
                       // Get checkpoints from data attribute
                       var checkpointsJson = tourDiv.getAttribute('data-checkpoints');
                       var checkpoints = JSON.parse(checkpointsJson);
                       
                       // Force map to resize and update markers after animation completes
                       setTimeout(function() {{
                           if (map) {{
                               map.invalidateSize();
                               // Update markers after map is resized
                               updateMapMarkers(checkpoints);
                           }}
                       }}, 600);
                   }};
                   
                   // Function to hide map
                   window.hideMap = function() {{
                       var toursPanel = document.getElementById('tours-panel');
                       var mapPanel = document.getElementById('map-panel');
                       
                       // Hide map panel
                       mapPanel.style.flex = '0 0 0';
                       mapPanel.style.opacity = '0';
                       
                       // Center tours panel
                       toursPanel.style.maxWidth = '100%';
                       toursPanel.style.margin = '0 auto';
                       
                       activeTourId = null;
                       
                       // Update URL back to root
                       window.history.pushState({{}}, '', '/');
                   }};
                   
                   // Function to update markers
                   window.updateMapMarkers = function(checkpoints) {{
                       if (!map) return; // Don't update if map not initialized
                       
                       // Clear existing markers
                       Object.values(markers).forEach(marker => map.removeLayer(marker));
                       markers = {{}};
                       
                       // Add new markers
                       checkpoints.forEach((cp, index) => {{
                           if (cp.latitude && cp.longitude) {{
                               var marker = L.marker([cp.latitude, cp.longitude])
                                   .addTo(map)
                                   .bindPopup('<b>' + (index + 1) + '. ' + cp.name + '</b>');
                               markers[cp.id] = marker;
                           }}
                       }});
                       
                       // Fit bounds if there are markers
                       if (Object.keys(markers).length > 0) {{
                           var group = L.featureGroup(Object.values(markers));
                           map.fitBounds(group.getBounds().pad(0.1));
                       }} else {{
                           // No markers with locations, center on Gdansk
                           map.setView([54.3520, 18.6466], 13);
                       }}
                   }};
                   
                   // Function to select checkpoint for location
                   window.selectCheckpointForLocation = function(checkpointId, tourId) {{
                       selectedCheckpoint = {{ id: checkpointId, tourId: tourId }};
                       document.body.style.cursor = 'crosshair';
                       showNotification('Click on the map to locate this checkpoint', 5000);
                   }};
                   
                   // Function to extract tour ID from URL
                   function getTourIdFromUrl() {{
                       var path = window.location.pathname;
                       var urlParams = new URLSearchParams(window.location.search);
                       var mode = urlParams.get('mode');
                       
                       // Check if URL matches /tours/{{tourId}}?mode=map-browsing
                       if (mode === 'map-browsing' && path.startsWith('/tours/')) {{
                           var tourId = path.replace('/tours/', '').split('?')[0];
                           return tourId ? parseInt(tourId) : null;
                       }}
                       return null;
                   }}
                   
                   // Check URL on page load and restore map view if needed
                   document.body.addEventListener('htmx:afterSettle', function(event) {{
                       // Only run once after tours list is loaded
                       if (event.detail.target.id === 'tours-list') {{
                           var tourId = getTourIdFromUrl();
                           
                           if (tourId) {{
                               setTimeout(function() {{
                                   showTourOnMap(tourId);
                               }}, 100);
                           }}
                       }}
                   }});
                   
                   // Handle browser back/forward buttons
                   window.addEventListener('popstate', function(event) {{
                       var tourId = getTourIdFromUrl();
                       
                       if (tourId) {{
                           // Wait for tours list to exist, then show the map
                           setTimeout(function() {{
                               showTourOnMap(tourId);
                           }}, 100);
                       }} else {{
                           hideMap();
                       }}
                   }});
               </script>
           </body>
           </html>
           ";
}

string GetToursListHtml(ToursRepository repository, string antiforgeryToken)
{
    var html = "";
    foreach (var tour in repository.GetAllTours())
    {
        // Get checkpoints for this tour as JSON
        var tourCheckpoints = repository.GetCheckpointsForTour(tour.Id)
            .Select(cp => new {
                id = cp.Id,
                name = cp.Name,
                latitude = cp.Latitude,
                longitude = cp.Longitude
            })
            .ToList();
        var checkpointsJson = JsonSerializer.Serialize(tourCheckpoints);
        
        html += $@"
            <div class=""bg-white p-4 rounded-lg shadow mb-4"" data-tour-id=""{tour.Id}"" data-checkpoints='{checkpointsJson}'>
                <div class=""flex justify-between items-center mb-2"">
                    <h3 class=""text-lg font-semibold"">{tour.Name}</h3>
                    <div class=""flex gap-2"">
                        <button
                            onclick=""showTourOnMap({tour.Id})""
                            class=""bg-blue-500 text-white px-3 py-1 rounded hover:bg-blue-600 text-sm"">
                            Show on Map
                        </button>
                        <button 
                            hx-delete=""/tours/{tour.Id}"" 
                            hx-target=""#tours-list""
                            hx-swap=""innerHTML""
                            hx-headers='{{""X-CSRF-TOKEN"": ""{antiforgeryToken}""}}'
                            class=""bg-red-500 text-white px-3 py-1 rounded hover:bg-red-600"">
                            Delete Tour
                        </button>
                    </div>
                </div>
                <div id=""checkpoints-{tour.Id}"" hx-get=""/tours/{tour.Id}/checkpoints"" hx-trigger=""load"" hx-swap=""innerHTML"">
                    Loading checkpoints...
                </div>
                <form hx-post=""/tours/{tour.Id}/checkpoints"" hx-target=""#checkpoints-{tour.Id}"" hx-swap=""innerHTML"" class=""mt-3""
                    hx-on::after-request=""this.reset()"">
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
    
    // Build JSON for map markers
    var checkpointsJson = System.Text.Json.JsonSerializer.Serialize(
        tourCheckpoints.Select(cp => new {
            id = cp.Id,
            name = cp.Name,
            latitude = cp.Latitude,
            longitude = cp.Longitude
        })
    );
    
    // Start with the main content div
    var html = $@"<div class=""space-y-2"">";
    
    for (int i = 0; i < tourCheckpoints.Count; i++)
    {
        var cp = tourCheckpoints[i];
        var prevCheckpoint = i > 0 ? tourCheckpoints[i - 1] : null;
        var nextCheckpoint = i < tourCheckpoints.Count - 1 ? tourCheckpoints[i + 1] : null;
        var hasLocation = cp.Latitude.HasValue && cp.Longitude.HasValue;
        
        html += $@"
            <div class=""flex items-center gap-2 bg-gray-50 p-3 rounded {(hasLocation ? "" : "border-2 border-yellow-300")}"">
                <span class=""flex-1 font-medium"">{cp.Order + 1}. {cp.Name}</span>
                <div class=""flex gap-1"">";
        
        // Show locate button if no location
        if (!hasLocation)
        {
            html += $@"
                    <button 
                        onclick=""selectCheckpointForLocation({cp.Id}, {tourId})""
                        class=""bg-yellow-500 text-white px-3 py-1 rounded hover:bg-yellow-600 text-sm font-semibold""
                        title=""Click to locate on map"">
                        Locate
                    </button>";
        }
        
        // Up arrow button (move before previous checkpoint)
        if (prevCheckpoint != null)
        {
            html += $@"
                    <button 
                        hx-post=""/checkpoints/{cp.Id}/move-before/{prevCheckpoint.Id}"" 
                        hx-target=""#checkpoints-{tourId}""
                        hx-swap=""innerHTML""
                        hx-headers='{{""X-CSRF-TOKEN"": ""{antiforgeryToken}""}}'
                        class=""bg-gray-200 text-gray-700 px-2 py-1 rounded hover:bg-gray-300 text-sm""
                        title=""Move up"">
                        ↑
                    </button>";
        }
        else
        {
            html += @"
                    <button class=""bg-gray-100 text-gray-400 px-2 py-1 rounded text-sm cursor-not-allowed"" disabled>
                        ↑
                    </button>";
        }
        
        // Down arrow button (move after next checkpoint)
        if (nextCheckpoint != null)
        {
            html += $@"
                    <button 
                        hx-post=""/checkpoints/{cp.Id}/move-after/{nextCheckpoint.Id}"" 
                        hx-target=""#checkpoints-{tourId}""
                        hx-swap=""innerHTML""
                        hx-headers='{{""X-CSRF-TOKEN"": ""{antiforgeryToken}""}}'
                        class=""bg-gray-200 text-gray-700 px-2 py-1 rounded hover:bg-gray-300 text-sm""
                        title=""Move down"">
                        ↓
                    </button>";
        }
        else
        {
            html += @"
                    <button class=""bg-gray-100 text-gray-400 px-2 py-1 rounded text-sm cursor-not-allowed"" disabled>
                        ↓
                    </button>";
        }
        
        html += $@"
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
    
    // Close the main div
    html += "</div>";
    
    // Add OOB swap to update map markers
    html += $@"
        <div id=""map-update"" hx-swap-oob=""true"">
            <script>
                if (window.updateMapMarkers) {{
                    window.updateMapMarkers({checkpointsJson});
                }}
            </script>
        </div>";
    
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
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

// Repository interface
interface ToursRepository
{
    // Tour operations
    IEnumerable<Tour> GetAllTours();
    Tour? GetTour(int id);
    Tour CreateTour(string name);
    bool DeleteTour(int tourId);
    
    // Checkpoint operations
    IEnumerable<Checkpoint> GetCheckpointsForTour(int tourId);
    Checkpoint? GetCheckpoint(int id);
    Checkpoint CreateCheckpoint(int tourId, string name);
    bool DeleteCheckpoint(int checkpointId);
    bool UpdateCheckpointLocation(int checkpointId, double latitude, double longitude);
    bool MoveCheckpointBefore(int checkpointId, int targetId);
    bool MoveCheckpointAfter(int checkpointId, int targetId);
}

// Repository for persisting tours and checkpoints
class FileToursRepository : ToursRepository
{
    private readonly string _filePath;
    private Dictionary<int, Tour> _tours = new();
    private Dictionary<int, Checkpoint> _checkpoints = new();
    private int _tourIdCounter = 1;
    private int _checkpointIdCounter = 1;
    private readonly object _lock = new();

    public FileToursRepository(IConfiguration configuration)
    {
        _filePath = configuration.GetValue<string>("DataFilePath") ?? "tours-data.json";
        Load();
    }

    // Tour operations
    public IEnumerable<Tour> GetAllTours() => _tours.Values.OrderBy(t => t.Id);

    public Tour? GetTour(int id) => _tours.GetValueOrDefault(id);

    public Tour CreateTour(string name)
    {
        lock (_lock)
        {
            var tour = new Tour { Id = _tourIdCounter++, Name = name };
            _tours[tour.Id] = tour;
            Save();
            return tour;
        }
    }

    public bool DeleteTour(int tourId)
    {
        lock (_lock)
        {
            if (!_tours.Remove(tourId))
                return false;

            // Remove all checkpoints for this tour
            var checkpointsToRemove = _checkpoints.Values
                .Where(c => c.TourId == tourId)
                .Select(c => c.Id)
                .ToList();

            foreach (var cpId in checkpointsToRemove)
            {
                _checkpoints.Remove(cpId);
            }

            Save();
            return true;
        }
    }

    // Checkpoint operations
    public IEnumerable<Checkpoint> GetCheckpointsForTour(int tourId) =>
        _checkpoints.Values
            .Where(c => c.TourId == tourId)
            .OrderBy(c => c.Order);

    public Checkpoint? GetCheckpoint(int id) => _checkpoints.GetValueOrDefault(id);

    public Checkpoint CreateCheckpoint(int tourId, string name)
    {
        lock (_lock)
        {
            var maxOrder = _checkpoints.Values
                .Where(c => c.TourId == tourId)
                .Select(c => (int?)c.Order)
                .Max() ?? -1;

            var checkpoint = new Checkpoint
            {
                Id = _checkpointIdCounter++,
                TourId = tourId,
                Name = name,
                Order = maxOrder + 1
            };
            _checkpoints[checkpoint.Id] = checkpoint;
            Save();
            return checkpoint;
        }
    }

    public bool DeleteCheckpoint(int checkpointId)
    {
        lock (_lock)
        {
            if (!_checkpoints.TryGetValue(checkpointId, out var checkpoint))
                return false;

            var tourId = checkpoint.TourId;
            _checkpoints.Remove(checkpointId);

            // Reorder remaining checkpoints
            var remaining = _checkpoints.Values
                .Where(c => c.TourId == tourId)
                .OrderBy(c => c.Order)
                .ToList();

            for (int i = 0; i < remaining.Count; i++)
            {
                remaining[i].Order = i;
            }

            Save();
            return true;
        }
    }

    public bool UpdateCheckpointLocation(int checkpointId, double latitude, double longitude)
    {
        lock (_lock)
        {
            if (!_checkpoints.TryGetValue(checkpointId, out var checkpoint))
                return false;

            checkpoint.Latitude = latitude;
            checkpoint.Longitude = longitude;
            Save();
            return true;
        }
    }

    public bool MoveCheckpointBefore(int checkpointId, int targetId)
    {
        lock (_lock)
        {
            if (!_checkpoints.TryGetValue(checkpointId, out var checkpoint) ||
                !_checkpoints.TryGetValue(targetId, out var target) ||
                checkpoint.TourId != target.TourId)
                return false;

            var tourId = checkpoint.TourId;
            var tourCheckpoints = _checkpoints.Values
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

            Save();
            return true;
        }
    }

    public bool MoveCheckpointAfter(int checkpointId, int targetId)
    {
        lock (_lock)
        {
            if (!_checkpoints.TryGetValue(checkpointId, out var checkpoint) ||
                !_checkpoints.TryGetValue(targetId, out var target) ||
                checkpoint.TourId != target.TourId)
                return false;

            var tourId = checkpoint.TourId;
            var tourCheckpoints = _checkpoints.Values
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

            Save();
            return true;
        }
    }

    // Persistence
    private void Save()
    {
        var data = new RepositoryData
        {
            Tours = _tours,
            Checkpoints = _checkpoints,
            TourIdCounter = _tourIdCounter,
            CheckpointIdCounter = _checkpointIdCounter
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<RepositoryData>(json);

            if (data != null)
            {
                _tours = data.Tours ?? new();
                _checkpoints = data.Checkpoints ?? new();
                _tourIdCounter = data.TourIdCounter;
                _checkpointIdCounter = data.CheckpointIdCounter;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading data: {ex.Message}");
            // Continue with empty data
        }
    }

    private class RepositoryData
    {
        public Dictionary<int, Tour> Tours { get; set; } = new();
        public Dictionary<int, Checkpoint> Checkpoints { get; set; } = new();
        public int TourIdCounter { get; set; }
        public int CheckpointIdCounter { get; set; }
    }
}
