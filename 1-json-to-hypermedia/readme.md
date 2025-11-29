## Exercise - PART I
### Backend and Frontend Engineer Collaboration: introduction
Go to `./exercise` and find "backend-engineer.cs" and "frontend-engineer.html" files. Naming is not accidental here - we try to "simulate" two different roles in a web application development team: backend engineer and frontend engineer.

This application is meant to:
* show current server time 
* allow user starting and stopping time refreshing session (whenever a session is started, the server time is refreshed every second and presented to the user, until user stops the session)

Run the application and explore its current behavior.

### Backend and Frontend Engineer Collaboration: new requirement
We want to limit the usage of refreshing sessions to 5. Once user has used 5 sessions, the "start refreshing" button should be disabled.

**Hint**: you will need to modify both backend and frontend code to implement this requirement, one of the things to cover in satisfying this requirement is to update the state of the system whenever a user starts a refreshing session.

#### Sequence diagram
```mermaid
sequenceDiagram
    participant Browser
    participant Server
    participant RefreshingSession

    Browser->>Server: GET /
    Server-->>Browser: frontend-engineer.html

    Note over Browser: Page loads with JavaScript

    Browser->>Server: GET /current-time
    Server->>RefreshingSession: HasReachedLimit()
    RefreshingSession-->>Server: false/true
    Server-->>Browser: JSON { time, refreshingSessionsLimitReached, _links }
    
    Note over Browser: JavaScript renders time<br/>Sets up auto-refresh timer

    loop Every interval (while enabled)
        Browser->>Server: PUT /time/refreshing
        Server->>RefreshingSession: Use()
        alt Limit not reached
            RefreshingSession-->>Server: success
        else Limit reached
            RefreshingSession-->>Server: throws Exception
        end
        Server-->>Browser: JSON { refreshingSessionsLimitReached }
        
        alt refreshingSessionsLimitReached = false
            Browser->>Server: GET /current-time
            Server->>RefreshingSession: HasReachedLimit()
            RefreshingSession-->>Server: false
            Server-->>Browser: JSON { time, refreshingSessionsLimitReached, _links }
            Note over Browser: Updates UI with new time
        else refreshingSessionsLimitReached = true
            Note over Browser: Disables refresh button<br/>Stops auto-refresh
        end
    end
```

## Exercise - PART II
### HTMX and Hypermedia: introduction
Go to `./exercise` and implement the same set of initial requirements using HTMX and hypermedia principles:
* show current server time 
* allow user starting and stopping time refreshing session (whenever a session is started, the server time is refreshed every second and presented to the user, until user stops the session)

#### Sequence diagram
```mermaid
sequenceDiagram
    participant Browser
    participant Server

    Browser->>Server: GET /hypermedia
    Server-->>Browser: HTML with htmx container<br/>(hx-get="/current-time/load" hx-trigger="load")

    Note over Browser: htmx library loads and<br/>processes hx-trigger="load"

    Browser->>Server: GET /current-time/load
    Server-->>Browser: HTML fragment with:<br/>- Current time div<br/>- Reload link<br/>- Start refreshing button

    Note over Browser: htmx swaps innerHTML<br/>of time-container

    alt User clicks "start refreshing" button
        Browser->>Server: GET /current-time/refreshing/start
        Note over Server: Creates polling response
        Server-->>Browser: HTML fragment with:<br/>- Time div with hx-trigger="every 1s"<br/>- Stop button (via hx-swap-oob)
        
        Note over Browser: htmx swaps time div<br/>and button simultaneously
        
        loop Every 1 second
            Note over Browser: hx-trigger fires automatically
            Browser->>Server: GET /current-time/refreshing
            Server-->>Browser: HTML fragment with:<br/>- Updated time<br/>- Same hx-trigger="every 1s"
            Note over Browser: htmx swaps innerHTML<br/>Polling continues
        end

        Note over Browser: User clicks "stop refreshing"
        Browser->>Server: GET /current-time/refreshing/stop
        Server-->>Browser: HTML fragment with:<br/>- Static time div (no hx-trigger)<br/>- Start button (via hx-swap-oob)
        
        Note over Browser: htmx swaps elements<br/>Polling stops (no trigger)
    end
```

### HTMX and Hypermedia: new requirement
Now we want to satisfy the same requirement as before, but now using HTMX and hypermedia principles.

Use **no** javascript code, focus on server-side and hypermedia HTTP endpoints.

#### Sequence diagram
### HTMX and Hypermedia: sequence diagram
```mermaid
sequenceDiagram
    participant Browser
    participant Server
    participant RefreshingSession

    Browser->>Server: GET /hypermedia
    Server-->>Browser: HTML with htmx container

    Note over Browser: htmx loads

    Browser->>Server: GET /current-time/load (hx-trigger="load")
    Server-->>Browser: HTML fragment with time,<br/>reload link, and "start refreshing" button

    alt User clicks "start refreshing"
        Browser->>Server: GET /current-time/refreshing/start
        Server->>RefreshingSession: Use()
        Server->>RefreshingSession: HasReachedLimit()
        
        alt Limit NOT reached
            RefreshingSession-->>Server: false
            Server-->>Browser: HTML with hx-trigger="every 1s"<br/>+ "stop refreshing" button (OOB swap)
            
            loop Every 1 second (while active)
                Browser->>Server: GET /current-time/refreshing
                Server-->>Browser: HTML fragment with updated time<br/>+ hx-trigger="every 1s"
                Note over Browser: htmx swaps innerHTML<br/>Re-establishes polling
            end
            
            Note over Browser: User clicks "stop refreshing"
            Browser->>Server: GET /current-time/refreshing/stop
            Server-->>Browser: HTML with static time<br/>+ "start refreshing" button (OOB swap)
            Note over Browser: Polling stops (no hx-trigger)
        else Limit reached
            RefreshingSession-->>Server: true
            Server-->>Browser: HTML with static time<br/>+ disabled "start refreshing" button (OOB swap)
            Note over Browser: Button disabled,<br/>no further interaction possible
        end
    end
```

### Useful hints
You can peek solution by using `./check-solution.sh 1-json-to-hypermedia` script from the root of the repo.