using Aptabase.Core;

namespace HelloMaui;

public partial class App : Application
{
	public App(IAptabaseClient aptabase)
	{
		InitializeComponent();

		MainPage = new AppShell();

		aptabase.TrackEvent("app_started");
	}
}

