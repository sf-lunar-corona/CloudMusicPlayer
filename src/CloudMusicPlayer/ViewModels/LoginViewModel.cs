using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IGoogleAuthService _authService;

    [ObservableProperty]
    private bool _isSigningIn;

    [ObservableProperty]
    private string _statusMessage = "Sign in with your Google account to access your music on Google Drive.";

    public LoginViewModel(IGoogleAuthService authService)
    {
        _authService = authService;
        Title = "Cloud Music Player";
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsSigningIn) return;

        try
        {
            IsSigningIn = true;
            StatusMessage = "Signing in...";

            var credential = await _authService.SignInAsync();
            if (credential != null)
            {
                StatusMessage = "Sign in successful!";
                await Shell.Current.GoToAsync("//library");
            }
            else
            {
                StatusMessage = "Sign in was cancelled or failed. Please try again.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sign in error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Sign in error: {ex}");
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    [RelayCommand]
    private async Task CheckAutoLoginAsync()
    {
        try
        {
            var isSignedIn = await _authService.IsSignedInAsync();
            if (isSignedIn)
            {
                await Shell.Current.GoToAsync("//library");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-login check failed: {ex}");
        }
    }
}
