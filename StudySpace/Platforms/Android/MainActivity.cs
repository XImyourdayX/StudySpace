using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MauiApplication = Microsoft.Maui.Controls.Application;

namespace StudySpace
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private MauiApplication? _app;

        protected override void OnResume()
        {
            base.OnResume();
            _app = MauiApplication.Current;
            if (_app != null)
            {
                _app.RequestedThemeChanged -= AlCambiarTema;
                _app.RequestedThemeChanged += AlCambiarTema;
            }
            ActualizarBarras();
        }

        protected override void OnPause()
        {
            if (_app != null) _app.RequestedThemeChanged -= AlCambiarTema;
            _app = null;
            base.OnPause();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus) ActualizarBarras();
        }

        private void AlCambiarTema(object? sender, AppThemeChangedEventArgs e)
            => RunOnUiThread(ActualizarBarras);

        private void ActualizarBarras()
        {
            var ventana = Window;
            if (ventana == null) return;
            bool claro = MauiApplication.Current?.RequestedTheme != AppTheme.Dark;
            var fondo = Android.Graphics.Color.ParseColor(claro ? "#F6F7FB" : "#111427");
            var fondoNavegacion = Android.Graphics.Color.ParseColor(claro ? "#FFFFFF" : "#111427");
#pragma warning disable CS0618
            ventana.SetStatusBarColor(OperatingSystem.IsAndroidVersionAtLeast(23) ? fondo : Android.Graphics.Color.ParseColor("#111427"));
            ventana.SetNavigationBarColor(OperatingSystem.IsAndroidVersionAtLeast(26) ? fondoNavegacion : Android.Graphics.Color.ParseColor("#111427"));
            if (OperatingSystem.IsAndroidVersionAtLeast(30) && ventana.InsetsController is { } controlador)
            {
                var mascara = WindowInsetsControllerAppearance.LightStatusBars | WindowInsetsControllerAppearance.LightNavigationBars;
                controlador.SetSystemBarsAppearance(claro ? (int)mascara : 0, (int)mascara);
            }
            else
            {
                var decor = ventana.DecorView;
                int flags = (int)decor.SystemUiVisibility;
                // SYSTEM_UI_FLAG_LIGHT_STATUS_BAR / LIGHT_NAVIGATION_BAR
                const int status = 0x2000, nav = 0x10;
                flags &= ~(status | nav);
                if (claro && OperatingSystem.IsAndroidVersionAtLeast(23)) flags |= status;
                if (claro && OperatingSystem.IsAndroidVersionAtLeast(26)) flags |= nav;
                decor.SystemUiVisibility = (StatusBarVisibility)flags;
            }
#pragma warning restore CS0618
        }
    }
}
