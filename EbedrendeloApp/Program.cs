using EbedrendeloApp.Components;
using EbedrendeloApp.Data;
using EbedrendeloApp.Data.Seed;
using EbedrendeloApp.Extensions;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

namespace EbedrendeloApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddMudServices();

            builder.Services.AddEbedrendeloData(builder.Configuration);

            var app = builder.Build();

            await using (var db = await app.Services.GetRequiredService<IDbContextFactory<EbedrendeloDbContext>>().CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
                await DatabaseSeeder.SeedAsync(db);
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
