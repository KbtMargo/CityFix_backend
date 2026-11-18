using CityFix_backend.Models;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;

namespace CityFix_backend.Tests.E2E;

/// <summary>The site on a real Kestrel port, driven by headless Microsoft Edge.</summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    public CityFixApp App { get; } = new() { RealServer = true };
    public IWebDriver Driver { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await App.InitializeAsync();
        var options = new EdgeOptions();
        options.AddArguments("--headless=new", "--window-size=1400,1000", "--lang=uk", "--disable-gpu", "--no-first-run");
        options.AddUserProfilePreference("intl.accept_languages", "uk");
        Driver = new EdgeDriver(options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
    }

    public async Task DisposeAsync()
    {
        Driver?.Quit();
        await App.DisposeAsync();
    }
}

[CollectionDefinition(nameof(BrowserCollection))]
public class BrowserCollection : ICollectionFixture<BrowserFixture>;

[Trait("Category", "E2E")]
[Collection(nameof(BrowserCollection))]
public class BrowserTests(BrowserFixture fixture) : IDisposable
{
    private IWebDriver Browser => fixture.Driver;
    private CityFixApp App => fixture.App;

    public void Dispose() => Browser.Manage().Cookies.DeleteAllCookies();

    private void Open(string path) => Browser.Navigate().GoToUrl(App.BaseUrl + path);

    // Pages reload after form posts, so elements found a moment ago may be gone; keep polling instead of failing.
    private WebDriverWait Wait(int seconds)
    {
        var wait = new WebDriverWait(Browser, TimeSpan.FromSeconds(seconds));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));
        return wait;
    }

    private IWebElement WaitFor(By by, int seconds = 10) =>
        Wait(seconds).Until(d =>
        {
            var element = d.FindElements(by).FirstOrDefault();
            return element is { Displayed: true } ? element : null;
        })!;

    private void WaitUntil(Func<IWebDriver, bool> condition, int seconds = 10) => Wait(seconds).Until(condition);

    private string Path => new Uri(Browser.Url).PathAndQuery;

    private void SignIn(string email, string password = TestData.Password)
    {
        Open("/Account/Login");
        WaitFor(By.Id("Email")).SendKeys(email);
        Browser.FindElement(By.Id("Password")).SendKeys(password);
        Browser.FindElement(By.CssSelector("form[action='/Account/Login'] button[type=submit]")).Click();
        WaitUntil(_ => Path.StartsWith("/Dashboard") || Path.StartsWith("/Account/Profile"));
    }

    private void Select(string id, string value)
    {
        Browser.FindElement(By.Id(id)).Click();
        Browser.FindElement(By.CssSelector($"#{id} option[value='{value}']")).Click();
    }

    [Fact]
    public void Landing_page_leads_to_the_login_form()
    {
        Open("/");
        Assert.Contains("CityFix", Browser.Title);
        Browser.FindElement(By.CssSelector("a[href='/Account/Login']")).Click();
        WaitFor(By.Id("Email"));
        Assert.Equal("/Account/Login", Path);
    }

    [Fact]
    public async Task Visitor_registers_in_the_browser_and_signs_in_after_confirming()
    {
        var email = TestData.UniqueEmail("browser");
        Open("/Account/Register");
        WaitFor(By.Id("FirstName")).SendKeys("Оксана");
        Browser.FindElement(By.Id("LastName")).SendKeys("Бойко");
        Browser.FindElement(By.Id("Email")).SendKeys(email);
        Browser.FindElement(By.Id("Password")).SendKeys("Browser-Passw0rd");
        Browser.FindElement(By.Id("ConfirmPassword")).SendKeys("Browser-Passw0rd");
        Browser.FindElement(By.CssSelector("form[action='/Account/Register'] button[type=submit]")).Click();

        WaitUntil(_ => Path.StartsWith("/Account/CheckEmail"));
        Assert.Contains(email, Browser.PageSource);

        var link = (await App.Email.WaitForAsync(email)).Link;
        Browser.Navigate().GoToUrl(App.BaseUrl + new Uri(link).PathAndQuery);
        WaitUntil(_ => Path.StartsWith("/Account/Login"));
        Assert.Contains(Uk.Text("Auth.EmailConfirmed"), Browser.FindElement(By.CssSelector("[data-flash]")).Text);

        SignIn(email, "Browser-Passw0rd");
        Assert.StartsWith("/Dashboard", Path);
        Assert.Contains("Оксана", Browser.PageSource);
    }

    [Fact]
    public void Empty_or_wrong_login_shows_an_error_and_does_not_sign_in()
    {
        Open("/Account/Login");
        WaitFor(By.Id("Email"));
        Browser.FindElement(By.CssSelector("form[action='/Account/Login'] button[type=submit]")).Click();
        Assert.False(string.IsNullOrWhiteSpace(WaitFor(By.CssSelector("[role=alert]")).Text));

        Browser.FindElement(By.Id("Email")).SendKeys("nobody@test.local");
        Browser.FindElement(By.Id("Password")).SendKeys("wrong");
        Browser.FindElement(By.CssSelector("form[action='/Account/Login'] button[type=submit]")).Click();
        WaitUntil(d => d.FindElement(By.CssSelector("[role=alert]")).Text.Contains(Uk.Text("Auth.LoginFailed")));
        Assert.Null(Browser.Manage().Cookies.GetCookieNamed(".AspNetCore.Cookies"));
    }

    [Fact]
    public void Password_eye_button_reveals_the_password()
    {
        Open("/Account/Login");
        var password = WaitFor(By.Id("Password"));
        password.SendKeys("visible?");
        var toggle = WaitFor(By.CssSelector("[data-password-toggle]"));

        toggle.Click();
        Assert.Equal("text", password.GetAttribute("type"));
        toggle.Click();
        Assert.Equal("password", password.GetAttribute("type"));
    }

    [Fact]
    public async Task Resident_picks_a_point_on_the_map_and_files_an_appeal()
    {
        var n = (await App.DataAsync(d => d.NeighbourhoodAsync()));
        SignIn(n.Resident.Email);

        Open("/Appeals/Create");
        WaitFor(By.Id("Title")).SendKeys("Розбитий ліхтар біля зупинки");
        Browser.FindElement(By.Id("Description")).SendKeys("Не світить уже тиждень");
        Select("CategoryId", n.Category.Id.ToString());
        var map = WaitFor(By.CssSelector("#map.leaflet-container"), 20);
        new OpenQA.Selenium.Interactions.Actions(Browser).MoveToElement(map, 40, 30).Click().Perform();
        WaitUntil(_ => !string.IsNullOrEmpty(Browser.FindElement(By.Id("Lat")).GetAttribute("value")));
        WaitUntil(_ => Browser.FindElement(By.Id("Address")).GetAttribute("value") == App.Geocoding.Address);
        Select("Responsible", "acab");
        ((IJavaScriptExecutor)Browser).ExecuteScript("arguments[0].form.requestSubmit()", Browser.FindElement(By.Id("Title")));

        WaitUntil(_ => Path.Contains("appealId="), 15);
        Assert.Contains("Розбитий ліхтар біля зупинки", Browser.PageSource);
        var stored = (await App.DbAsync((db, _) => db.Appeals.AsNoTracking().SingleAsync(a => a.AuthorId == n.Resident.Id)));
        Assert.Equal((App.Geocoding.Address, AppealStatus.New), (stored.Address, stored.Status));
        Assert.InRange((double)stored.Lat!, 49.3, 49.6);
    }

    [Fact]
    public async Task Head_changes_the_status_in_the_appeal_card()
    {
        var n = (await App.DataAsync(d => d.NeighbourhoodAsync()));
        var appeal = (await App.DataAsync(d => d.AppealAsync(n.Resident, n.Category, title: "Протікає дах")));
        SignIn(n.Head.Email);

        Open($"/Appeals?appealId={appeal.Id}");
        WaitFor(By.Id("update-status"));
        Select("update-status", "InProgress");
        Browser.FindElement(By.Id("update-comment")).SendKeys("Покрівельники вже в дорозі");
        Browser.FindElement(By.CssSelector("form[action='/Appeals/Process'] button[type=submit]")).Click();

        WaitUntil(_ => Browser.FindElements(By.CssSelector("[data-flash]")).Count > 0);
        Assert.Contains(Uk.Text("Appeal.StatusSaved"), Browser.PageSource);
        Assert.Equal(AppealStatus.InProgress, (await App.DbAsync((db, _) => db.Appeals.Where(a => a.Id == appeal.Id).Select(a => a.Status).SingleAsync())));
        Assert.Contains("Покрівельники вже в дорозі", Browser.PageSource);
    }

    [Fact]
    public async Task Resident_does_not_see_staff_menu_or_processing_controls()
    {
        var n = (await App.DataAsync(d => d.NeighbourhoodAsync()));
        var appeal = (await App.DataAsync(d => d.AppealAsync(n.Neighbour, n.Category)));
        SignIn(n.Resident.Email);

        Open($"/Appeals?appealId={appeal.Id}");
        WaitFor(By.CssSelector("body"));
        Assert.Empty(Browser.FindElements(By.CssSelector("a[href='/Users']")));
        Assert.Empty(Browser.FindElements(By.Id("update-status")));
    }

    [Fact]
    public void Language_switch_translates_the_interface()
    {
        Open("/Account/Login");
        WaitFor(By.CssSelector("button[name=culture][value=en]")).Click();
        WaitUntil(d => d.FindElement(By.TagName("html")).GetAttribute("lang") == "en");
        Assert.Equal("true", Browser.FindElement(By.CssSelector("button[name=culture][value=en]")).GetAttribute("aria-pressed"));

        Browser.FindElement(By.CssSelector("button[name=culture][value=uk]")).Click();
        WaitUntil(d => d.FindElement(By.TagName("html")).GetAttribute("lang") == "uk");
    }

    [Fact]
    public async Task Pages_load_without_javascript_errors_or_blocked_resources()
    {
        var n = (await App.DataAsync(d => d.NeighbourhoodAsync()));
        SignIn(n.Head.Email);
        foreach (var page in new[] { "/Dashboard", "/Appeals", "/Analytics", "/Account/Profile" })
        {
            Open(page);
            WaitUntil(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")!.Equals("complete"));
            var errors = ((IJavaScriptExecutor)Browser).ExecuteScript(
                "return performance.getEntriesByType('resource').filter(r => r.responseStatus >= 400).map(r => r.name)") as IReadOnlyCollection<object>;
            Assert.True(errors == null || errors.Count == 0, $"{page}: failed resources {string.Join(", ", errors ?? [])}");
        }
        var logs = Browser.Manage().Logs.GetLog(LogType.Browser).Where(l => l.Level == LogLevel.Severe && !l.Message.Contains("tile.openstreetmap")).ToList();
        Assert.Empty(logs.Select(l => l.Message));
    }
}
