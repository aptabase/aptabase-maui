using Aptabase.Maui;

namespace HelloWorld;

public partial class App : Application
{
	public App(IAptabaseClient aptabase)
	{
		InitializeComponent();

		MainPage = new AppShell();

		aptabase.TrackEventAsync("app_started");
	}
}

