using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Data.Seed;

public static class DatabaseSeeder
{
    public const string AdminRoleName = "Admin";
    public const string UserRoleName = "User";

    private static readonly string[] VariantCodes = ["A", "B", "C"];

    public static async Task SeedAsync(EbedrendeloDbContext db, CancellationToken ct = default)
    {
        var roles = await SeedRolesAsync(db, ct);
        var settings = await SeedAppSettingAsync(db, ct);
        var users = await SeedUsersAsync(db, roles, ct);
        var periods = await SeedOrderingPeriodsAsync(db, ct);
        var excludedDays = await SeedExcludedDaysAsync(db, users, roles, periods, ct);
        var variantsByDate = await SeedDailyMenusAsync(db, periods, ct);
        var items = await SeedALaCarteItemsAsync(db, ct);
        await SeedALaCarteDailyOffersAsync(db, items, excludedDays, ct);
        await SeedSampleTrafficAsync(db, users, roles, periods, excludedDays, variantsByDate, settings, ct);
    }

    private static async Task<Dictionary<string, Role>> SeedRolesAsync(EbedrendeloDbContext db, CancellationToken ct)
    {
        if (await db.Roles.AnyAsync(ct))
        {
            var existing = await db.Roles.ToListAsync(ct);
            return existing.ToDictionary(r => r.Name);
        }

        var roles = new List<Role>
        {
            new() { Name = AdminRoleName },
            new() { Name = UserRoleName },
        };

        db.Roles.AddRange(roles);
        await db.SaveChangesAsync(ct);
        return roles.ToDictionary(r => r.Name);
    }

    private static async Task<AppSetting> SeedAppSettingAsync(EbedrendeloDbContext db, CancellationToken ct)
    {
        var existing = await db.AppSettings.FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return existing;
        }

        var setting = new AppSetting
        {
            MenuPortionHuf = 1400,
            ChangeDeadlineWorkingDays = 3,
            ChangeDeadlineLocalTime = new TimeOnly(11, 0),
            ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
            UpdatedAtUtc = DateTime.UtcNow,
        };

        db.AppSettings.Add(setting);
        await db.SaveChangesAsync(ct);
        return setting;
    }

    private static async Task<List<User>> SeedUsersAsync(EbedrendeloDbContext db, Dictionary<string, Role> roles, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return await db.Users.ToListAsync(ct);
        }

        var adminRoleId = roles[AdminRoleName].Id;
        var userRoleId = roles[UserRoleName].Id;

        var users = new List<User>
        {
            new() { UserId = 1001, UserName = "admin", KeresztNev = "Rendszer", VezetekNev = "Adminisztrátor", Rf = "RF-000", SzervKod = "KOZP", RoleId = adminRoleId },
            new() { UserId = 1002, UserName = "kovacs.j", KeresztNev = "János", VezetekNev = "Kovács", Rf = "RF-101", SzervKod = "GY01", RoleId = userRoleId },
            new() { UserId = 1003, UserName = "nagy.a", KeresztNev = "Anna", VezetekNev = "Nagy", Rf = "RF-102", SzervKod = "GY01", RoleId = userRoleId },
            new() { UserId = 1004, UserName = "szabo.p", KeresztNev = "Péter", VezetekNev = "Szabó", Rf = "RF-103", SzervKod = "GY02", RoleId = userRoleId },
            new() { UserId = 1005, UserName = "toth.e", KeresztNev = "Eszter", VezetekNev = "Tóth", Rf = "RF-104", SzervKod = "GY02", RoleId = userRoleId },
            new() { UserId = 1006, UserName = "varga.b", KeresztNev = "Balázs", VezetekNev = "Varga", Rf = "RF-105", SzervKod = "GY03", RoleId = userRoleId },
        };

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);
        return users;
    }

    private static async Task<List<OrderingPeriod>> SeedOrderingPeriodsAsync(EbedrendeloDbContext db, CancellationToken ct)
    {
        if (await db.OrderingPeriods.AnyAsync(ct))
        {
            return await db.OrderingPeriods.OrderBy(p => p.StartDate).ToListAsync(ct);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var period1Start = new DateOnly(today.Year, today.Month, 5);
        var period1End = period1Start.AddMonths(1);
        var period2Start = period1End;
        var period2End = period2Start.AddMonths(1);

        var periods = new List<OrderingPeriod>
        {
            new()
            {
                Name = $"{period1Start:yyyy. MMMM}",
                StartDate = period1Start,
                EndDate = period1End,
                OrderDeadline = period1Start.AddDays(-10).ToDateTime(new TimeOnly(10, 0)),
                IsOpen = true,
            },
            new()
            {
                Name = $"{period2Start:yyyy. MMMM}",
                StartDate = period2Start,
                EndDate = period2End,
                OrderDeadline = period2Start.AddDays(-10).ToDateTime(new TimeOnly(10, 0)),
                IsOpen = true,
            },
        };

        db.OrderingPeriods.AddRange(periods);
        await db.SaveChangesAsync(ct);
        return periods;
    }

    private static async Task<List<ExcludedDay>> SeedExcludedDaysAsync(
        EbedrendeloDbContext db, List<User> users, Dictionary<string, Role> roles, List<OrderingPeriod> periods, CancellationToken ct)
    {
        if (await db.ExcludedDays.AnyAsync(ct))
        {
            return await db.ExcludedDays.ToListAsync(ct);
        }

        var adminRoleId = roles[AdminRoleName].Id;
        var admin = users.Single(u => u.RoleId == adminRoleId);
        var period1 = periods[0];
        var candidates = GetWeekdays(period1.StartDate, period1.EndDate).Skip(2).Take(1);

        var excludedDays = candidates
            .Select(date => new ExcludedDay
            {
                Date = date,
                Reason = "Karbantartás",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = admin.Id,
            })
            .ToList();

        db.ExcludedDays.AddRange(excludedDays);
        await db.SaveChangesAsync(ct);
        return excludedDays;
    }

    private static async Task<Dictionary<DateOnly, List<MenuVariant>>> SeedDailyMenusAsync(
        EbedrendeloDbContext db, List<OrderingPeriod> periods, CancellationToken ct)
    {
        if (await db.DailyMenus.AnyAsync(ct))
        {
            var existing = await db.DailyMenus.Include(m => m.Variants).ToListAsync(ct);
            return existing.ToDictionary(m => m.Date, m => m.Variants.OrderBy(v => v.SortOrder).ToList());
        }

        var weekdays = periods.SelectMany(p => GetWeekdays(p.StartDate, p.EndDate)).Distinct().OrderBy(d => d).ToList();
        var recipeCount = SeedCatalog.Recipes.Count;

        var dailyMenus = new List<DailyMenu>();
        for (var i = 0; i < weekdays.Count; i++)
        {
            var variants = new List<MenuVariant>();
            for (var v = 0; v < VariantCodes.Length; v++)
            {
                var recipe = SeedCatalog.Recipes[(i * VariantCodes.Length + v) % recipeCount];
                variants.Add(new MenuVariant
                {
                    DailyMenuId = 0,
                    Code = VariantCodes[v],
                    Name = recipe.Name,
                    Description = recipe.Description,
                    SortOrder = v,
                });
            }

            dailyMenus.Add(new DailyMenu
            {
                Date = weekdays[i],
                IsPublished = true,
                Variants = variants,
            });
        }

        db.DailyMenus.AddRange(dailyMenus);
        await db.SaveChangesAsync(ct);
        return dailyMenus.ToDictionary(m => m.Date, m => m.Variants.OrderBy(v => v.SortOrder).ToList());
    }

    private static async Task<List<ALaCarteItem>> SeedALaCarteItemsAsync(EbedrendeloDbContext db, CancellationToken ct)
    {
        if (await db.ALaCarteItems.AnyAsync(ct))
        {
            return await db.ALaCarteItems.ToListAsync(ct);
        }

        var items = new List<ALaCarteItem>
        {
            new() { Name = "Húsleves", Category = ALaCarteCategory.Leves, PriceHuf = 650, IsActive = true, Allergens = "1,9" },
            new() { Name = "Zöldségkrémleves", Category = ALaCarteCategory.Leves, PriceHuf = 600, IsActive = true, Allergens = "7,9" },
            new() { Name = "Cordon bleu", Category = ALaCarteCategory.Foetel, PriceHuf = 1900, IsActive = true, Allergens = "1,3,7" },
            new() { Name = "Rántott hal", Category = ALaCarteCategory.Foetel, PriceHuf = 1750, IsActive = true, Allergens = "1,3,4" },
            new() { Name = "Bakonyi szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1850, IsActive = true, Allergens = "1,3,7" },
            new() { Name = "Zöldségfasírt", Category = ALaCarteCategory.Foetel, PriceHuf = 1500, IsActive = true, Allergens = "1,3" },
            new() { Name = "Hasábburgonya", Category = ALaCarteCategory.Koret, PriceHuf = 550, IsActive = true },
            new() { Name = "Jázmin rizs", Category = ALaCarteCategory.Koret, PriceHuf = 500, IsActive = true },
            new() { Name = "Vegyes saláta", Category = ALaCarteCategory.Koret, PriceHuf = 500, IsActive = true },
            new() { Name = "Somlói galuska", Category = ALaCarteCategory.Desszert, PriceHuf = 750, IsActive = true, Allergens = "1,3,7,8" },
            new() { Name = "Gesztenyepüré", Category = ALaCarteCategory.Desszert, PriceHuf = 700, IsActive = true, Allergens = "7" },
        };

        db.ALaCarteItems.AddRange(items);
        await db.SaveChangesAsync(ct);
        return items;
    }

    private static async Task SeedALaCarteDailyOffersAsync(
        EbedrendeloDbContext db, List<ALaCarteItem> items, List<ExcludedDay> excludedDays, CancellationToken ct)
    {
        if (await db.ALaCarteDailyOffers.AnyAsync(ct))
        {
            return;
        }

        var excludedDates = excludedDays.Select(e => e.Date).ToHashSet();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var offerDates = GetWeekdays(today, today.AddDays(14))
            .Where(d => !excludedDates.Contains(d))
            .Take(5)
            .ToList();

        var capacities = new[] { 8, 10, 12, 15 };
        var offers = new List<ALaCarteDailyOffer>();
        for (var d = 0; d < offerDates.Count; d++)
        {
            foreach (var item in items)
            {
                offers.Add(new ALaCarteDailyOffer
                {
                    Date = offerDates[d],
                    ALaCarteItemId = item.Id,
                    Capacity = capacities[(d + item.Id) % capacities.Length],
                    OrderedCount = 0,
                });
            }
        }

        db.ALaCarteDailyOffers.AddRange(offers);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSampleTrafficAsync(
        EbedrendeloDbContext db,
        List<User> users,
        Dictionary<string, Role> roles,
        List<OrderingPeriod> periods,
        List<ExcludedDay> excludedDays,
        Dictionary<DateOnly, List<MenuVariant>> variantsByDate,
        AppSetting settings,
        CancellationToken ct)
    {
        if (await db.MenuOrders.AnyAsync(ct))
        {
            return;
        }

        var userRoleId = roles[UserRoleName].Id;
        var workers = users.Where(u => u.RoleId == userRoleId).ToList();
        var period1 = periods[0];
        var orderableDates = GetWeekdays(period1.StartDate, period1.EndDate)
            .Where(variantsByDate.ContainsKey)
            .ToList();
        var menuPortionHuf = settings.MenuPortionHuf;

        var activeOrders = new List<MenuOrder>();
        for (var i = 0; i < Math.Min(3, Math.Min(workers.Count, orderableDates.Count)); i++)
        {
            var variant = variantsByDate[orderableDates[i]][0];
            activeOrders.Add(new MenuOrder
            {
                UserId = workers[i].Id,
                Date = orderableDates[i],
                OrderingPeriodId = period1.Id,
                MenuVariantId = variant.Id,
                PriceHuf = menuPortionHuf,
                Status = OrderStatus.Active,
                PlacedByUserId = workers[i].Id,
                PlacedAtUtc = DateTime.UtcNow,
            });
        }

        db.MenuOrders.AddRange(activeOrders);
        await db.SaveChangesAsync(ct);

        var notifications = new List<UserNotification>();

        if (orderableDates.Count > 3 && workers.Count > 0)
        {
            var cancelDate = orderableDates[3];
            var variant = variantsByDate[cancelDate][0];
            var user = workers[0];

            var cancelledOrder = new MenuOrder
            {
                UserId = user.Id,
                Date = cancelDate,
                OrderingPeriodId = period1.Id,
                MenuVariantId = variant.Id,
                PriceHuf = menuPortionHuf,
                Status = OrderStatus.Cancelled,
                PlacedByUserId = user.Id,
                PlacedAtUtc = DateTime.UtcNow.AddDays(-1),
                CancelledAtUtc = DateTime.UtcNow,
                CancelledByUserId = user.Id,
                CancellationReason = CancellationReason.ByUser,
            };
            db.MenuOrders.Add(cancelledOrder);
            await db.SaveChangesAsync(ct);

            db.CreditEntries.Add(new CreditEntry
            {
                UserId = user.Id,
                AmountHuf = cancelledOrder.PriceHuf,
                Kind = CreditEntryKind.CancellationCredit,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                SourceMenuOrderId = cancelledOrder.Id,
                RemainingHuf = cancelledOrder.PriceHuf,
            });

            notifications.Add(new UserNotification
            {
                UserId = user.Id,
                Type = NotificationType.CreditIssued,
                Title = "Jóváírás keletkezett",
                Message = $"A(z) {cancelDate:yyyy.MM.dd} napi lemondásod után {cancelledOrder.PriceHuf} Ft jóváírás került az egyenlegedre.",
                RelatedDate = cancelDate,
                RelatedMenuOrderId = cancelledOrder.Id,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        if (excludedDays.Count > 0 && variantsByDate.TryGetValue(excludedDays[0].Date, out var excludedVariants) && workers.Count > 1)
        {
            var excludedDay = excludedDays[0];
            var user = workers[1];
            var variant = excludedVariants[0];

            var excludedOrder = new MenuOrder
            {
                UserId = user.Id,
                Date = excludedDay.Date,
                OrderingPeriodId = period1.Id,
                MenuVariantId = variant.Id,
                PriceHuf = menuPortionHuf,
                Status = OrderStatus.Cancelled,
                PlacedByUserId = user.Id,
                PlacedAtUtc = DateTime.UtcNow.AddDays(-2),
                CancelledAtUtc = excludedDay.CreatedAtUtc,
                CancelledByUserId = excludedDay.CreatedByUserId,
                CancellationReason = CancellationReason.DayExcluded,
                CancelledByExcludedDayId = excludedDay.Id,
            };
            db.MenuOrders.Add(excludedOrder);
            await db.SaveChangesAsync(ct);

            db.CreditEntries.Add(new CreditEntry
            {
                UserId = user.Id,
                AmountHuf = excludedOrder.PriceHuf,
                Kind = CreditEntryKind.CancellationCredit,
                CreatedAtUtc = excludedDay.CreatedAtUtc,
                CreatedByUserId = excludedDay.CreatedByUserId,
                SourceMenuOrderId = excludedOrder.Id,
                RemainingHuf = excludedOrder.PriceHuf,
            });

            notifications.Add(new UserNotification
            {
                UserId = user.Id,
                Type = NotificationType.MenuCancelled,
                Title = "Rendelésed lemondásra került",
                Message = $"A(z) {excludedDay.Date:yyyy.MM.dd} nap kizárásra került ({excludedDay.Reason}), a rendelésed jóváírásra került.",
                RelatedDate = excludedDay.Date,
                RelatedMenuOrderId = excludedOrder.Id,
                CreatedAtUtc = excludedDay.CreatedAtUtc,
            });
        }

        if (notifications.Count > 0)
        {
            db.UserNotifications.AddRange(notifications);
            await db.SaveChangesAsync(ct);
        }
    }

    private static IEnumerable<DateOnly> GetWeekdays(DateOnly start, DateOnly end)
    {
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                yield return date;
            }
        }
    }
}
