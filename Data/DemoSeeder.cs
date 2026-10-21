using CityFix_backend.Models;
using CityFix_backend.Services;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Data
{
    // Demo data for Khmelnytskyi: associations, categories, city services, residents and heads, and appeals in
    // every status with their history, notifications and hand-written translations.
    //   dotnet run -- --seed-demo
    // Only adds what is missing (matched by email, name or the author's appeal title), so it can run again.
    // Categories and recipients created before names were bilingual are given their proper names.
    // Every demo account has the password DemoPassword.
    public static class DemoSeeder
    {
        public const string DemoPassword = "CityFix2026!";

        private record CategorySeed(string Uk, string En, params string[] Legacy);

        private record RecipientSeed(string Uk, string En, string Contact, params string[] Legacy);

        private record AcabSeed(string Name, string Address, string NameEn, string AddressEn);

        private record UserSeed(string Email, string FirstName, string LastName, string Phone, UserRole Role, string? Acab, string Language = "uk");

        // A status change by staff, Hours after the appeal was created.
        private record Step(AppealStatus Status, double Hours, string? Comment = null);

        private record AppealSeed(
            string Author, string Category, string? Recipient, AppealPriority Priority, double DaysAgo,
            string Title, string Description, string TitleTranslated, string DescriptionTranslated,
            double Lat, double Lng, string Address, params Step[] Steps);

        private static readonly CategorySeed[] Categories =
        {
            new("Дороги та тротуари", "Roads and sidewalks", "Road Damage"),
            new("Вуличне освітлення", "Street lighting"),
            new("Вивезення сміття", "Waste collection"),
            new("Водопостачання та каналізація", "Water supply and sewerage"),
            new("Опалення та гаряча вода", "Heating and hot water"),
            new("Благоустрій та озеленення", "Landscaping and greenery", "Home improvement"),
            new("Ліфти", "Elevators"),
            new("Під'їзди та прибудинкова територія", "Entrances and yards")
        };

        private static readonly RecipientSeed[] Recipients =
        {
            new("КП «Спецкомунтранс»", "Spetskomuntrans municipal enterprise", "вул. Подільська, 48; +380 382 65 12 34", "HKP \"Spetskomuntrans\""),
            new("Служба автомобільних доріг", "Road Maintenance Service", "roads@khm.gov.ua; +380 382 70 45 67", "Municipal Road Service"),
            new("МКП «Хмельницькводоканал»", "Khmelnytskvodokanal water utility", "вул. Водопровідна, 75; аварійна служба 1557"),
            new("МКП «Хмельницьктеплокомуненерго»", "Khmelnytskteplokomunenergo heating utility", "вул. Проскурівського Підпілля, 139; +380 382 72 03 15"),
            new("КП «Міськсвітло»", "Misksvitlo street lighting company", "вул. Купріна, 46; +380 382 65 33 08"),
            new("КП «Парки і сквери міста Хмельницького»", "Khmelnytskyi Parks and Squares", "вул. Проскурівська, 1; parks@khm.gov.ua"),
            new("Управління житлово-комунального господарства", "Housing and Utilities Department", "вул. Гагаріна, 3; zhkg@khm.gov.ua")
        };

        private const string Proskurivska = "ОСББ «Проскурівська 24»";
        private const string Zarichanskyi = "ОСББ «Зарічанський»";
        private const string Kashtan = "ОСББ «Каштан»";

        private static readonly AcabSeed[] Acabs =
        {
            new(Proskurivska, "вул. Проскурівська, 24, Хмельницький", "Proskurivska 24 Homeowners' Association", "24 Proskurivska St., Khmelnytskyi"),
            new(Zarichanskyi, "вул. Зарічанська, 10/1, Хмельницький", "Zarichanskyi Homeowners' Association", "10/1 Zarichanska St., Khmelnytskyi"),
            new(Kashtan, "вул. Кам'янецька, 61, Хмельницький", "Kashtan Homeowners' Association", "61 Kamianetska St., Khmelnytskyi")
        };

        private static readonly UserSeed[] Users =
        {
            new("oksana.melnyk@example.com", "Оксана", "Мельник", "+380671112233", UserRole.AcabAdmin, Proskurivska),
            new("viktor.bondar@example.com", "Віктор", "Бондар", "+380502223344", UserRole.AcabAdmin, Zarichanskyi),
            new("natalia.savchuk@example.com", "Наталія", "Савчук", "+380933334455", UserRole.AcabAdmin, Kashtan),
            new("ivan.kovalenko@example.com", "Іван", "Коваленко", "+380674445566", UserRole.Resident, Proskurivska),
            new("maria.shevchuk@example.com", "Марія", "Шевчук", "+380635556677", UserRole.Resident, Proskurivska),
            new("dmytro.oliinyk@example.com", "Дмитро", "Олійник", "+380976667788", UserRole.Resident, Proskurivska),
            new("andrii.tkachenko@example.com", "Андрій", "Ткаченко", "+380507778899", UserRole.Resident, Zarichanskyi),
            new("yulia.kravchuk@example.com", "Юлія", "Кравчук", "+380688889900", UserRole.Resident, Zarichanskyi),
            new("emily.carter@example.com", "Emily", "Carter", "+380991234567", UserRole.Resident, Kashtan, "en"),
            new("oleh.moroz@example.com", "Олег", "Мороз", "+380731239876", UserRole.Resident, null)
        };

        // Recipient null: the author's association is responsible.
        private static readonly AppealSeed[] Appeals =
        {
            new("ivan.kovalenko@example.com", "Ліфти", null, AppealPriority.Urgent, 6,
                "Не працює ліфт у другому під'їзді",
                "Ліфт у другому під'їзді другий день стоїть між 4 і 5 поверхами. На дев'ятому поверсі живуть літні люди, їм важко підніматися пішки.",
                "The elevator in the second entrance is out of order",
                "The elevator in the second entrance has been stuck between the 4th and 5th floors for two days. Elderly people live on the ninth floor and find it hard to walk up.",
                49.42291, 26.98712, "вул. Проскурівська, 24, Хмельницький",
                new Step(AppealStatus.UnderReview, 2, "Викликали аварійну службу ліфтів, огляд сьогодні до 18:00."),
                new Step(AppealStatus.InProgress, 26, "Майстер замовив нову плату керування, заміна — до п'ятниці.")),

            new("ivan.kovalenko@example.com", "Під'їзди та прибудинкова територія", null, AppealPriority.Normal, 25,
                "Протікає дах над п'ятим під'їздом",
                "Після кожного дощу на стелі в кв. 38 і на сходовій клітці дев'ятого поверху з'являються мокрі плями.",
                "The roof above the fifth entrance is leaking",
                "After every rain, wet patches appear on the ceiling of flat 38 and on the ninth-floor landing.",
                49.42305, 26.98690, "вул. Проскурівська, 24, Хмельницький",
                new Step(AppealStatus.UnderReview, 20, "Огляд покрівлі призначено на четвер."),
                new Step(AppealStatus.InProgress, 72, "Підрядник почав ремонт покрівлі над квартирами 36–40."),
                new Step(AppealStatus.Done, 290, "Покрівлю над п'ятим під'їздом перекрито, шви загерметизовано.")),

            new("ivan.kovalenko@example.com", "Під'їзди та прибудинкова територія", null, AppealPriority.Low, 15,
                "Не прибирають сходові клітки",
                "Уже два тижні в під'їзді ніхто не миє підлогу, на сходах пісок і сміття.",
                "The stairwells are not being cleaned",
                "Nobody has washed the floors in the entrance for two weeks; there is sand and litter on the stairs.",
                49.42280, 26.98730, "вул. Проскурівська, 24, Хмельницький",
                new Step(AppealStatus.UnderReview, 20),
                new Step(AppealStatus.InProgress, 48, "Домовилися з клінінговою компанією про прибирання двічі на тиждень."),
                new Step(AppealStatus.Done, 120, "Прибирання проводиться за графіком: щовівторка і щоп'ятниці.")),

            new("maria.shevchuk@example.com", "Вуличне освітлення", "КП «Міськсвітло»", AppealPriority.Normal, 3,
                "Не горять ліхтарі у дворі",
                "Увечері у дворі будинку повністю темно: не працюють обидва ліхтарі біля дитячого майданчика.",
                "The street lights in the yard are off",
                "In the evening the yard is completely dark: both lights next to the playground are not working.",
                49.42262, 26.98655, "вул. Проскурівська, 24, Хмельницький"),

            new("maria.shevchuk@example.com", "Вивезення сміття", "КП «Спецкомунтранс»", AppealPriority.Urgent, 9,
                "Переповнені сміттєві контейнери",
                "Контейнери біля будинку не вивозили з понеділка, сміття лежить на землі довкола майданчика.",
                "The waste containers are overflowing",
                "The containers next to the building have not been emptied since Monday; rubbish is lying on the ground around the site.",
                49.42240, 26.98760, "вул. Проскурівська, 24, Хмельницький",
                new Step(AppealStatus.UnderReview, 3),
                new Step(AppealStatus.InProgress, 22, "Заявку передано перевізнику, додатковий вивіз заплановано на завтра."),
                new Step(AppealStatus.Done, 46, "Контейнери вивезено, вивіз збільшено до щоденного.")),

            new("dmytro.oliinyk@example.com", "Ліфти", null, AppealPriority.Normal, 5,
                "Ліфт у 2-му під'їзді не їде",
                "Натискаю кнопку виклику — ліфт не реагує.",
                "The elevator in entrance 2 does not move",
                "I press the call button and the elevator does not respond.",
                49.42291, 26.98712, "вул. Проскурівська, 24, Хмельницький",
                new Step(AppealStatus.Duplicate, 3, "Це звернення дублює вже прийняте в роботу звернення про ліфт у другому під'їзді.")),

            new("dmytro.oliinyk@example.com", "Водопостачання та каналізація", "МКП «Хмельницькводоканал»", AppealPriority.Urgent, 40,
                "Прорвало трубу в підвалі",
                "У підвалі б'є вода з труби холодного водопостачання, підвал заливає.",
                "A pipe has burst in the basement",
                "Water is gushing from the cold water pipe in the basement and it is flooding.",
                49.42275, 26.98700, "вул. Проскурівська, 24, Хмельницький",
                new Step(AppealStatus.InProgress, 2, "Аварійна бригада перекрила стояк."),
                new Step(AppealStatus.Done, 26, "Ділянку труби замінено, підвал осушено.")),

            new("andrii.tkachenko@example.com", "Дороги та тротуари", "Служба автомобільних доріг", AppealPriority.Urgent, 14,
                "Яма на виїзді з двору",
                "На виїзді з двору на вул. Зарічанську глибока яма, автомобілі пошкоджують колеса.",
                "Pothole at the yard exit",
                "There is a deep pothole at the exit from the yard onto Zarichanska Street; cars are damaging their wheels.",
                49.43628, 26.97519, "вул. Зарічанська, 10/1, Хмельницький",
                new Step(AppealStatus.UnderReview, 20),
                new Step(AppealStatus.OnHold, 70, "Ремонт можливий лише за плюсової температури, роботи заплановано на квітень.")),

            new("andrii.tkachenko@example.com", "Опалення та гаряча вода", "МКП «Хмельницьктеплокомуненерго»", AppealPriority.Urgent, 2,
                "Немає гарячої води",
                "Гарячої води немає з учорашнього ранку, жодного оголошення про відключення не було.",
                "There is no hot water",
                "There has been no hot water since yesterday morning, and there was no notice about a shutdown.",
                49.43610, 26.97540, "вул. Зарічанська, 10/1, Хмельницький",
                new Step(AppealStatus.UnderReview, 4, "Уточнюємо в теплопостачальника причину відключення.")),

            new("yulia.kravchuk@example.com", "Благоустрій та озеленення", null, AppealPriority.Low, 20,
                "Зламана лавка на дитячому майданчику",
                "У лавки відірвана дошка сидіння, стирчать цвяхи — діти можуть поранитися.",
                "A broken bench on the playground",
                "A seat board has come off the bench and nails are sticking out, so children could get hurt.",
                49.43650, 26.97490, "вул. Зарічанська, 10/1, Хмельницький",
                new Step(AppealStatus.NeedsInfo, 20, "Підкажіть, будь ласка, яка саме лавка — біля пісочниці чи біля гойдалок? Додайте фото, якщо можна.")),

            new("yulia.kravchuk@example.com", "Водопостачання та каналізація", "МКП «Хмельницькводоканал»", AppealPriority.Normal, 7,
                "Слабкий тиск води на верхніх поверхах",
                "На восьмому і дев'ятому поверхах вода ледь тече, особливо ввечері.",
                "Low water pressure on the upper floors",
                "On the eighth and ninth floors the water barely runs, especially in the evening.",
                49.43640, 26.97505, "вул. Зарічанська, 10/1, Хмельницький",
                new Step(AppealStatus.UnderReview, 24),
                new Step(AppealStatus.NeedsInfo, 50, "Вкажіть, будь ласка, номер квартири і в який час тиск найнижчий.")),

            new("emily.carter@example.com", "Під'їзди та прибудинкова територія", null, AppealPriority.Normal, 4,
                "Broken entrance door lock",
                "The lock on the main entrance door does not close, so anyone can walk into the building.",
                "Зламаний замок на вхідних дверях",
                "Замок на вхідних дверях не зачиняється, тож будь-хто може зайти в будинок.",
                49.41170, 26.97050, "вул. Кам'янецька, 61, Хмельницький",
                new Step(AppealStatus.UnderReview, 5, "Замовили новий замок і доводчик.")),

            new("emily.carter@example.com", "Благоустрій та озеленення", "КП «Парки і сквери міста Хмельницького»", AppealPriority.Urgent, 1,
                "Fallen tree branch on the playground",
                "A large branch broke off the old chestnut tree during the night and is lying across the playground.",
                "Гілка дерева впала на дитячий майданчик",
                "Уночі від старого каштана відламалася велика гілка, і тепер вона лежить посеред дитячого майданчика.",
                49.41185, 26.97020, "вул. Кам'янецька, 61, Хмельницький"),

            new("oleh.moroz@example.com", "Вуличне освітлення", "КП «Міськсвітло»", AppealPriority.Normal, 11,
                "Немає освітлення на вулиці Шевченка",
                "На ділянці вулиці Шевченка від будинку 40 до 52 не світить жоден ліхтар.",
                "No street lighting on Shevchenka Street",
                "Not a single street light works on Shevchenka Street between buildings 40 and 52.",
                49.42070, 26.99300, "вул. Шевченка, 46, Хмельницький",
                new Step(AppealStatus.UnderReview, 22),
                new Step(AppealStatus.InProgress, 50, "Замовлено заміну трьох світильників.")),

            new("oleh.moroz@example.com", "Благоустрій та озеленення", "Управління житлово-комунального господарства", AppealPriority.Low, 30,
                "Оголошення на електроопорі",
                "Електроопора біля зупинки обклеєна старими оголошеннями.",
                "Notices pasted on a power pole",
                "The power pole next to the bus stop is covered with old notices.",
                49.42110, 26.99150, "вул. Шевченка, 30, Хмельницький",
                new Step(AppealStatus.Rejected, 48, "Опора належить обленерго, міська служба не може її очищати. Звернення переслано власнику.")),

            new("oleh.moroz@example.com", "Дороги та тротуари", "Служба автомобільних доріг", AppealPriority.Urgent, 0.15,
                "Яма на перехресті Інститутської та Свободи",
                "Посеред перехрестя утворилася глибока яма, водії різко гальмують.",
                "Pothole at the Instytutska and Svobody crossroads",
                "A deep pothole has formed in the middle of the crossroads and drivers brake sharply.",
                49.41550, 26.98400, "вул. Інститутська / вул. Свободи, Хмельницький")
        };

        public static async Task SeedAsync(IServiceProvider services, ILogger logger)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

            var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Administrator && !u.IsBlocked)
                ?? throw new InvalidOperationException("Seed an administrator first (DbSeeder.SeedAdminAsync).");

            // ── Reference data ──
            var categories = new Dictionary<string, Category>();
            foreach (var seed in Categories)
            {
                var names = seed.Legacy.Append(seed.Uk).Append(seed.En).ToList();
                var category = await db.Categories.FirstOrDefaultAsync(c => names.Contains(c.Name) || (c.NameEn != null && names.Contains(c.NameEn)));
                if (category == null) db.Categories.Add(category = new Category());
                (category.Name, category.NameEn) = (seed.Uk, seed.En);
                categories[seed.Uk] = category;
            }

            var recipients = new Dictionary<string, Recipient>();
            foreach (var seed in Recipients)
            {
                var names = seed.Legacy.Append(seed.Uk).Append(seed.En).ToList();
                var recipient = await db.Recipients.FirstOrDefaultAsync(r => names.Contains(r.Name) || (r.NameEn != null && names.Contains(r.NameEn)));
                if (recipient == null) db.Recipients.Add(recipient = new Recipient { ContactInfo = seed.Contact });
                (recipient.Name, recipient.NameEn) = (seed.Uk, seed.En);
                recipient.ContactInfo ??= seed.Contact;
                recipients[seed.Uk] = recipient;
            }

            var acabs = new Dictionary<string, Acab>();
            foreach (var seed in Acabs)
            {
                var acab = await db.Acabs.FirstOrDefaultAsync(a => a.Name == seed.Name);
                if (acab == null)
                {
                    db.Acabs.Add(acab = new Acab
                    {
                        Name = seed.Name,
                        Address = seed.Address,
                        ResidentCode = RandomCodes.InvitationCode(),
                        HeadCode = RandomCodes.InvitationCode(),
                        CreatedAt = now.AddDays(-60),
                        UpdatedAt = now.AddDays(-60)
                    });
                }
                (acab.NameEn, acab.AddressEn) = (seed.NameEn, seed.AddressEn);
                acabs[seed.Name] = acab;
            }
            await db.SaveChangesAsync();

            // ── Accounts ──
            var users = new Dictionary<string, User>();
            var createdUsers = 0;
            foreach (var seed in Users)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == seed.Email);
                if (user == null)
                {
                    db.Users.Add(user = new User
                    {
                        Email = seed.Email,
                        PasswordHash = hasher.Hash(DemoPassword),
                        EmailConfirmed = true,
                        FirstName = seed.FirstName,
                        LastName = seed.LastName,
                        Phone = seed.Phone,
                        Role = seed.Role,
                        AcabId = seed.Acab == null ? null : acabs[seed.Acab].Id,
                        Language = seed.Language,
                        CreatedAt = now.AddDays(-50),
                        UpdatedAt = now.AddDays(-50)
                    });
                    createdUsers++;
                }
                users[seed.Email] = user;
            }
            await db.SaveChangesAsync();

            // ── Appeals with their history ──
            var createdAppeals = 0;
            foreach (var seed in Appeals)
            {
                var author = users[seed.Author];
                if (await db.Appeals.AnyAsync(a => a.AuthorId == author.Id && a.Title == seed.Title)) continue;

                var created = now.AddDays(-seed.DaysAgo);
                var english = TextLanguage.Detect(seed.Title) == "en";
                var appeal = new Appeal
                {
                    Title = seed.Title,
                    Description = seed.Description,
                    Language = english ? "en" : "uk",
                    TitleTranslated = seed.TitleTranslated,
                    DescriptionTranslated = seed.DescriptionTranslated,
                    Lat = (decimal)seed.Lat,
                    Lng = (decimal)seed.Lng,
                    Address = seed.Address,
                    Priority = seed.Priority,
                    AuthorId = author.Id,
                    AcabId = author.AcabId,
                    CategoryId = categories[seed.Category].Id,
                    ResponsibleKind = seed.Recipient == null ? ResponsibleKind.Association : ResponsibleKind.Recipient,
                    RecipientId = seed.Recipient == null ? null : recipients[seed.Recipient].Id,
                    CreatedAt = created,
                    UpdatedAt = created
                };
                db.Appeals.Add(appeal);
                await db.SaveChangesAsync();

                // The head of the author's association processes it; without an association, the administrator.
                var staff = author.AcabId == null
                    ? admin
                    : users.Values.FirstOrDefault(u => u.Role == UserRole.AcabAdmin && u.AcabId == author.AcabId) ?? admin;

                var heads = users.Values.Where(u => u.Role == UserRole.AcabAdmin && u.AcabId != null && u.AcabId == author.AcabId).ToList();
                foreach (var head in heads)
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = head.Id, AppealId = appeal.Id, Type = NotificationType.NewAppeal,
                        IsRead = seed.Steps.Length > 0, CreatedAt = created
                    });
                }

                var status = AppealStatus.New;
                for (var i = 0; i < seed.Steps.Length; i++)
                {
                    var step = seed.Steps[i];
                    if (!AppealWorkflow.CanMove(status, step.Status))
                    {
                        throw new InvalidOperationException($"Seed appeal \"{seed.Title}\": {status} cannot move to {step.Status}.");
                    }
                    var at = created.AddHours(step.Hours);
                    db.AppealUpdates.Add(new AppealUpdate
                    {
                        AppealId = appeal.Id, OldStatus = status, NewStatus = step.Status,
                        ChangedById = staff.Id, Comment = step.Comment, ChangedAt = at
                    });
                    db.Notifications.Add(new Notification
                    {
                        UserId = author.Id, AppealId = appeal.Id, Type = NotificationType.StatusChanged,
                        NewStatus = step.Status, Message = step.Comment ?? "",
                        // Only the latest change of a recent appeal is still unread.
                        IsRead = i < seed.Steps.Length - 1 || seed.DaysAgo > 7,
                        CreatedAt = at
                    });
                    status = step.Status;
                    appeal.UpdatedAt = at;
                }
                appeal.Status = status;
                await db.SaveChangesAsync();
                createdAppeals++;
            }

            logger.LogInformation(
                "Demo data: {Users} accounts and {Appeals} appeals added (password {Password}). Association codes: {Codes}",
                createdUsers, createdAppeals, DemoPassword,
                string.Join("; ", acabs.Values.Select(a => $"{a.Name}: residents {a.ResidentCode}, head {a.HeadCode}")));
        }
    }
}
