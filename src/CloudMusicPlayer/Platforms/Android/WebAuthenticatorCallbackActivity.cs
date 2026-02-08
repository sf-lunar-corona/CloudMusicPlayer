using Android.App;
using Android.Content;
using Android.Content.PM;

namespace CloudMusicPlayer.Platforms.Android;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "com.googleusercontent.apps.436318736481-s6dq9f696n6vqm6vgskh9cq6g8ng4fus")]
public class WebAuthenticatorCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
