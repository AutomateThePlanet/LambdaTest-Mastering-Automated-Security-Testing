using OpenQA.Selenium.Chrome;
using WebDriverManager.DriverConfigs.Impl;

namespace SecurityAutomatedTests;

[TestFixture]
public class JuiceShopZapTests
{
    private IWebDriver _driver;
    private WebDriverWait _wait;
    private const string URL = "http://juice-shop.herokuapp.com/";

    [SetUp]
    public void TestInit()
    {
        new WebDriverManager.DriverManager().SetUpDriver(new ChromeConfig());
        ChromeOptions options = new ChromeOptions();
        options.AddArgument("--proxy-server=http://localhost:8088"); // ZAP running on port 8088
        //options.AddArgument("--ignore-certificate-errors");
        _driver = new ChromeDriver(options);
        _driver.Manage().Window.Maximize();

        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

        _driver.Navigate().GoToUrl(URL);

        _driver.Manage().Cookies.AddCookie(new Cookie("welcomebanner_status", "dismiss"));

        _driver.Navigate().Refresh();
    }

    [TearDown]
    public void TestCleanup()
    {
        _driver?.Quit();
        _driver?.Dispose();
    }

    [Test]
    public void CrawJuiceShopForSecurityProblems()
    {
        ClickElement(By.XPath("//mat-icon[normalize-space(text())='search']"));
        var searchElement = WaitForElement(By.XPath("//*[@id='mat-input-0']"));
        new Actions(_driver).MoveToElement(searchElement).SendKeys("Apple Juice").SendKeys(Keys.Enter).Perform();

        ClickElement(By.XPath("//img[@alt='Apple Juice (1000ml)']"));

        //Console.WriteLine("Starting spider scan...");
        //ZAPService.StartSpiderScan(URL);

        //Console.WriteLine("Starting active scan...");
        //ZAPService.StartActiveScan(URL);

        //Console.WriteLine("Retrieving alerts...");
        //ZAPService.GetAlerts(URL);

        ZAPService.ScanCurrentPage(URL);

        // Generate HTML report
        ZAPService.GenerateHtmlReport("ZAP_Scan_Report1.html");

        // Perform assertions
        ZAPService.AssertAlertsArePresent();
        ZAPService.AssertNoHighRiskAlerts();
        ZAPService.AssertAllAlertsHaveSolutions();
        ZAPService.AssertAlertsBelowRiskLevel("Medium");

       
    }

    private void ClickElement(By locator)
    {
        var element = WaitForElement(locator);
        element.Click();
    }

    private IWebElement WaitForElement(By locator)
    {
        return _wait.Until(ExpectedConditions.ElementIsVisible(locator));
    }
}
