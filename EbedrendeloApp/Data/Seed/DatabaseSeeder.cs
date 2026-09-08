using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Data.Seed;

/// <summary>
/// Demo/dev data. Every date is derived from <c>DateTime.Now</c> at seed time (never a fixed literal),
/// so a freshly created database always looks "current" — roughly a month of history behind today and a
/// month of orderable days ahead — no matter when the app is first run. Each Seed*Async method still
/// short-circuits if its table already has rows (see e.g. <see cref="SeedUsersAsync"/>), so re-running
/// this against an already-seeded database is a no-op: to get fresh dates again the database itself has
/// to be recreated first.
/// </summary>
public static class DatabaseSeeder
{
    public const string AdminRoleName = "Admin";
    public const string UserRoleName = "User";

    private static readonly string[] VariantCodes = ["A", "B", "C"];

    // Fixed seeds (not time-based) so the *shape* of the generated data — who ordered what, which days
    // got cancelled — stays stable across repeated fresh-database runs, even though the dates themselves
    // shift with "today".
    private const int MenuOrderRandomSeed = 20260907;
    private const int ALaCarteOfferRandomSeed = 20260908;
    private const int ALaCarteOrderRandomSeed = 20260909;

    public static async Task SeedAsync(EbedrendeloDbContext db, CancellationToken ct = default)
    {
        var roles = await SeedRolesAsync(db, ct);
        var settings = await SeedAppSettingAsync(db, ct);
        var users = await SeedUsersAsync(db, roles, ct);
        var periods = await SeedOrderingPeriodsAsync(db, ct);
        var excludedDays = await SeedExcludedDaysAsync(db, users, roles, periods, ct);
        var variantsByDate = await SeedDailyMenusAsync(db, periods, ct);
        var items = await SeedALaCarteItemsAsync(db, ct);
        await SeedALaCarteDailyOffersAsync(db, items, periods, excludedDays, ct);
        await SeedMenuOrdersAsync(db, users, roles, periods, excludedDays, variantsByDate, settings, ct);
        await SeedKitchenClosuresAsync(db, users, roles, ct);
        await SeedALaCarteOrdersAsync(db, users, roles, items, periods, excludedDays, ct);
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
            new() { UserId = 1001, UserName = "admin", KeresztNev = "Rendszer", VezetekNev = "Adminisztrátor", Igazgatosag = "Központ", Osztaly = "Informatika", Rf = "RF-000", SzervKod = "KOZP", RoleId = adminRoleId },
            new() { UserId = 1002, UserName = "kovacs.j", KeresztNev = "János", VezetekNev = "Kovács", Igazgatosag = "Gyártás", Osztaly = "1. üzem", Rf = "RF-101", SzervKod = "GY01", RoleId = userRoleId },
            new() { UserId = 1003, UserName = "nagy.a", KeresztNev = "Anna", VezetekNev = "Nagy", Igazgatosag = "Gyártás", Osztaly = "1. üzem", Rf = "RF-102", SzervKod = "GY01", RoleId = userRoleId },
            new() { UserId = 1004, UserName = "szabo.p", KeresztNev = "Péter", VezetekNev = "Szabó", Igazgatosag = "Gyártás", Osztaly = "2. üzem", Rf = "RF-103", SzervKod = "GY02", RoleId = userRoleId },
            new() { UserId = 1005, UserName = "toth.e", KeresztNev = "Eszter", VezetekNev = "Tóth", Igazgatosag = "Gyártás", Osztaly = "2. üzem", Rf = "RF-104", SzervKod = "GY02", RoleId = userRoleId },
            new() { UserId = 1006, UserName = "varga.b", KeresztNev = "Balázs", VezetekNev = "Varga", Igazgatosag = "Logisztika", Osztaly = "Raktár", Rf = "RF-105", SzervKod = "GY03", RoleId = userRoleId },
            new() { UserId = 1007, UserName = "horvath.k", KeresztNev = "Katalin", VezetekNev = "Horváth", Igazgatosag = "Logisztika", Osztaly = "Szállítás", Rf = "RF-106", SzervKod = "GY03", RoleId = userRoleId },
            new() { UserId = 1008, UserName = "kiss.z", KeresztNev = "Zoltán", VezetekNev = "Kiss", Igazgatosag = "Pénzügy", Osztaly = "Könyvelés", Rf = "RF-107", SzervKod = "PU01", RoleId = userRoleId },
            new() { UserId = 1009, UserName = "molnar.r", KeresztNev = "Réka", VezetekNev = "Molnár", Igazgatosag = "Pénzügy", Osztaly = "Kontrolling", Rf = "RF-108", SzervKod = "PU01", RoleId = userRoleId },
            new() { UserId = 1010, UserName = "farkas.g", KeresztNev = "Gábor", VezetekNev = "Farkas", Igazgatosag = "HR", Osztaly = "Toborzás", Rf = "RF-109", SzervKod = "HR01", RoleId = userRoleId },
            new() { UserId = 1011, UserName = "papp.zs", KeresztNev = "Zsófia", VezetekNev = "Papp", Igazgatosag = "HR", Osztaly = "Bérszámfejtés", Rf = "RF-110", SzervKod = "HR01", RoleId = userRoleId },
            new() { UserId = 1012, UserName = "balogh.t", KeresztNev = "Tamás", VezetekNev = "Balogh", Igazgatosag = "Informatika", Osztaly = "Fejlesztés", Rf = "RF-111", SzervKod = "IT01", RoleId = userRoleId },
            new() { UserId = 1013, UserName = "szucs.n", KeresztNev = "Nóra", VezetekNev = "Szűcs", Igazgatosag = "Informatika", Osztaly = "Üzemeltetés", Rf = "RF-112", SzervKod = "IT01", RoleId = userRoleId },
            new() { UserId = 1014, UserName = "juhasz.m", KeresztNev = "Márton", VezetekNev = "Juhász", Igazgatosag = "Gyártás", Osztaly = "3. üzem", Rf = "RF-113", SzervKod = "GY04", RoleId = userRoleId },
        };

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);
        return users;
    }

    /// <summary>
    /// Three consecutive periods anchored on the 5th of the month (same convention as before — the 5th
    /// exists in every month, so <see cref="DateOnly.AddMonths"/> never needs end-of-month clamping):
    /// last month (closed, purely historical), this month (open, "today" always falls inside it) and
    /// next month (open, still far enough out that its bulk order-deadline usually hasn't passed yet).
    /// </summary>
    private static async Task<List<OrderingPeriod>> SeedOrderingPeriodsAsync(EbedrendeloDbContext db, CancellationToken ct)
    {
        if (await db.OrderingPeriods.AnyAsync(ct))
        {
            return await db.OrderingPeriods.OrderBy(p => p.StartDate).ToListAsync(ct);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var thisMonthAnchor = new DateOnly(today.Year, today.Month, 5);
        var currentStart = today.Day >= 5 ? thisMonthAnchor : thisMonthAnchor.AddMonths(-1);

        var previousStart = currentStart.AddMonths(-1);
        var previousEnd = currentStart.AddDays(-1);

        var currentEnd = currentStart.AddMonths(1);

        var nextStart = currentEnd.AddDays(1);
        var nextEnd = nextStart.AddMonths(1);

        OrderingPeriod MakePeriod(DateOnly start, DateOnly end, bool isOpen) => new()
        {
            Name = $"{start:yyyy. MMMM}",
            StartDate = start,
            EndDate = end,
            OrderDeadline = start.AddDays(-10).ToDateTime(new TimeOnly(10, 0)),
            IsOpen = isOpen,
        };

        var periods = new List<OrderingPeriod>
        {
            MakePeriod(previousStart, previousEnd, isOpen: false),
            MakePeriod(currentStart, currentEnd, isOpen: true),
            MakePeriod(nextStart, nextEnd, isOpen: true),
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
        var admin = users.First(u => u.RoleId == adminRoleId);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var previousPeriod = periods[0];
        var currentPeriod = periods[1];
        var nextPeriod = periods[2];

        // One already-past exclusion (so its "orders got cancelled" trail is visible in history), one
        // upcoming exclusion still inside the current period (so the calendar shows a real future
        // DayExcluded day), and one further out in next month.
        var pastDate = NthWeekdayOrLast(GetWeekdays(previousPeriod.StartDate, previousPeriod.EndDate), 4);
        var upcomingDate = NthWeekdayOrLast(GetWeekdays(today.AddDays(1), currentPeriod.EndDate), 4);
        var futureDate = NthWeekdayOrLast(GetWeekdays(nextPeriod.StartDate, nextPeriod.EndDate), 6);

        var excludedDays = new List<ExcludedDay>
        {
            new() { Date = pastDate, Reason = "Karbantartás", CreatedAtUtc = DateTime.UtcNow.AddMonths(-1), CreatedByUserId = admin.Id },
            new() { Date = upcomingDate, Reason = "Munkaszüneti nap (áthelyezett pihenőnap)", CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = admin.Id },
            new() { Date = futureDate, Reason = "Céges rendezvény — a konyha zárva tart", CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = admin.Id },
        }
            .DistinctBy(e => e.Date)
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

        var soupDishIdByName = await EnsureMenuDishesAsync(db, MenuDishKind.Leves, SeedCatalog.Soups, ct);
        var mainCourseDishIdByName = await EnsureMenuDishesAsync(db, MenuDishKind.Foetel, SeedCatalog.MainCourses, ct);

        var rangeStart = periods.Min(p => p.StartDate);
        var rangeEnd = periods.Max(p => p.EndDate);
        var weekdays = GetWeekdays(rangeStart, rangeEnd).ToList();

        var soupCount = SeedCatalog.Soups.Count;
        var mainCount = SeedCatalog.MainCourses.Count;

        var dailyMenus = new List<DailyMenu>();
        for (var i = 0; i < weekdays.Count; i++)
        {
            var variants = new List<MenuVariant>();
            for (var v = 0; v < VariantCodes.Length; v++)
            {
                // Soup and main course rotate independently (offset by 5) so the same soup doesn't
                // always end up paired with the same main course across the whole seeded range.
                var slot = i * VariantCodes.Length + v;
                var soupName = SeedCatalog.Soups[slot % soupCount].Name;
                var mainName = SeedCatalog.MainCourses[(slot + 5) % mainCount].Name;

                variants.Add(new MenuVariant
                {
                    DailyMenuId = 0,
                    Code = VariantCodes[v],
                    SoupName = soupName,
                    SoupDishId = soupDishIdByName[soupName],
                    MainCourseName = mainName,
                    MainCourseDishId = mainCourseDishIdByName[mainName],
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

    /// <summary>Looks up (or creates) the <see cref="MenuDish"/> catalog rows the seeded recipes need, so
    /// SeedDailyMenusAsync can populate MenuVariant's required SoupDishId/MainCourseDishId FKs. Also backfills
    /// EnergyKcal/Allergens onto any already-existing row that predates those columns being seeded.</summary>
    private static async Task<Dictionary<string, int>> EnsureMenuDishesAsync(
        EbedrendeloDbContext db, MenuDishKind kind, IEnumerable<SeedDish> dishes, CancellationToken ct)
    {
        var distinctDishes = dishes.DistinctBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var distinctNames = distinctDishes.Select(d => d.Name).ToList();
        var existing = await db.MenuDishes.Where(d => d.Kind == kind && distinctNames.Contains(d.Name)).ToListAsync(ct);
        var existingByName = existing.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

        var missing = new List<MenuDish>();
        foreach (var dish in distinctDishes)
        {
            if (existingByName.TryGetValue(dish.Name, out var found))
            {
                if (found.EnergyKcal is null)
                {
                    found.EnergyKcal = dish.EnergyKcal;
                    found.Allergens = dish.Allergens;
                }

                continue;
            }

            missing.Add(new MenuDish { Kind = kind, Name = dish.Name, EnergyKcal = dish.EnergyKcal, Allergens = dish.Allergens });
        }

        if (missing.Count > 0)
        {
            db.MenuDishes.AddRange(missing);
        }

        await db.SaveChangesAsync(ct);

        return existing.Concat(missing).ToDictionary(d => d.Name, d => d.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<List<ALaCarteItem>> SeedALaCarteItemsAsync(EbedrendeloDbContext db, CancellationToken ct)
    {
        if (await db.ALaCarteItems.AnyAsync(ct))
        {
            return await db.ALaCarteItems.ToListAsync(ct);
        }

        // A Leves kategóriából szándékosan csak 1 tétel van (AC 4.1.4 — naponta legfeljebb egy aktív
        // Leves ajánlat lehet). Az ára a Főétel-rendelések sorába olvad bele, önállóan sosem rendelhető
        // és sosem kerül készlet-ellenőrzésre (lásd SeedALaCarteDailyOffersAsync lent).
        var items = new List<ALaCarteItem>
        {
            new() { Name = "Csontleves", Category = ALaCarteCategory.Leves, PriceHuf = 650, IsActive = true, Allergens = "9" },
            new() { Name = "Rántott sertés szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1900, IsActive = true, Allergens = "1,3" },
            new() { Name = "Rántott csirke(mell)", Category = ALaCarteCategory.Foetel, PriceHuf = 1850, IsActive = true, Allergens = "1,3" },
            new() { Name = "Csirke roston", Category = ALaCarteCategory.Foetel, PriceHuf = 1900, IsActive = true },
            new() { Name = "Rántott trappista sajt", Category = ALaCarteCategory.Foetel, PriceHuf = 1750, IsActive = true, Allergens = "1,3,7" },
            new() { Name = "Rántott camembert", Category = ALaCarteCategory.Foetel, PriceHuf = 1800, IsActive = true, Allergens = "1,3,7" },
            new() { Name = "Hasábburgonya", Category = ALaCarteCategory.Koret, PriceHuf = 550, IsActive = true },
            new() { Name = "Steak burgonya", Category = ALaCarteCategory.Koret, PriceHuf = 600, IsActive = true },
            new() { Name = "Mexikói zöldségkeverék", Category = ALaCarteCategory.Koret, PriceHuf = 550, IsActive = true },
            new() { Name = "Párolt zöldség", Category = ALaCarteCategory.Koret, PriceHuf = 500, IsActive = true },
            new() { Name = "Lekváros derelye", Category = ALaCarteCategory.Desszert, PriceHuf = 700, IsActive = true, Allergens = "1,3" },
            new() { Name = "Túrós derelye", Category = ALaCarteCategory.Desszert, PriceHuf = 750, IsActive = true, Allergens = "1,3,7" },
            new() { Name = "Tartár mártás", Category = ALaCarteCategory.Ontet, PriceHuf = 350, IsActive = true, Allergens = "3,10" },
        };

        db.ALaCarteItems.AddRange(items);
        await db.SaveChangesAsync(ct);
        return items;
    }

    private static async Task SeedALaCarteDailyOffersAsync(
        EbedrendeloDbContext db, List<ALaCarteItem> items, List<OrderingPeriod> periods, List<ExcludedDay> excludedDays, CancellationToken ct)
    {
        if (await db.ALaCarteDailyOffers.AnyAsync(ct))
        {
            return;
        }

        var excludedDates = excludedDays.Select(e => e.Date).ToHashSet();
        var rangeStart = periods.Min(p => p.StartDate);
        var rangeEnd = periods.Max(p => p.EndDate);
        var offerDates = GetWeekdays(rangeStart, rangeEnd).Where(d => !excludedDates.Contains(d)).ToList();

        var random = new Random(ALaCarteOfferRandomSeed);

        // A Leves ajánlat Capacity-je figyelmen kívül hagyott placeholder — a leves sosem kerül
        // készlet-ellenőrzésre (AC 4.1.4 / AC 4.2.4).
        var offers = new List<ALaCarteDailyOffer>();
        foreach (var date in offerDates)
        {
            foreach (var item in items)
            {
                offers.Add(new ALaCarteDailyOffer
                {
                    Date = date,
                    ALaCarteItemId = item.Id,
                    Capacity = item.Category == ALaCarteCategory.Leves ? int.MaxValue : random.Next(8, 20),
                    OrderedCount = 0,
                });
            }
        }

        db.ALaCarteDailyOffers.AddRange(offers);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds one <see cref="MenuOrder"/> per (weekday, worker) pair with decreasing density the further
    /// out the date is, plus a handful of ByUser/DayExcluded cancellations so the credit ledger has real
    /// entries. "Today" and "tomorrow" are deliberately left empty for every worker, so there is always
    /// an obviously orderable day on the calendar to try placing a fresh order on.
    /// </summary>
    private static async Task SeedMenuOrdersAsync(
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

        var workers = users.Where(u => u.RoleId == roles[UserRoleName].Id).OrderBy(u => u.Id).ToList();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var excludedDates = excludedDays.Select(e => e.Date).ToHashSet();
        var menuPortionHuf = settings.MenuPortionHuf;

        var rangeStart = periods.Min(p => p.StartDate);
        var rangeEnd = periods.Max(p => p.EndDate);

        var random = new Random(MenuOrderRandomSeed);
        var orders = new List<MenuOrder>();

        for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
        {
            if (excludedDates.Contains(date) || date == today || date == today.AddDays(1)
                || !variantsByDate.TryGetValue(date, out var variants))
            {
                continue;
            }

            var period = periods.First(p => p.StartDate <= date && date <= p.EndDate);
            var isPast = date < today;
            var density = isPast ? 0.8 : 0.4;
            var daysFromToday = date.DayNumber - today.DayNumber;

            foreach (var worker in workers)
            {
                if (random.NextDouble() >= density)
                {
                    continue;
                }

                var variant = variants[random.Next(variants.Count)];
                var order = new MenuOrder
                {
                    UserId = worker.Id,
                    Date = date,
                    OrderingPeriodId = period.Id,
                    MenuVariantId = variant.Id,
                    PriceHuf = menuPortionHuf,
                    Status = OrderStatus.Active,
                    PlacedByUserId = worker.Id,
                    PlacedAtUtc = DateTime.UtcNow.AddDays(Math.Min(daysFromToday, -1)),
                };

                // A small slice of past orders were later cancelled by the user themselves.
                if (isPast && random.NextDouble() < 0.12)
                {
                    order.Status = OrderStatus.Cancelled;
                    order.CancelledAtUtc = DateTime.UtcNow.AddDays(Math.Min(daysFromToday + 1, -1));
                    order.CancelledByUserId = worker.Id;
                    order.CancellationReason = CancellationReason.ByUser;
                }

                orders.Add(order);
            }
        }

        // One past excluded day retroactively cancels the orders that already existed for it — mirrors
        // ExcludeDayHandler's real effect, so the ledger/notifications show a DayExcluded example too.
        var pastExcludedDay = excludedDays.FirstOrDefault(e => e.Date < today);
        if (pastExcludedDay is not null && variantsByDate.TryGetValue(pastExcludedDay.Date, out var excludedVariants))
        {
            var excludedPeriod = periods.First(p => p.StartDate <= pastExcludedDay.Date && pastExcludedDay.Date <= p.EndDate);
            foreach (var worker in workers.Take(3))
            {
                orders.Add(new MenuOrder
                {
                    UserId = worker.Id,
                    Date = pastExcludedDay.Date,
                    OrderingPeriodId = excludedPeriod.Id,
                    MenuVariantId = excludedVariants[0].Id,
                    PriceHuf = menuPortionHuf,
                    Status = OrderStatus.Cancelled,
                    PlacedByUserId = worker.Id,
                    PlacedAtUtc = pastExcludedDay.CreatedAtUtc.AddDays(-3),
                    CancelledAtUtc = pastExcludedDay.CreatedAtUtc,
                    CancelledByUserId = pastExcludedDay.CreatedByUserId,
                    CancellationReason = CancellationReason.DayExcluded,
                    CancelledByExcludedDayId = pastExcludedDay.Id,
                });
            }
        }

        db.MenuOrders.AddRange(orders);
        await db.SaveChangesAsync(ct);

        // Now that the cancelled orders have real Ids, issue their cancellation credit + notification.
        var notifications = new List<UserNotification>();
        var creditEntries = new List<CreditEntry>();
        foreach (var cancelled in orders.Where(o => o.Status == OrderStatus.Cancelled))
        {
            creditEntries.Add(new CreditEntry
            {
                UserId = cancelled.UserId,
                AmountHuf = cancelled.PriceHuf,
                Kind = CreditEntryKind.CancellationCredit,
                CreatedAtUtc = cancelled.CancelledAtUtc!.Value,
                CreatedByUserId = cancelled.CancelledByUserId!.Value,
                SourceMenuOrderId = cancelled.Id,
                RemainingHuf = cancelled.PriceHuf,
            });

            var (type, title, message) = cancelled.CancellationReason == CancellationReason.DayExcluded
                ? (NotificationType.MenuCancelled, "Rendelésed lemondásra került",
                    $"A(z) {cancelled.Date:yyyy.MM.dd} nap kizárásra került, a rendelésed jóváírásra került.")
                : (NotificationType.CreditIssued, "Jóváírás keletkezett",
                    $"A(z) {cancelled.Date:yyyy.MM.dd} napi lemondásod után {cancelled.PriceHuf} Ft jóváírás került az egyenlegedre.");

            notifications.Add(new UserNotification
            {
                UserId = cancelled.UserId,
                Type = type,
                Title = title,
                Message = message,
                RelatedDate = cancelled.Date,
                RelatedMenuOrderId = cancelled.Id,
                CreatedAtUtc = cancelled.CancelledAtUtc.Value,
            });
        }

        // A little variety beyond auto-generated cancellation credits, so the balances/ledger views show
        // a manual adjustment and a revoked credit too, not just cancellation credits.
        var admin = users.First(u => u.RoleId == roles[AdminRoleName].Id);
        var creditSubject = workers[0];

        var manualCredit = new CreditEntry
        {
            UserId = creditSubject.Id,
            AmountHuf = 1400,
            Kind = CreditEntryKind.ManualAdjustment,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-6),
            CreatedByUserId = admin.Id,
            Note = "Rendszerhiba miatti korrekció",
            RemainingHuf = 1400,
        };
        creditEntries.Add(manualCredit);

        notifications.Add(new UserNotification
        {
            UserId = creditSubject.Id,
            Type = NotificationType.CreditIssued,
            Title = "Jóváírás érkezett",
            Message = $"{manualCredit.AmountHuf} Ft jóváírás került a menü-egyenlegedhez. Indoklás: {manualCredit.Note}",
            CreatedAtUtc = manualCredit.CreatedAtUtc,
        });

        db.CreditEntries.AddRange(creditEntries);
        db.UserNotifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);

        // A second worker gets a manual credit that is then partially revoked, so
        // CreditEntryKind.CreditRevoked shows up in the ledger at least once too.
        var revocationSubject = workers[1];
        var revocableCredit = new CreditEntry
        {
            UserId = revocationSubject.Id,
            AmountHuf = 2000,
            Kind = CreditEntryKind.ManualAdjustment,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
            CreatedByUserId = admin.Id,
            Note = "Téves díjbeszedés visszatérítése",
            RemainingHuf = 2000,
        };
        db.CreditEntries.Add(revocableCredit);
        await db.SaveChangesAsync(ct);

        revocableCredit.RemainingHuf = 0;
        db.CreditEntries.Add(new CreditEntry
        {
            UserId = revocationSubject.Id,
            AmountHuf = -revocableCredit.AmountHuf,
            Kind = CreditEntryKind.CreditRevoked,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
            CreatedByUserId = admin.Id,
            ConsumesCreditEntryId = revocableCredit.Id,
            Note = "A korrekció tévesen lett kiadva, visszavonva",
            RemainingHuf = 0,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Closes the two oldest past weekdays that have active orders — one stays simply closed, the other
    /// goes through a full close → reopen → close-again lifecycle (KitchenClosure is append-only, so
    /// "closed again" is a brand-new row — see <see cref="KitchenClosureReopening"/>). Leaves more recent
    /// past days un-closed so a tester can still try "Nap zárása" (Close day) on them.
    /// </summary>
    private static async Task SeedKitchenClosuresAsync(
        EbedrendeloDbContext db, List<User> users, Dictionary<string, Role> roles, CancellationToken ct)
    {
        if (await db.KitchenClosures.AnyAsync(ct))
        {
            return;
        }

        var admin = users.First(u => u.RoleId == roles[AdminRoleName].Id);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var pastDatesWithOrders = await db.MenuOrders
            .Where(o => o.Date < today && o.Status == OrderStatus.Active)
            .Select(o => o.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);

        if (pastDatesWithOrders.Count < 3)
        {
            return;
        }

        var simpleCloseDate = pastDatesWithOrders[0];
        var lifecycleCloseDate = pastDatesWithOrders[1];

        await CloseDayForSeedAsync(db, simpleCloseDate, admin.Id, DateTime.UtcNow.AddDays(simpleCloseDate.DayNumber - today.DayNumber), ct);

        var firstClosedAtUtc = DateTime.UtcNow.AddDays(lifecycleCloseDate.DayNumber - today.DayNumber);
        var firstClosure = await CloseDayForSeedAsync(db, lifecycleCloseDate, admin.Id, firstClosedAtUtc, ct);

        var reopenedAtUtc = firstClosedAtUtc.AddHours(2);
        db.KitchenClosureReopenings.Add(new KitchenClosureReopening
        {
            KitchenClosureId = firstClosure.Id,
            ReopenedAtUtc = reopenedAtUtc,
            ReopenedByUserId = admin.Id,
        });
        await db.SaveChangesAsync(ct);

        var affectedUserIds = await db.MenuOrders
            .Where(o => o.Date == lifecycleCloseDate && o.Status == OrderStatus.Active)
            .Select(o => o.UserId)
            .ToListAsync(ct);

        db.UserNotifications.AddRange(affectedUserIds.Select(userId => new UserNotification
        {
            UserId = userId,
            Type = NotificationType.DayReopened,
            Title = "A nap újranyitásra került",
            Message = $"A(z) {lifecycleCloseDate:yyyy.MM.dd} napi konyhai zárás visszavonásra került, a rendelésed ismét aktív.",
            RelatedDate = lifecycleCloseDate,
            CreatedAtUtc = reopenedAtUtc,
        }));
        await db.SaveChangesAsync(ct);

        await CloseDayForSeedAsync(db, lifecycleCloseDate, admin.Id, reopenedAtUtc.AddHours(4), ct);
    }

    /// <summary>Mirrors CloseDayHandler's snapshot logic directly against the DbContext, without going
    /// through MediatR — the seeder runs once at startup, so the handler's concurrency guard (Serializable
    /// transaction) would just add overhead here.</summary>
    private static async Task<KitchenClosure> CloseDayForSeedAsync(
        EbedrendeloDbContext db, DateOnly date, int closedByUserId, DateTime closedAtUtc, CancellationToken ct)
    {
        var grouped = await db.MenuOrders
            .Where(o => o.Date == date && o.Status == OrderStatus.Active)
            .Join(db.MenuVariants, o => o.MenuVariantId, v => v.Id, (o, v) => v)
            .GroupBy(v => new { v.Code, v.SoupName, v.MainCourseName })
            .Select(g => new { g.Key.Code, g.Key.SoupName, g.Key.MainCourseName, Quantity = g.Count() })
            .ToListAsync(ct);

        var orderedLines = grouped.OrderBy(g => g.Code, StringComparer.Ordinal).ToList();

        var closure = new KitchenClosure
        {
            Date = date,
            ClosedAtUtc = closedAtUtc,
            ClosedByUserId = closedByUserId,
            TotalPortions = orderedLines.Sum(g => g.Quantity),
        };
        db.KitchenClosures.Add(closure);
        await db.SaveChangesAsync(ct);

        db.KitchenClosureLines.AddRange(orderedLines.Select(g => new KitchenClosureLine
        {
            KitchenClosureId = closure.Id,
            VariantCode = g.Code,
            VariantNameSnapshot = VariantDisplayName.Combine(g.SoupName, g.MainCourseName),
            Quantity = g.Quantity,
        }));
        await db.SaveChangesAsync(ct);

        return closure;
    }

    /// <summary>
    /// Places real <see cref="ALaCarteOrder"/>/<see cref="ALaCarteOrderLine"/> rows (not just the daily
    /// offers) for past weekdays, mirroring PlaceALaCarteOrderHandler's "soup bundled into the Foetel
    /// line, one item per category" rule and keeping each offer's OrderedCount in sync. "Today" is
    /// deliberately left with zero orders so a tester can actually place one and see it work.
    /// </summary>
    private static async Task SeedALaCarteOrdersAsync(
        EbedrendeloDbContext db,
        List<User> users,
        Dictionary<string, Role> roles,
        List<ALaCarteItem> items,
        List<OrderingPeriod> periods,
        List<ExcludedDay> excludedDays,
        CancellationToken ct)
    {
        if (await db.ALaCarteOrders.AnyAsync(ct))
        {
            return;
        }

        var workers = users.Where(u => u.RoleId == roles[UserRoleName].Id).OrderBy(u => u.Id).ToList();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var excludedDates = excludedDays.Select(e => e.Date).ToHashSet();
        var rangeStart = periods.Min(p => p.StartDate);

        var mainItems = items.Where(i => i.Category == ALaCarteCategory.Foetel).ToList();
        var sideItems = items.Where(i => i.Category == ALaCarteCategory.Koret).ToList();
        var dessertItems = items.Where(i => i.Category == ALaCarteCategory.Desszert).ToList();
        var sauceItems = items.Where(i => i.Category == ALaCarteCategory.Ontet).ToList();
        if (mainItems.Count == 0)
        {
            return;
        }

        var random = new Random(ALaCarteOrderRandomSeed);
        var allOrders = new List<ALaCarteOrder>();

        for (var date = rangeStart; date < today; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || excludedDates.Contains(date))
            {
                continue;
            }

            var offers = await db.ALaCarteDailyOffers
                .Include(o => o.ALaCarteItem)
                .Where(o => o.Date == date)
                .ToListAsync(ct);
            if (offers.Count == 0)
            {
                continue;
            }

            var period = periods.First(p => p.StartDate <= date && date <= p.EndDate);
            var soupOffer = offers.FirstOrDefault(o => o.ALaCarteItem!.Category == ALaCarteCategory.Leves);
            var offerByItemId = offers.ToDictionary(o => o.ALaCarteItemId);
            var placedAtUtc = DateTime.UtcNow.AddDays(Math.Min(date.DayNumber - today.DayNumber - 1, -1));

            foreach (var worker in workers)
            {
                if (random.NextDouble() >= 0.35)
                {
                    continue;
                }

                var main = mainItems[random.Next(mainItems.Count)];
                if (!offerByItemId.TryGetValue(main.Id, out var mainOffer) || mainOffer.OrderedCount >= mainOffer.Capacity)
                {
                    continue;
                }

                var order = new ALaCarteOrder
                {
                    UserId = worker.Id,
                    Date = date,
                    OrderingPeriodId = period.Id,
                    PlacedAtUtc = placedAtUtc,
                    PlacedByUserId = worker.Id,
                    TotalHuf = 0,
                };

                var includesSoup = soupOffer is not null;
                var mainUnitPrice = includesSoup ? main.PriceHuf + soupOffer!.ALaCarteItem!.PriceHuf : main.PriceHuf;
                order.Lines.Add(new ALaCarteOrderLine
                {
                    ALaCarteOrderId = order.Id,
                    ALaCarteDailyOfferId = mainOffer.Id,
                    ItemNameSnapshot = main.Name,
                    CategorySnapshot = main.Category,
                    UnitPriceHuf = mainUnitPrice,
                    IncludesSoup = includesSoup,
                });
                mainOffer.OrderedCount++;
                order.TotalHuf += mainUnitPrice;

                AddOptionalLine(order, sideItems, offerByItemId, random, 0.8);
                AddOptionalLine(order, dessertItems, offerByItemId, random, 0.3);
                AddOptionalLine(order, sauceItems, offerByItemId, random, 0.25);

                allOrders.Add(order);
            }
        }

        db.ALaCarteOrders.AddRange(allOrders);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Randomly adds at most one line from <paramref name="candidates"/> (a single à la carte
    /// category) to <paramref name="order"/>, provided that item has a live offer with remaining capacity
    /// today — mirrors the "one item per category" rule PlaceALaCarteOrderHandler enforces.</summary>
    private static void AddOptionalLine(
        ALaCarteOrder order, List<ALaCarteItem> candidates, Dictionary<int, ALaCarteDailyOffer> offerByItemId, Random random, double chance)
    {
        if (candidates.Count == 0 || random.NextDouble() >= chance)
        {
            return;
        }

        var item = candidates[random.Next(candidates.Count)];
        if (!offerByItemId.TryGetValue(item.Id, out var offer) || offer.OrderedCount >= offer.Capacity)
        {
            return;
        }

        order.Lines.Add(new ALaCarteOrderLine
        {
            ALaCarteOrderId = order.Id,
            ALaCarteDailyOfferId = offer.Id,
            ItemNameSnapshot = item.Name,
            CategorySnapshot = item.Category,
            UnitPriceHuf = item.PriceHuf,
            IncludesSoup = false,
        });
        offer.OrderedCount++;
        order.TotalHuf += item.PriceHuf;
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

    private static DateOnly NthWeekdayOrLast(IEnumerable<DateOnly> weekdays, int skip)
    {
        var list = weekdays.ToList();
        return list[Math.Min(skip, list.Count - 1)];
    }
}
