using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Data;

public sealed class EbedrendeloDbContext(DbContextOptions<EbedrendeloDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ExcludedDay> ExcludedDays => Set<ExcludedDay>();
    public DbSet<OrderingPeriod> OrderingPeriods => Set<OrderingPeriod>();
    public DbSet<DailyMenu> DailyMenus => Set<DailyMenu>();
    public DbSet<MenuVariant> MenuVariants => Set<MenuVariant>();
    public DbSet<MenuOrder> MenuOrders => Set<MenuOrder>();
    public DbSet<ALaCarteItem> ALaCarteItems => Set<ALaCarteItem>();
    public DbSet<ALaCarteDailyOffer> ALaCarteDailyOffers => Set<ALaCarteDailyOffer>();
    public DbSet<ALaCarteOrder> ALaCarteOrders => Set<ALaCarteOrder>();
    public DbSet<ALaCarteOrderLine> ALaCarteOrderLines => Set<ALaCarteOrderLine>();
    public DbSet<CreditEntry> CreditEntries => Set<CreditEntry>();
    public DbSet<PeriodInvoice> PeriodInvoices => Set<PeriodInvoice>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<KitchenClosure> KitchenClosures => Set<KitchenClosure>();
    public DbSet<KitchenClosureLine> KitchenClosureLines => Set<KitchenClosureLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EbedrendeloDbContext).Assembly);
    }
}
