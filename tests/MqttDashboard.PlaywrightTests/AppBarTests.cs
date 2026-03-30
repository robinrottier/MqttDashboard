using Microsoft.Playwright;

namespace MqttDashboard.PlaywrightTests;

/// <summary>
/// Tests the responsive AppBar behaviour at different viewport widths.
/// Covers hamburger visibility, edit-toggle hide/show, and menu opening.
/// </summary>
public class AppBarTests : IClassFixture<PlaywrightWebAppFixture>
{
    private readonly PlaywrightWebAppFixture _fixture;

    public AppBarTests(PlaywrightWebAppFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IPage> NewPageWithSizeAsync(int width, int height)
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Wait for Blazor Server circuit to be ready
        await page.Locator("header.mud-appbar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        return page;
    }

    [Fact]
    public async Task AppBar_NarrowViewport_HamburgerStillVisible()
    {
        var page = await NewPageWithSizeAsync(320, 600);
        try
        {
            // The hamburger is in .appbar-menu-pin which has flex-shrink:0 — it must always be visible.
            var hamburger = page.Locator(".appbar-menu-pin button");
            await Assertions.Expect(hamburger).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AppBar_WideViewport_EditToggleVisible()
    {
        var page = await NewPageWithSizeAsync(1024, 768);
        try
        {
            // The edit toggle is inside .toolbar-hide-xs — visible only at >= 600px.
            // It is only rendered when auth is NOT required (default test config has no admin hash).
            var editSwitch = page.Locator(".toolbar-hide-xs .mud-switch-base").First;
            await editSwitch.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
            await Assertions.Expect(editSwitch).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AppBar_NarrowViewport_EditToggleHidden()
    {
        var page = await NewPageWithSizeAsync(320, 600);
        try
        {
            // .toolbar-hide-xs items have display:none at < 600px.
            var editToggleContainer = page.Locator(".toolbar-hide-xs").First;
            // It may not even be in the DOM if the user is not admin; if it is, it must be hidden.
            var count = await editToggleContainer.CountAsync();
            if (count > 0)
                await Assertions.Expect(editToggleContainer).ToBeHiddenAsync();
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AppBar_HamburgerMenu_Opens()
    {
        var page = await NewPageWithSizeAsync(400, 700);
        try
        {
            var hamburger = page.Locator(".appbar-menu-pin button");
            await hamburger.ClickAsync();

            // The MudMenu popover should become visible with at least one menu item
            var menuList = page.Locator(".mud-menu-list").First;
            await menuList.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
            await Assertions.Expect(menuList).ToBeVisibleAsync();
        }
        finally { await page.CloseAsync(); }
    }
}
