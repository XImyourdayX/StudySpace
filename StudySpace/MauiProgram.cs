using Aplicacion.CasosDeUso;
using Aplicacion.Interfaces;
using Infraestructura.DBContext;
using Infraestructura.Repositorios;
using Microsoft.Extensions.Logging;
using StudySpace.Servicios;
using StudySpace.VistaModelos;
using StudySpace.Vistas;

namespace StudySpace;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIconsOutlined-Regular.otf", "IconosMaterial");
            });

        // Servicios
        var ruta = Path.Combine(FileSystem.AppDataDirectory, "studyspace.db3");
        builder.Services.AddSingleton(new ContextoBaseDatos(ruta));
        builder.Services.AddSingleton<IRepositorioUsuarios, RepositorioUsuarios>();
        builder.Services.AddSingleton<IRepositorioCursos, RepositorioCursos>();
        builder.Services.AddSingleton<IAlmacenSesion, AlmacenSesionPreferences>();
        builder.Services.AddSingleton<ServicioAutenticacion>();
        builder.Services.AddSingleton<ServicioCursos>();
        builder.Services.AddSingleton<IRepositorioTareas, RepositorioTareas>();
        builder.Services.AddSingleton<ServicioTareas>();

        // ViewModels
        builder.Services.AddTransient<InicioSesionVM>();
        builder.Services.AddTransient<RegistroVM>();

        // Páginas
        builder.Services.AddTransient<PaginaInicioSesion>();
        builder.Services.AddTransient<PaginaRegistro>();
        builder.Services.AddSingleton<ServicioCursos>();
        builder.Services.AddTransient<CursosVM>();
        builder.Services.AddTransient<PaginaCursos>();
        builder.Services.AddTransient<NuevaTareaVM>();
        builder.Services.AddTransient<PaginaNuevaTarea>();
        builder.Services.AddTransient<PrincipalVM>();
        builder.Services.AddTransient<PerfilVM>();
        builder.Services.AddTransient<PaginaPrincipal>();
        builder.Services.AddTransient<PaginaPerfil>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SinBorde", (handler, view) =>
        {
#if WINDOWS
Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("SinBorde", (handler, view) =>
{
    handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
    handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
});
Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("SinBorde", (handler, view) =>
{
    handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
    handler.PlatformView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
});
#endif
#if ANDROID
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
    var transparente = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
    handler.PlatformView.Resources["TextControlBorderBrush"] = transparente;
    handler.PlatformView.Resources["TextControlBorderBrushPointerOver"] = transparente;
    handler.PlatformView.Resources["TextControlBorderBrushFocused"] = transparente;
    handler.PlatformView.Resources["TextControlBackground"] = transparente;
    handler.PlatformView.Resources["TextControlBackgroundPointerOver"] = transparente;
    handler.PlatformView.Resources["TextControlBackgroundFocused"] = transparente;
#endif
        });

        return builder.Build();
    }
}
