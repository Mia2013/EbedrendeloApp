using EbedrendeloApp.Common.Behaviors;
using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using FluentValidation;

namespace EbedrendeloApp.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddEbedrendeloApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppClock, AppClock>();
        services.AddSingleton<IWorkingDayCalculator, WorkingDayCalculator>();
        services.AddScoped<ICreditService, CreditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMenuReassignmentService, MenuReassignmentService>();

        // Stub current-user (see StubCurrentUser) — replaced wholesale by Epic 9's cookie-based
        // ICurrentUser; both interfaces are implemented by the same instance for now.
        services.AddScoped<StubCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<StubCurrentUser>());
        services.AddScoped<IDevUserSwitcher>(sp => sp.GetRequiredService<StubCurrentUser>());

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(Program));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        AddValidatorsFromCurrentAssembly(services);

        return services;
    }

    private static void AddValidatorsFromCurrentAssembly(IServiceCollection services)
    {
        var validatorInterface = typeof(IValidator<>);

        foreach (var type in typeof(Program).Assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            foreach (var implementedInterface in type.GetInterfaces())
            {
                if (implementedInterface.IsGenericType && implementedInterface.GetGenericTypeDefinition() == validatorInterface)
                {
                    services.AddScoped(implementedInterface, type);
                }
            }
        }
    }
}
