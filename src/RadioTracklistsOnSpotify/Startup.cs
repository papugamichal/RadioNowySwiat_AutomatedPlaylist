using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using RadioTracklistsOnSpotify.HostedServices;
using RadioTracklistsOnSpotify.Services;
using RadioTracklistsOnSpotify.Services.DataSourceService.Abstraction;
using RadioTracklistsOnSpotify.Services.SpotifyClientService;
using RadioTracklistsOnSpotify.Services.SpotifyClientService.Abstraction;
using RadioTracklistsOnSpotify.Services.SpotifyClientService.Configuration;
using RadioTracklistsOnSpotify.Services.SpotifyClientService.Security;
using RadioTracklistsOnSpotify.Services.TrackCache;

namespace RadioNowySwiatPlaylistBot
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();

            services
                .AddDataProtection().Services
                .Configure<SpotifyClientOptions>(options =>
                    this.Configuration.GetSection(SpotifyClientOptions.SectionName).Bind(options))
                .Configure<SpotifyAuthorizationServiceOptions>(options =>
                    this.Configuration.GetSection(SpotifyAuthorizationServiceOptions.SectionName).Bind(options))
                .AddSingleton<ISpotifyClientService, SpotifyClientService>()
                .AddSingleton<ISpotifyAuthorizationService, SpotifyAuthorizationService>()
                .AddSingleton<FoundInSpotifyCache>()
                .AddSingleton<NotFoundInSpotifyCache>()
                .AddRadioNowySwiatDataSource()
                .AddRadioNowySwiatPlaylistManager()
                .AddRadio357DataSource()
                .AddRadio357PlaylistManager()
                .AddRadioNowySwiatPlaylistUpdaterHostedService()
                .AddRadio357PlaylistUpdaterHostedService()
                .AddPlaylistVisibilityLimiterHostedService(Configuration)
                .AddKeepAliveHostedService(Configuration)
                ;
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet(ApiPaths.Root, async context =>
                {
                    var spotifyClient = context.RequestServices.GetRequiredService<ISpotifyClientService>();

                    var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
                    var isAuth = spotifyClient.IsAuthenticated();

                    await context.Response.WriteAsync($"Service is working!\n" +
                        $"User is: {(isAuth ? "Authenticated!" : "Not authenticated!")}\n" +
                        $"Uptime: {uptime.Days} days {uptime.Hours} hours {uptime.Minutes} minutes {uptime.Seconds} seconds");
                });

                endpoints.MapGet(ApiPaths.Endpoints, async context =>
                {
                    var endpoints = typeof(ApiPaths).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    var result = JsonConvert.SerializeObject(endpoints.Select(e => (string)e.GetRawConstantValue()).ToList(), new JsonSerializerSettings() { Formatting = Formatting.Indented });
                    await context.Response.WriteAsync(result);
                });

                endpoints.MapGet(ApiPaths.IsAuthenticated, async context =>
                {
                    var client = context.RequestServices.GetRequiredService<ISpotifyClientService>();

                    if (client.IsAuthenticated())
                    {
                        await context.Response.WriteAsync("Authenticated!");
                        return;
                    }

                    await context.Response.WriteAsync("Not authenticated!");
                });

                endpoints.MapGet(ApiPaths.Auth, async context =>
                {
                    var client = context.RequestServices.GetRequiredService<ISpotifyClientService>();

                    if (client.IsAuthenticated())
                    {
                        await context.Response.WriteAsync("Already authenticated!");
                        return;
                    }
                        
                    context.Response.Redirect(client.GetAuthorizationUri().ToString(), permanent: false);
                    
                });

                endpoints.MapGet(ApiPaths.Callback, async context =>
                {
                    var client = context.RequestServices.GetRequiredService<ISpotifyClientService>();

                    if (client.IsAuthenticated())
                    {
                        await context.Response.WriteAsync("Already authenticated!");
                        return;
                    }

                    string code = context.Request.Query["code"];
                    if (string.IsNullOrEmpty(code) && context.Request.Query.ContainsKey("error"))
                    {
                        string error = context.Request.Query["error"];
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Not authenticated! Reason: " + error);
                        return;
                    }

                    else if (string.IsNullOrEmpty(code))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Not authenticated!");
                        return;
                    }

                    await client.SetupAccessToken(code);

                    context.Response.Redirect(ApiPaths.IsAuthenticated, permanent: true);
                });

                endpoints.MapGet(ApiPaths.Playlist, async context =>
                {
                    string dateFromQuery = context.Request.Query["date"];
                    string startDateFromQuery = context.Request.Query["startdate"];
                    string endDateFromQuery = context.Request.Query["enddate"];

                    if (string.IsNullOrEmpty(dateFromQuery) && string.IsNullOrEmpty(startDateFromQuery))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Not date provided");
                        return;
                    }

                    string result = string.Empty;
                    var dataSourceService = context.RequestServices.GetRequiredService<IDataSourceService>();

                    if (!string.IsNullOrEmpty(dateFromQuery))
                    {
                        var playlist = dataSourceService.GetPlaylistFor(DateTime.Parse(dateFromQuery));
                        context.Response.ContentType = "application/json";
                        result = JsonConvert.SerializeObject(playlist, new JsonSerializerSettings() { Formatting = Formatting.Indented });
                    }
                    else if (!string.IsNullOrEmpty(startDateFromQuery))
                    {
                        DateTime endDate;
                        if (!string.IsNullOrEmpty(endDateFromQuery))
                        {
                            endDate = DateTime.Parse(endDateFromQuery);
                        }
                        else
                        {
                            endDate = DateTime.Today;
                        }

                        var playlist = dataSourceService.GetPlaylistForRange(DateTime.Parse(startDateFromQuery), endDate);
                        context.Response.ContentType = "application/json";
                        result = JsonConvert.SerializeObject(playlist, new JsonSerializerSettings() { Formatting = Formatting.Indented });
                    }

                    await context.Response.WriteAsync(result);
                });

                endpoints.MapGet(ApiPaths.UserId, async context =>
                {
                    var spotifyClient = context.RequestServices.GetRequiredService<ISpotifyClientService>();
                    if (!spotifyClient.IsAuthenticated())
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Not authenticated!");
                        return;
                    }
                    var result = await spotifyClient.RequestForUserId();
                    await context.Response.WriteAsync(result);
                });

                endpoints.MapGet(ApiPaths.UserPlaylists, async context =>
                {
                    var spotifyClient = context.RequestServices.GetRequiredService<ISpotifyClientService>();
                    if (!spotifyClient.IsAuthenticated())
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Not authenticated!");
                        return;
                    }

                    var playlists = await spotifyClient.RequestForUserPlaylists();
                    context.Response.ContentType = "application/json";
                    var result = JsonConvert.SerializeObject(playlists, new JsonSerializerSettings() { Formatting = Formatting.Indented });
                    await context.Response.WriteAsync(result);
                });

                endpoints.MapGet(ApiPaths.CreatePlaylist, async context =>
                {
                    string name = context.Request.Query["name"];
                    if (string.IsNullOrEmpty(name))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Not name provided");
                        return;
                    }

                    var spotifyClient = context.RequestServices.GetRequiredService<ISpotifyClientService>();
                    if (!spotifyClient.IsAuthenticated())
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Not authenticated!");
                        return;
                    }

                    //await spotifyClient.CreatePlaylist(name);
                    await context.Response.WriteAsync("Done");
                });
            });
        }
    }

    public static class ApiPaths
    {
        public const string Root = "/";
        public const string Endpoints = "/api";
        public const string Auth = "/auth";
        public const string IsAuthenticated = "/isAuthenticated";
        public const string Callback = "/callback";
        public const string UserPlaylists = "/userplaylists";
        public const string UserId = "/userid";
        public const string Playlist = "/playlist/";
        public const string CreatePlaylist = "/createplaylist";
    }
}

