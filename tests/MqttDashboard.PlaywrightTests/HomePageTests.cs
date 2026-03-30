using Microsoft.Playwright;

namespace MqttDashboard.PlaywrightTests;

/// <summary>
/// Smoke tests: verifies the home page loads and key toolbar elements are present.
/// </summary>
public class HomePageTests : IClassFixture<PlaywrightWebAppFixture>
{
    private readonly PlaywrightWebAppFixture _fixture;

    public HomePageTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private Task<IPage> NewPageAsync() =>
        _fixture.Browser!.NewPageAsync();

    [Fact]
    public async Task HomePage_Loads_TitleVisible()
    {
        var page = await NewPageAsync();
        try
        {
        await page.GotoAsync(_fixture.BaseUrl);

        // Wait for Blazor Server to establish its circuit and render
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The AppBar should contain the app name
        var header = page.Locator("header.mud-appbar");
        await header.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Assertions.Expect(header).ToBeVisibleAsync();

        // The "MQTT Dashboard" product name should be present somewhere in the title area
        var titleText = page.Locator(".appbar-title-inner");
        await Assertions.Expect(titleText).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task HomePage_Loads_HamburgerMenuVisible()
    {
        var page = await NewPageAsync();
        try
        {
        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The hamburger menu icon (the MudMenu activator button inside .appbar-menu-pin)
        var hamburger = page.Locator(".appbar-menu-pin button");
        await hamburger.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Assertions.Expect(hamburger).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task HomePage_Loads_MqttStatusIconVisible()
    {
        var page = await NewPageAsync();
        try
        {
        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The MQTT status icon is inside .toolbar-hide-xs which is only visible at >=600px.
        // Use a desktop viewport to ensure it is shown.
        await page.SetViewportSizeAsync(1280, 800);

        var mqttIcon = page.Locator(".toolbar-hide-xs svg").First;
        await mqttIcon.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        await Assertions.Expect(mqttIcon).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }
}
