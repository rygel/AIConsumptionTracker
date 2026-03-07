using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;

namespace AIUsageTracker.Web.Tests;

[TestClass]
public class ViewTests : WebTestBase
{
    [TestMethod]
    [DataRow("/")]
    [DataRow("/providers")]
    [DataRow("/charts")]
    [DataRow("/history")]
    [DataRow("/reliability")]
    public async Task Page_LoadsSuccessfully(string path)
    {
        var response = await Page.GotoAsync($"{ServerUrl}{path}");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);
    }

    [TestMethod]
    public async Task Dashboard_HasExpectedElements()
    {
        await Page.GotoAsync(ServerUrl);
        
        // Check for common layout elements
        var sidebar = await Page.QuerySelectorAsync(".sidebar");
        Assert.IsNotNull(sidebar, "Sidebar should be present");

        var mainContent = await Page.QuerySelectorAsync("main");
        Assert.IsNotNull(mainContent, "Main content area should be present");
    }

    [TestMethod]
    public async Task Dashboard_ModelBinding_WithShowUsedParameter()
    {
        var response = await Page.GotoAsync($"{ServerUrl}?showUsed=true");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);

        var uri = new Uri(ServerUrl);
        var cookies = await Page.Context.CookiesAsync(new[] { uri.AbsoluteUri });
        var showUsedCookie = cookies.FirstOrDefault(c => c.Name == "showUsedPercentage");
        Assert.IsNotNull(showUsedCookie, "showUsedPercentage cookie should be set");
    }

    [TestMethod]
    public async Task Dashboard_ModelBinding_WithShowInactiveParameter()
    {
        var response = await Page.GotoAsync($"{ServerUrl}?showInactive=true");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);

        var uri = new Uri(ServerUrl);
        var cookies = await Page.Context.CookiesAsync(new[] { uri.AbsoluteUri });
        var showInactiveCookie = cookies.FirstOrDefault(c => c.Name == "showInactiveProviders");
        Assert.IsNotNull(showInactiveCookie, "showInactiveProviders cookie should be set");
    }

    [TestMethod]
    public async Task ProvidersPage_LoadsSuccessfully()
    {
        var response = await Page.GotoAsync($"{ServerUrl}/providers");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);
    }

    [TestMethod]
    public async Task ProvidersPage_HasTableStructure()
    {
        await Page.GotoAsync($"{ServerUrl}/providers");
        
        var table = await Page.QuerySelectorAsync("table");
        Assert.IsNotNull(table, "Providers table should be present");

        var tableHeaders = await table.QuerySelectorAllAsync("th");
        Assert.IsTrue(tableHeaders.Count > 0, "Table should have headers");
    }

    [TestMethod]
    public async Task ChartsPage_LoadsSuccessfully()
    {
        var response = await Page.GotoAsync($"{ServerUrl}/charts");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);
    }

    [TestMethod]
    public async Task ChartsPage_HasChartElements()
    {
        await Page.GotoAsync($"{ServerUrl}/charts");
        
        var canvas = await Page.QuerySelectorAsync("canvas");
        Assert.IsNotNull(canvas, "Chart canvas should be present");
    }

    [TestMethod]
    public async Task HistoryPage_LoadsSuccessfully()
    {
        var response = await Page.GotoAsync($"{ServerUrl}/history");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);
    }

    [TestMethod]
    public async Task HistoryPage_HasTableStructure()
    {
        await Page.GotoAsync($"{ServerUrl}/history");
        
        var table = await Page.QuerySelectorAsync("table");
        Assert.IsNotNull(table, "History table should be present");

        var tableHeaders = await table.QuerySelectorAllAsync("th");
        Assert.IsTrue(tableHeaders.Count > 0, "Table should have headers");
    }

    [TestMethod]
    public async Task ProviderPage_LoadsSuccessfully()
    {
        var response = await Page.GotoAsync($"{ServerUrl}/provider?providerId=openai");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);
    }

    [TestMethod]
    public async Task ProviderPage_HasProviderDetails()
    {
        await Page.GotoAsync($"{ServerUrl}/provider?providerId=openai");
        
        var providerCard = await Page.QuerySelectorAsync(".provider-card");
        Assert.IsNotNull(providerCard, "Provider card should be present");
    }

    [TestMethod]
    public async Task ReliabilityPage_LoadsSuccessfully()
    {
        var response = await Page.GotoAsync($"{ServerUrl}/reliability");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);
    }

    [TestMethod]
    public async Task ReliabilityPage_HasReliabilityElements()
    {
        await Page.GotoAsync($"{ServerUrl}/reliability");
        
        var reliabilityTable = await Page.QuerySelectorAsync("table");
        Assert.IsNotNull(reliabilityTable, "Reliability table should be present");
    }

    [TestMethod]
    public async Task ErrorPage_LoadsSuccessfully()
    {
        var response = await Page.GotoAsync($"{ServerUrl}/error");
        Assert.IsNotNull(response);
        Assert.AreEqual(200, response.Status);
    }

    [TestMethod]
    public async Task ErrorPage_HasErrorMessage()
    {
        await Page.GotoAsync($"{ServerUrl}/error?message=TestError");
        
        var errorMessage = await Page.QuerySelectorAsync(".error-message");
        Assert.IsNotNull(errorMessage, "Error message should be present");
    }

    [TestMethod]
    public async Task Layout_HasConsistentNavigation()
    {
        await Page.GotoAsync(ServerUrl);

        var navLinks = await Page.QuerySelectorAllAsync("nav a[href]");
        Assert.IsTrue(navLinks.Count > 0, "Navigation should have links");

        bool hasProvidersLink = false;
        bool hasChartsLink = false;
        bool hasHistoryLink = false;

        foreach (var link in navLinks)
        {
            var href = await link.GetAttributeAsync("href");
            if (href?.Contains("/providers") == true)
                hasProvidersLink = true;
            if (href?.Contains("/charts") == true)
                hasChartsLink = true;
            if (href?.Contains("/history") == true)
                hasHistoryLink = true;
        }

        Assert.IsTrue(hasProvidersLink || hasChartsLink || hasHistoryLink,
            "Should have navigation to main sections");
    }

    [TestMethod]
    public async Task Layout_HasThemeToggle()
    {
        await Page.GotoAsync(ServerUrl);
        
        var themeToggle = await Page.QuerySelectorAsync(".theme-toggle");
        Assert.IsNotNull(themeToggle, "Theme toggle should be present");
    }
}
