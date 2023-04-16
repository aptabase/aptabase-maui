using Aptabase.Maui;

namespace HelloWorld;

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


