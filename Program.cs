using ESPNScrape.Jobs;
using ESPNScrape.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/espn-scrape-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    if (args.Length > 0 && args[0] == "--stats-2025")
    {
        Log.Information("Running NFL Weekly Stats job for 2025 season");

        var statsHost = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Register HttpClient
                services.AddHttpClient<ESPNDataService>();

                // Register services
                services.AddSingleton<ESPNDataService>();
                services.AddSingleton<SupabaseService>();
                services.AddSingleton<ESPNPlayerMappingService>();

                // Configure Quartz for stats job
                services.AddQuartz(q =>
                {
                    var statsJobKey = new JobKey("NFLWeeklyJob2025");
                    q.AddJob<NFLWeeklyJob>(opts => opts.WithIdentity(statsJobKey));

                    q.AddTrigger(opts => opts
                        .ForJob(statsJobKey)
                        .WithIdentity("NFLWeeklyJob2025-trigger")
                        .StartNow()
                        .UsingJobData("season", 2025)
                        .UsingJobData("startWeek", 1)
                        .UsingJobData("endWeek", 18));
                });

                services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
            })
            .Build();

        await statsHost.RunAsync();
        return;
    }

    if (args.Length > 0 && args[0] == "--headshots-only")
    {
        Log.Information("Running NFL Player Headshot job only");

        var headshotHost = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Register HttpClient
                services.AddHttpClient<ESPNDataService>();

                // Register services
                services.AddSingleton<ESPNDataService>();
                services.AddSingleton<SupabaseService>();
                services.AddSingleton<ImageProcessingService>();

                // Configure Quartz for headshot job only
                services.AddQuartz(q =>
                {
                    var headshotJobKey = new JobKey("NFLPlayerHeadshotJob");
                    q.AddJob<NFLPlayerHeadshotJob>(opts => opts.WithIdentity(headshotJobKey));

                    q.AddTrigger(opts => opts
                        .ForJob(headshotJobKey)
                        .WithIdentity("NFLPlayerHeadshotJob-trigger")
                        .StartNow());
                });

                services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
            })
            .Build();

        await headshotHost.RunAsync();
        return;
    }

    if (args.Length > 0 && args[0] == "--sync-players")
    {
        Log.Information("Running NFL Player Sync job - syncing ESPN player IDs");

        var playerSyncHost = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Register HttpClient
                services.AddHttpClient<ESPNDataService>();

                // Register services
                services.AddSingleton<ESPNDataService>();
                services.AddSingleton<SupabaseService>();
                services.AddSingleton<ESPNPlayerMappingService>();

                // Configure Quartz for player sync job
                services.AddQuartz(q =>
                {
                    var playerSyncJobKey = new JobKey("NFLPlayerSyncJob");
                    q.AddJob<NFLPlayerSyncJob>(opts => opts.WithIdentity(playerSyncJobKey));

                    q.AddTrigger(opts => opts
                        .ForJob(playerSyncJobKey)
                        .WithIdentity("NFLPlayerSyncJob-trigger")
                        .StartNow());
                });

                services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
            })
            .Build();

        await playerSyncHost.RunAsync();
        return;
    }

    if (args.Length > 0 && args[0] == "--sync-schedule")
    {
        Log.Information("Running NFL Schedule Sync job - syncing ESPN schedule data");

        var scheduleSyncHost = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Register HttpClient
                services.AddHttpClient<ESPNDataService>();

                // Register services
                services.AddSingleton<ESPNDataService>();
                services.AddSingleton<SupabaseService>();

                // Configure Quartz for schedule sync job
                services.AddQuartz(q =>
                {
                    var scheduleSyncJobKey = new JobKey("NFLScheduleSyncJob");
                    q.AddJob<NFLScheduleSyncJob>(opts => opts.WithIdentity(scheduleSyncJobKey));

                    q.AddTrigger(opts => opts
                        .ForJob(scheduleSyncJobKey)
                        .WithIdentity("NFLScheduleSyncJob-trigger")
                        .StartNow()
                        .UsingJobData("season", 2025)
                        .UsingJobData("startWeek", 13)
                        .UsingJobData("endWeek", 18)
                        .UsingJobData("seasonType", 2)); // Regular season
                });

                services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
            })
            .Build();

        await scheduleSyncHost.RunAsync();
        return;
    }

    // Debug mode removed - always process all teams and all players

    Log.Information("Starting NFL Player Headshot job");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            // Register HttpClient
            services.AddHttpClient<ESPNDataService>();

            // Register services
            services.AddSingleton<ESPNDataService>();
            services.AddSingleton<SupabaseService>();
            services.AddSingleton<ImageProcessingService>();

            // Configure Quartz - ONLY the headshot job
            services.AddQuartz(q =>
            {
                var headshotJobKey = new JobKey("NFLPlayerHeadshotJob");
                q.AddJob<NFLPlayerHeadshotJob>(opts => opts.WithIdentity(headshotJobKey));

                q.AddTrigger(opts => opts
                    .ForJob(headshotJobKey)
                    .WithIdentity("NFLPlayerHeadshotJob-trigger")
                    .StartNow());
            });

            // Add Quartz hosted service
            services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
