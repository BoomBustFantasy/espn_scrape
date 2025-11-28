using BoomBust.HealthChecks;
using BoomBust.Logging;
using ESPNScrape.Jobs;
using ESPNScrape.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Quartz;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure logging with BoomBust.Logging
    builder.UseBoomBustLogging(options =>
    {
        options.ApplicationName = "ESPNScrape";
        options.LogFilePath = "logs/espn-scrape-.txt";
        options.OverrideToWarning = ["Microsoft", "System", "Quartz"];
    });

    Log.Information("Starting ESPN Scrape Service");

    // Register HttpClient for ESPN API with resilience policies
    builder.Services.AddHttpClient<ESPNDataService>(client =>
    {
        client.BaseAddress = new Uri("https://sports.core.api.espn.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "ESPNScraper/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    })
    .AddStandardResilienceHandler(options =>
    {
        // Configure retry with exponential backoff
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.OnRetry = args =>
        {
            Log.Warning("Retry attempt {AttemptNumber} for ESPN API after {Delay}ms delay. Exception: {Exception}",
                args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message);
            return default;
        };

        // Configure timeout
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.OnTimeout = args =>
        {
            Log.Error("Request to ESPN API timed out after 30s");
            return default;
        };

        // Configure circuit breaker
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 3;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.OnOpened = args =>
        {
            Log.Error("Circuit breaker opened for ESPN API. Will retry after 30s");
            return default;
        };
        options.CircuitBreaker.OnClosed = args =>
        {
            Log.Information("Circuit breaker closed for ESPN API. Service is healthy again");
            return default;
        };
    });

    Log.Information("Resilience policies configured: MaxRetries=3, Timeout=30s, CircuitBreaker enabled");

    // Register services
    builder.Services.AddScoped<ESPNDataService>();
    builder.Services.AddScoped<SupabaseService>();
    builder.Services.AddScoped<ESPNPlayerMappingService>();
    builder.Services.AddScoped<ImageProcessingService>();

    // Add ASP.NET Core services
    builder.Services.AddControllers();

    // Required for BoomBust.HealthChecks
    builder.Services.AddHttpClient();

    // Get Supabase connection string for health checks
    var supabaseUrl = builder.Configuration["Supabase:Url"];
    var supabaseServiceKey = builder.Configuration["Supabase:ServiceRoleKey"];
    var supabaseConnectionString = !string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseServiceKey)
        ? $"Host={new Uri(supabaseUrl).Host};Database=postgres;Username=postgres;Password={supabaseServiceKey}"
        : null;

    // Add comprehensive health checks with BoomBust.HealthChecks
    builder.Services.AddHealthChecks()
        // Liveness - is the app alive?
        .AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: ["live"])
        
        // Readiness - Database check
        .AddSupabaseHealthCheck(
            connectionString: supabaseConnectionString ?? "Host=localhost;Database=postgres;Username=postgres;Password=postgres",
            name: "supabase",
            healthCheckName: "Supabase Database",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["db", "supabase", "ready"],
            timeout: TimeSpan.FromSeconds(10)
        )
        
        // Readiness - External API checks
        .AddApiHealthCheck(
            apiUrl: "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl",
            name: "espn-api",
            healthCheckName: "ESPN API",
            failureStatus: HealthStatus.Degraded,
            tags: ["external", "api", "ready"],
            timeout: TimeSpan.FromSeconds(10)
        );

    // Configure Quartz with all jobs
    builder.Services.AddQuartz(q =>
    {
        // ============================================
        // NFL Weekly Stats Job
        // Runs every Tuesday at 6 AM (after Monday Night Football)
        // ============================================
        var weeklyStatsJobKey = new JobKey("NFLWeeklyJob");
        q.AddJob<NFLWeeklyJob>(opts => opts
            .WithIdentity(weeklyStatsJobKey)
            .DisallowConcurrentExecution()
            .StoreDurably());

        q.AddTrigger(opts => opts
            .ForJob(weeklyStatsJobKey)
            .WithIdentity("NFLWeeklyJob-cron-trigger")
            .WithCronSchedule("0 0 6 ? * TUE") // Every Tuesday at 6:00 AM
            .UsingJobData("season", 2025)
            .UsingJobData("startWeek", 1)
            .UsingJobData("endWeek", 18)
            .WithDescription("NFL Weekly Stats - Every Tuesday at 6 AM"));

        // ============================================
        // NFL Schedule Sync Job
        // Runs every day at 5 AM to catch schedule updates
        // ============================================
        var scheduleSyncJobKey = new JobKey("NFLScheduleSyncJob");
        q.AddJob<NFLScheduleSyncJob>(opts => opts
            .WithIdentity(scheduleSyncJobKey)
            .DisallowConcurrentExecution()
            .StoreDurably());

        q.AddTrigger(opts => opts
            .ForJob(scheduleSyncJobKey)
            .WithIdentity("NFLScheduleSyncJob-cron-trigger")
            .WithCronSchedule("0 0 5 * * ?") // Every day at 5:00 AM
            .UsingJobData("season", 2025)
            .UsingJobData("startWeek", 1)
            .UsingJobData("endWeek", 18)
            .UsingJobData("seasonType", 2) // Regular season
            .WithDescription("NFL Schedule Sync - Daily at 5 AM"));

        // ============================================
        // NFL Player Sync Job
        // Runs every day at 4 AM to sync ESPN player IDs
        // ============================================
        var playerSyncJobKey = new JobKey("NFLPlayerSyncJob");
        q.AddJob<NFLPlayerSyncJob>(opts => opts
            .WithIdentity(playerSyncJobKey)
            .DisallowConcurrentExecution()
            .StoreDurably());

        q.AddTrigger(opts => opts
            .ForJob(playerSyncJobKey)
            .WithIdentity("NFLPlayerSyncJob-cron-trigger")
            .WithCronSchedule("0 0 4 * * ?") // Every day at 4:00 AM
            .WithDescription("NFL Player Sync - Daily at 4 AM"));

        // ============================================
        // NFL Player Headshot Job
        // Runs every Sunday at 3 AM (weekly before games)
        // ============================================
        var headshotJobKey = new JobKey("NFLPlayerHeadshotJob");
        q.AddJob<NFLPlayerHeadshotJob>(opts => opts
            .WithIdentity(headshotJobKey)
            .DisallowConcurrentExecution()
            .StoreDurably());

        q.AddTrigger(opts => opts
            .ForJob(headshotJobKey)
            .WithIdentity("NFLPlayerHeadshotJob-cron-trigger")
            .WithCronSchedule("0 0 3 ? * SUN") // Every Sunday at 3:00 AM
            .WithDescription("NFL Player Headshots - Every Sunday at 3 AM"));
    });

    // Add Quartz hosted service
    builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseRouting();
    app.MapControllers();

    // Health check endpoints - Kubernetes style
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        AllowCachingResponses = false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        AllowCachingResponses = false
    });

    // Detailed health check with JSON response
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            
            var response = new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = $"{e.Value.Duration.TotalMilliseconds:F2}ms",
                    exception = e.Value.Exception?.Message,
                    data = e.Value.Data
                }),
                totalDuration = $"{report.TotalDuration.TotalMilliseconds:F2}ms"
            };
            
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    Log.Information("ESPN Scrape Service started with scheduled jobs:");
    Log.Information("  📊 NFLWeeklyJob: Every Tuesday at 6:00 AM");
    Log.Information("  🗓️ NFLScheduleSyncJob: Daily at 5:00 AM");
    Log.Information("  👤 NFLPlayerSyncJob: Daily at 4:00 AM");
    Log.Information("  📸 NFLPlayerHeadshotJob: Every Sunday at 3:00 AM");
    Log.Information("Health check endpoints: /health, /health/live, /health/ready");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
