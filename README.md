![Aptabase](https://raw.githubusercontent.com/aptabase/aptabase/main/og.png)

# MAUI SDK for Aptabase

Instrument your apps with Aptabase, a privacy-first analytics platform for desktop, mobile and web apps.

## Install

Start by adding the Aptabase NuGet package to your .csproj:

```xml
<PackageReference Include="Aptabase.Maui" Version="0.0.2" />
```

## Usage

First you need to get your `App Key` from Aptabase, you can find it in the `Instructions` menu on the left side menu.

Change your `MauiProgram.cs` to add Aptabase to the build pipeline:

```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseAptabase("<YOUR_APP_KEY>") // 👈 this is where you enter your App Key
    ...
}
```

The `UseAptabase` method will add the `IAptabaseClient` to your dependency injection container, allowing you to use it in your pages and view models.

As an example, you can add the following code to your `MainPage.xaml.cs`:

```csharp
public partial class MainPage : ContentPage
{
    IAptabaseClient _aptabase;
    int count = 0;

    public MainPage(IAptabaseClient aptabase)
    {
        InitializeComponent();
        _aptabase = aptabase;
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        count++;
        _aptabase.TrackEvent("Increment");

        if (count == 1)
            CounterBtn.Text = $"Clicked {count} time";
        else
            CounterBtn.Text = $"Clicked {count} times";

        SemanticScreenReader.Announce(CounterBtn.Text);
    }
}
```

The `TrackEvent` method also supports custom properties:

```csharp
_aptabase.TrackEvent("connect_click"); // An event with no properties
_aptabase.TrackEvent("play_music", new() {  // An event with a custom property
    { "name", "Here comes the sun" }
});
```

A few important notes:

1. The SDK will automatically enhance the event with some useful information, like the OS, the app version, and other things.
2. You're in control of what gets sent to Aptabase. This SDK does not automatically track any events, you need to call `TrackEvent` manually.
   - Because of this, it's generally recommended to at least track an event at startup
3. The `TrackEvent` function is a non-blocking operation as it runs on the background.
4. Only strings and numbers values are allowed on custom properties
