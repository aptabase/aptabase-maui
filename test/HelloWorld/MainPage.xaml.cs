using Aptabase.Maui;

namespace HelloWorld;

public partial class MainPage : ContentPage
{
    private readonly IAptabaseClient _aptabase;
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

	private async void OnTrackHandledErrorClicked(object sender, EventArgs e)
	{
		try
		{
			ThrowDeep("handled test error from HelloWorld");
		}
		catch (Exception ex)
		{
			await _aptabase.TrackError(ex);
		}
	}

	private async void OnTrackFatalErrorClicked(object sender, EventArgs e)
	{
		try
		{
			ThrowDeep("fatal test error from HelloWorld");
		}
		catch (Exception ex)
		{
			await _aptabase.TrackError(ex, fatal: true);
		}
	}

	private void OnCrashClicked(object sender, EventArgs e)
	{
		ThrowDeep("unhandled test crash from HelloWorld");
	}

	// A little call depth so the reported stack trace has something to show
	private static void ThrowDeep(string message) => ThrowInner(message);

	private static void ThrowInner(string message) => throw new InvalidOperationException(message);
}
