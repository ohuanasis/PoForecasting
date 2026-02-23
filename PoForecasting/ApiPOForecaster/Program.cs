
namespace ApiPOForecaster
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Ensure deterministic config layering
            builder.Configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);


            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Explicitly tell the app that the HTTPS port is 5017
            builder.Services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = 5017;
            });

            var app = builder.Build();

            // 1. Map OpenAPI for both Dev environments
            if (app.Environment.IsEnvironment("DevelopmentHTTP") || 
                app.Environment.IsEnvironment("DevelopmentHTTPS") || 
                app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // 2. Surgical HTTPS Redirection
            // We ONLY redirect if we are in the HTTPS profile and NOT in Docker
            if (app.Environment.IsEnvironment("DevelopmentHTTPS") && !app.Environment.IsEnvironment("Docker"))
            {
                app.UseHttpsRedirection();
            }

            app.MapGet("/", () => "ApiPOForecaster is running.");

            app.Run();
        }
    }
}
