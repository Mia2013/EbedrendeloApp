using EbedrendeloApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Extensions;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddEbedrendeloData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EbedrendeloApp")
            ?? throw new InvalidOperationException("Missing connection string 'EbedrendeloApp'.");

        services.AddDbContextFactory<EbedrendeloDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }
}
