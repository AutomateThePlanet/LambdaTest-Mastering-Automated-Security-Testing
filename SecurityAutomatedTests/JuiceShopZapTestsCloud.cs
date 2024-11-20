using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using System;
using System.Collections.Generic;

namespace SecurityAutomatedTests;

[TestFixture]
public class ProductPurchaseTestsCloud
{
    private RemoteWebDriver _driver;
    private WebDriverWait _wait;
    private const string URL = "http://juice-shop.herokuapp.com/";
    private bool _isTestPassed = true;
    private Exception _testException = null;

    [SetUp]
    public void TestInit()
    {
        string userName = Environment.GetEnvironmentVariable("LT_USERNAME", EnvironmentVariableTarget.Machine);
        string accessKey = Environment.GetEnvironmentVariable("LT_ACCESSKEY", EnvironmentVariableTarget.Machine);

        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(accessKey))
        {
            throw new Exception("LambdaTest credentials are not set in environment variables.");
        }

        ChromeOptions options = new ChromeOptions();

        options.AddAdditionalOption("user", userName);
        options.AddAdditionalOption("accessKey", accessKey);
        options.AddArgument("--ignore-certificate-errors");

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

        var hubUrl = new Uri($"https://{userName}:{accessKey}@hub.lambdatest.com/wd/hub");
        _driver = new RemoteWebDriver(hubUrl, options);
        _driver.Manage().Window.Maximize();

        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

        _driver.Navigate().GoToUrl(URL);
        _driver.Manage().Cookies.AddCookie(new Cookie("welcomebanner_status", "dismiss"));
        _driver.Navigate().Refresh();
    }

    [Test]
    public void CrawJuiceShopForSecurityProblems()
    {
        try
        {
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
        }
        catch (Exception ex)
        {
            _isTestPassed = false; // Mark the test as failed
            _testException = ex; // Store the exception for later reporting
            throw; // Rethrow the exception to let the test fail naturally
        }
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

    [TearDown]
    public void TestCleanup()
    {
        try
        {
            if (_driver != null)
            {
                // Update test status on LambdaTest
                if (_isTestPassed)
                {
                    _driver.ExecuteScript("lambda-status=passed");
                    Console.WriteLine("Test passed successfully.");
                }
                else
                {
                    var exceptionCapture = new List<string>
                    {
                        _testException?.ToString() ?? "Unknown error"
                    };
                    _driver.ExecuteScript("lambda-exceptions", exceptionCapture);
                    _driver.ExecuteScript("lambda-status=failed");
                    Console.WriteLine($"Test failed: {_testException?.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during LambdaTest status update: {ex.Message}");
        }
        finally
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
    }
}
