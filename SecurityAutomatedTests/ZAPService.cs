using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RestSharp;

namespace SecurityAutomatedTests;

public class ZAPService
{
    private static readonly RestClient client;
    private static readonly string ApiKey;

    static ZAPService()
    {
        client = new RestClient("http://127.0.0.1:8088");
        ApiKey = Environment.GetEnvironmentVariable("zap_api_key");
    }

    public static List<ZapAlert> Alerts { get; private set; }

    public static void ScanCurrentPage(string pageUrl)
    {
        var request = new RestRequest("/JSON/ascan/action/scan/", Method.Get);
        request.AddParameter("apikey", ApiKey);
        request.AddParameter("url", pageUrl);
        request.AddParameter("recurse", "false"); // Ensures the scan does not recurse into other links on the page

        var response = client.ExecuteAsync(request).Result;

        if (response.IsSuccessful)
        {
            Console.WriteLine($"Active scan started successfully for the page: {pageUrl}");
        }
        else
        {
            Console.WriteLine($"Failed to start active scan for the page: {pageUrl}");
            Console.WriteLine($"Error: {response.Content}");
        }

        GetAlerts(pageUrl);
    }


    public static void StartSpiderScan(string targetUrl)
    {
        var request = new RestRequest("/JSON/spider/action/scan/", Method.Get);
        request.AddParameter("apikey", ApiKey);
        request.AddParameter("url", targetUrl);
        var response = client.ExecuteAsync(request).Result;

        if (response.IsSuccessful)
        {
            Console.WriteLine("Spider scan started successfully.");
        }
        else
        {
            Console.WriteLine("Failed to start spider scan.");
        }
    }

    public static void StartActiveScan(string targetUrl)
    {
        var request = new RestRequest("/JSON/ascan/action/scan/", Method.Get);
        request.AddParameter("apikey", ApiKey);
        request.AddParameter("url", targetUrl);
        var response = client.ExecuteAsync(request).Result;

        if (response.IsSuccessful)
        {
            Console.WriteLine("Active scan started successfully.");
        }
        else
        {
            Console.WriteLine("Failed to start active scan.");
        }
    }

    public static List<ZapAlert> GetAlerts(string targetUrl)
    {
        var request = new RestRequest("/JSON/core/view/alerts/", Method.Get);
        request.AddParameter("apikey", ApiKey);
        request.AddParameter("url", targetUrl);
        var response = client.ExecuteAsync<ZapAlertsResponse>(request).Result;

        if (response.IsSuccessful)
        {
            Console.WriteLine("Alerts retrieved successfully:");
        }
        else
        {
            Console.WriteLine("Failed to retrieve alerts.");
        }

        Alerts = response.Data.Alerts;

        return Alerts;
    }

    public static void GenerateHtmlReport(string outputPath)
    {
        var html = new StringBuilder();

        // Include Bootstrap CSS
        html.Append("<html><head>");
        html.Append("<meta charset='UTF-8'>");
        html.Append("<meta name='viewport' content='width=device-width, initial-scale=1, shrink-to-fit=no'>");
        html.Append("<link rel='stylesheet' href='https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0/css/bootstrap.min.css'>");
        html.Append("<title>ZAP Scan Report</title></head><body>");

        html.Append("<div class='container'>");
        html.Append("<h1 class='my-4'>ZAP Scan Report</h1>");

        // Summary Section
        html.Append("<h2>Summary</h2>");
        var groupedByRisk = Alerts.GroupBy(a => a.Risk).Select(g => new { Risk = g.Key, Count = g.Count() }).ToList();
        html.Append("<table class='table table-bordered'>");
        html.Append("<thead class='thead-dark'><tr><th>Risk Level</th><th>Count</th></tr></thead><tbody>");
        foreach (var item in groupedByRisk)
        {
            html.Append($"<tr><td>{item.Risk}</td><td>{item.Count}</td></tr>");
        }
        html.Append("</tbody></table>");

        // Detailed Alerts Table
        html.Append("<h2>Detailed Findings</h2>");
        html.Append("<table class='table table-striped table-bordered'>");
        html.Append("<thead class='thead-dark'><tr>");
        html.Append("<th>Alert</th><th>Risk</th><th>URL</th><th>Description</th><th>Solution</th>");
        html.Append("</tr></thead><tbody>");

        // Add each alert as a table row
        foreach (var alert in Alerts)
        {
            html.Append("<tr>");
            html.Append($"<td>{alert.Alert}</td>");
            html.Append($"<td>{alert.Risk}</td>");
            html.Append($"<td>{alert.Url}</td>");
            html.Append($"<td>{alert.Description}</td>");
            html.Append($"<td>{alert.Solution}</td>");
            html.Append("</tr>");
        }

        // Close HTML document
        html.Append("</tbody></table>");
        html.Append("</div>"); // Close container
        html.Append("</body></html>");

        // Write to output file
        File.WriteAllText(outputPath, html.ToString());
    }

    // Assertion Methods

    /// <summary>
    /// Asserts that alerts are present in the scan results.
    /// </summary>
    public static void AssertAlertsArePresent()
    {
        if (Alerts == null || !Alerts.Any())
        {
            throw new Exception("Assertion failed: No alerts were found in the scan results.");
        }
        Console.WriteLine("Assertion passed: Alerts are present.");
    }

    /// <summary>
    /// Asserts that there are no high-risk alerts in the scan results.
    /// </summary>
    public static void AssertNoHighRiskAlerts()
    {
        var highRiskAlerts = Alerts.Where(a => a.Risk.Equals("High", StringComparison.OrdinalIgnoreCase)).ToList();
        if (highRiskAlerts.Any())
        {
            var alertNames = string.Join(", ", highRiskAlerts.Select(a => a.Alert));
            throw new Exception($"Assertion failed: High-risk alerts found - {alertNames}");
        }
        Console.WriteLine("Assertion passed: No high-risk alerts found.");
    }

    /// <summary>
    /// Asserts that a specific alert is present.
    /// </summary>
    public static void AssertAlertIsPresent(string alertName)
    {
        if (!Alerts.Any(a => a.Alert.Equals(alertName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception($"Assertion failed: Alert '{alertName}' was not found in the scan results.");
        }
        Console.WriteLine($"Assertion passed: Alert '{alertName}' is present.");
    }

    /// <summary>
    /// Asserts that all alerts are below a specified risk level.
    /// </summary>
    public static void AssertAlertsBelowRiskLevel(string maxRiskLevel)
    {
        var riskLevels = new List<string> { "Informational", "Low", "Medium", "High" };
        var maxRiskIndex = riskLevels.IndexOf(maxRiskLevel);

        if (maxRiskIndex == -1)
        {
            throw new ArgumentException($"Invalid risk level: {maxRiskLevel}");
        }

        var invalidAlerts = Alerts.Where(a => riskLevels.IndexOf(a.Risk) > maxRiskIndex).ToList();

        if (invalidAlerts.Any())
        {
            var alertNames = string.Join(", ", invalidAlerts.Select(a => a.Alert));
            throw new Exception($"Assertion failed: Alerts with risk level above '{maxRiskLevel}' found - {alertNames}");
        }
        Console.WriteLine($"Assertion passed: All alerts are below the risk level '{maxRiskLevel}'.");
    }

    /// <summary>
    /// Asserts that all alerts have solutions provided.
    /// </summary>
    public static void AssertAllAlertsHaveSolutions()
    {
        var alertsWithoutSolutions = Alerts.Where(a => string.IsNullOrWhiteSpace(a.Solution)).ToList();
        if (alertsWithoutSolutions.Any())
        {
            throw new Exception($"Assertion failed: Some alerts do not have solutions. Alerts without solutions: {alertsWithoutSolutions.Count}.");
        }
        Console.WriteLine("Assertion passed: All alerts have solutions.");
    }
}
