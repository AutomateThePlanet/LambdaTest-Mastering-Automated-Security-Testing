using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using RestSharp;

namespace SecurityAutomatedTests;

[TestFixture]
public class ProductPurchaseTestsCloud
{
    private RemoteWebDriver _driver;
    private WebDriverWait _wait;
    private const string URL = "http://juice-shop.herokuapp.com";

    [SetUp]
    public void TestInit()
    {
        // Retrieve credentials from environment variables
        string userName = Environment.GetEnvironmentVariable("LT_USERNAME", EnvironmentVariableTarget.Machine);
        string accessKey = Environment.GetEnvironmentVariable("LT_ACCESSKEY", EnvironmentVariableTarget.Machine);

        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(accessKey))
        {
            throw new Exception("LambdaTest credentials are not set in environment variables.");
        }

        // Configure LambdaTest options
        ChromeOptions options = new ChromeOptions();
        //options.AddArgument("--proxy-server=http://localhost:8088"); // ZAP running on port 8088

        options.AddAdditionalOption("user", userName);
        options.AddAdditionalOption("accessKey", accessKey);

        // Enable LambdaTest Tunnel
        var capabilities = new Dictionary<string, object>
        {
            { "resolution", "1920x1080" },
            { "platform", "Windows 10" },
            { "visual", "false" },
            { "video", "true" },
            { "seCdp", "true" },
            { "console", "true" },
            { "w3c", "true" },
            { "plugin", "c#-c#" },
            { "tunnel", "true" }, // Enable LambdaTest Tunnel
            { "tunnelName", "ZAP-Tunnel" }, // Optional: Specify tunnel name
            { "build", $"PO_{DateTime.Now:yyyyMMddHHmmss}" },
            { "project", "Security_RUN" },
            { "selenium_version", "4.22.0" }
        };
        options.AddAdditionalOption("LT:Options", capabilities);

        // Initialize RemoteWebDriver with LambdaTest hub URL
        var hubUrl = new Uri($"https://{userName}:{accessKey}@hub.lambdatest.com/wd/hub");
        _driver = new RemoteWebDriver(hubUrl, options);
        _driver.Manage().Window.Maximize();

        // Initialize WebDriverWait
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

        // Navigate to the application
        _driver.Navigate().GoToUrl(URL);
        _driver.Manage().Cookies.AddCookie(new Cookie("welcomebanner_status", "dismiss"));
        _driver.Navigate().Refresh();
    }

    [Test]
    public void CrawJuiceShopForSecurityProblems()
    {
        try
        {
            // Interact with the page
            ClickElement(By.XPath("//mat-icon[normalize-space(text())='search']"));
            var searchElement = WaitForElement(By.XPath("//*[@id='mat-input-0']"));
            new Actions(_driver).MoveToElement(searchElement).SendKeys("Apple Juice").SendKeys(Keys.Enter).Perform();
            ClickElement(By.XPath("//img[@alt='Apple Juice (1000ml)']"));

            // Start ZAP scan and assertions
            ZAPService.ScanCurrentPage(URL);
            ZAPService.GenerateHtmlReport("ZAP_Scan_Report1.html");

            ZAPService.AssertAlertsArePresent();
            ZAPService.AssertNoHighRiskAlerts();
            ZAPService.AssertAllAlertsHaveSolutions();
            ZAPService.AssertAlertsBelowRiskLevel("Medium");

            // Mark test as passed
            UpdateTestStatusOnLambdaTest(true, "Test passed successfully.");
        }
        catch (Exception ex)
        {
            // Mark test as failed with error message
            UpdateTestStatusOnLambdaTest(false, $"Test failed: {ex.Message}");
            throw;
        }
    }

    private void ClickElement(By locator)
    {
        var element = WaitForElement(locator);
        element.Click();
    }

    private IWebElement WaitForElement(By locator)
    {
        return _wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(locator));
    }

    [TearDown]
    public void TestCleanup()
    {
        try
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during driver cleanup: {ex.Message}");
        }
    }

    private void UpdateTestStatusOnLambdaTest(bool isPassed, string message)
    {
        try
        {
            var sessionId = _driver.SessionId.ToString();
            var client = new RestClient($"https://{Environment.GetEnvironmentVariable("LT_USERNAME")}:{Environment.GetEnvironmentVariable("LT_ACCESSKEY")}@api.lambdatest.com/automation/api/v1/sessions/{sessionId}");
            var request = new RestRequest();
            request.AddJsonBody(new
            {
                status_ind = isPassed ? "passed" : "failed",
                remark = message
            });

            var response = client.Patch(request);
            if (response.IsSuccessful)
            {
                Console.WriteLine($"Test status updated on LambdaTest: {message}");
            }
            else
            {
                Console.WriteLine($"Failed to update test status on LambdaTest: {response.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating test status on LambdaTest: {ex.Message}");
        }
    }
}
