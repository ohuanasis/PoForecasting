using WebAppPOForecaster.Components;
using WebAppPOForecaster.Services;

namespace WebAppPOForecaster
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddHttpClient<ForecastApiClient>(http =>
            {
                var baseUrl = builder.Configuration["ForecastApi:BaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                    throw new InvalidOperationException("Missing config value: ForecastApi:BaseUrl");

                http.BaseAddress = new Uri(baseUrl);
                http.Timeout = TimeSpan.FromSeconds(30);
                http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
