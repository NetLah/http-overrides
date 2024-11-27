using NetLah.Diagnostics;
using NetLah.Extensions.HttpOverrides;
using NetLah.Extensions.Logging;
using SampleWebApi;

AppLog.InitLogger();
AppLog.Logger.LogInformation("Application configure...");

try
{
    var appInfo = ApplicationInfo.Initialize(null);
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSingleton<IAssemblyInfo>(appInfo);

    builder.UseSerilog(logger => LogAppEvent(logger, "Application initializing...", appInfo));
    var logger = AppLog.Logger;

    void LogLibrary(AssemblyInfo assembly)
    {
        logger.LogInformation("Library:{title}; Version:{version} BuildTime:{buildTime}; Framework:{framework}",
            assembly.Title, assembly.InformationalVersion, assembly.BuildTimestampLocal, assembly.FrameworkName);
    }

    void LogAssembly(AssemblyInfo assembly)
    {
        logger.LogInformation("AssemblyTitle:{title}; Version:{version} Framework:{framework}",
            assembly.Title, assembly.InformationalVersion, assembly.FrameworkName);
    }

    LogLibrary(new AssemblyInfo(typeof(HttpOverridesExtensions).Assembly));
    LogLibrary(new AssemblyInfo(typeof(AppLogReference).Assembly));
    LogAssembly(new AssemblyInfo(typeof(ForwardedHeadersOptions).Assembly));

    // Add services to the container.

    builder.Services.AddControllers();

    // this dependency required by Microsoft.Extensions.ApiDescription.Server.targets
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.AddHttpOverrides();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<HttpContextInfo>();

    var app = builder.Build();

    logger.LogInformation("Environment: {environmentName}; DeveloperMode:{isDevelopment}", app.Environment.EnvironmentName, app.Environment.IsDevelopment());

    app.UseHttpOverrides();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        // todo1
    }

    // app.UseHttpsRedirection()

    app.UseStatusCodePages();

    app.UseStaticFiles();

    app.UseAuthorization();

    app.MapControllers();

    app.Lifetime.ApplicationStarted.Register(() => LogAppEvent(logger, "ApplicationStarted", appInfo));
    app.Lifetime.ApplicationStopping.Register(() => LogAppEvent(logger, "ApplicationStopping", appInfo));
    app.Lifetime.ApplicationStopped.Register(() => LogAppEvent(logger, "ApplicationStopped", appInfo));
    app.Logger.LogInformation("Finished configuring application");
    app.Run();

    static void LogAppEvent(ILogger logger, string appEvent, IAssemblyInfo appInfo)
    {
        logger.LogInformation("{ApplicationEvent} App:{title}; Version:{version} BuildTime:{buildTime}; Framework:{framework}",
            appEvent, appInfo.Title, appInfo.InformationalVersion, appInfo.BuildTimestampLocal, appInfo.FrameworkName);
    }
}
catch (Exception ex)
{
    AppLog.Logger.LogCritical(ex, "Application terminated unexpectedly");
}
finally
{
    await Serilog.Log.CloseAndFlushAsync();
}
