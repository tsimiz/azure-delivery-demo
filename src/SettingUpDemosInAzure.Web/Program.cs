using SettingUpDemosInAzure.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .AddApplicationInsightsTelemetry()
            .AddHostedService<SqlDbInitializer>()
            .AddConnectionFactories();
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.Configure((webHostBuilderContext, app) =>
        {
            if (webHostBuilderContext.HostingEnvironment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapApplicationRoutes();
            });
        });
    })
    .Build()
    .Run();