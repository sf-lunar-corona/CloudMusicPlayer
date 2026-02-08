namespace CloudMusicPlayer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Apply saved theme
        var isDarkTheme = Preferences.Get("dark_theme", false);
        UserAppTheme = isDarkTheme ? AppTheme.Dark : AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
