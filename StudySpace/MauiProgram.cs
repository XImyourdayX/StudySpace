using Aplicacion.CasosDeUso;
using Aplicacion.Interfaces;
using Infraestructura.DBContext;
using Infraestructura.Repositorios;
using Microsoft.Extensions.Logging;
using StudySpace.Servicios;
using StudySpace.VistaModelos;
using StudySpace.Vistas;

#if ANDROID
using Plugin.LocalNotification;
using Xamarin.Android.Net;
#endif

namespace StudySpace;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

#if ANDROID
        // HU05 - NOTIFICACIONES LOCALES
        builder.UseLocalNotification();
#endif

        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont(
                "OpenSans-Regular.ttf",
                "OpenSansRegular");

            fonts.AddFont(
                "OpenSans-Semibold.ttf",
                "OpenSansSemibold");

            fonts.AddFont(
                "MaterialIconsOutlined-Regular.otf",
                "IconosMaterial");
        });

        // ============================================
        // BASE DE DATOS SQLITE LOCAL
        // ============================================

        var ruta = Path.Combine(
            FileSystem.AppDataDirectory,
            "studyspace.db3");

        builder.Services.AddSingleton(
            new ContextoBaseDatos(ruta));

        // ============================================
        // REPOSITORIOS
        // ============================================

        builder.Services.AddSingleton<
            IRepositorioUsuarios,
            RepositorioUsuarios>();

        builder.Services.AddSingleton<
            IRepositorioCursos,
            RepositorioCursos>();

        builder.Services.AddSingleton<
            IRepositorioTareas,
            RepositorioTareas>();

        // VINCULACION ENTRE IDENTIDADES MYSQL Y SQLITE
        builder.Services.AddSingleton<RepositorioVinculosUsuario>();

        // ============================================
        // SESION Y SERVICIOS ACADEMICOS
        // ============================================

        builder.Services.AddSingleton<
            IAlmacenSesion,
            AlmacenSesionPreferences>();

        builder.Services.AddSingleton<ServicioAutenticacion>();
        builder.Services.AddSingleton<ServicioCursos>();
        builder.Services.AddSingleton<ServicioTareas>();

        // HU05 - NOTIFICACIONES
        builder.Services.AddSingleton<ServicioNotificaciones>();

        // ============================================
        // API PHP DE WOPRES
        // ============================================
        // Unico HttpClient registrado para la API.
        // El cliente local de ASP.NET Core se retiro.
        // HTTPS conserva la validacion del certificado.
        // ============================================

        builder.Services.AddKeyedSingleton<HttpClient>(
            "ApiPHP",
            (proveedor, clave) =>
            {
#if ANDROID
                var manejador = new AndroidMessageHandler();
                var cliente = new HttpClient(manejador);
#else
                var cliente = new HttpClient();
#endif

                cliente.BaseAddress = new Uri(
                    "https://pollosyparrillasgringos.wopres.com/studyspace-api/");

                cliente.Timeout = TimeSpan.FromSeconds(30);

                return cliente;
            });

        // ============================================
        // SERVICIOS DE LA API PHP
        // ============================================

        builder.Services.AddSingleton<ServicioVerificacionCorreo>();
        builder.Services.AddSingleton<ServicioSesionRemota>();
        builder.Services.AddSingleton<ServicioRecuperacionContrasena>();

        // REGISTRO PENDIENTE DE VERIFICACION
        builder.Services.AddSingleton<ServicioRegistroPendiente>();

        // ============================================
        // VIEWMODELS
        // ============================================

        builder.Services.AddTransient<InicioSesionVM>();
        builder.Services.AddTransient<RegistroVM>();
        builder.Services.AddTransient<CursosVM>();
        builder.Services.AddTransient<NuevaTareaVM>();
        builder.Services.AddTransient<PrincipalVM>();
        builder.Services.AddTransient<PerfilVM>();

        // HU12 - DETALLE DE TAREA
        builder.Services.AddTransient<DetalleTareaVM>();

        // DETALLE DE CURSO
        builder.Services.AddTransient<DetalleCursoVM>();

        // HU09 - CALENDARIO
        builder.Services.AddTransient<CalendarioVM>();

        // VERIFICACION DE CORREO
        builder.Services.AddTransient<VerificarCorreoVM>();

        // RECUPERACION DE CONTRASENA
        builder.Services.AddTransient<RecuperarContrasenaVM>();

        // ============================================
        // PAGINAS
        // ============================================

        builder.Services.AddTransient<PaginaInicioSesion>();
        builder.Services.AddTransient<PaginaRegistro>();
        builder.Services.AddTransient<PaginaCursos>();
        builder.Services.AddTransient<PaginaNuevaTarea>();
        builder.Services.AddTransient<PaginaPrincipal>();
        builder.Services.AddTransient<PaginaPerfil>();

        // HU12 - DETALLE DE TAREA
        builder.Services.AddTransient<PaginaDetalleTarea>();

        // DETALLE DE CURSO
        builder.Services.AddTransient<PaginaDetalleCurso>();

        // HU09 - CALENDARIO
        builder.Services.AddTransient<PaginaCalendario>();

        // VERIFICACION DE CORREO
        builder.Services.AddTransient<PaginaVerificarCorreo>();

        // RECUPERACION DE CONTRASENA
        builder.Services.AddTransient<PaginaRecuperarContrasena>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ============================================
        // QUITAR BORDES NATIVOS DE ENTRY
        // ============================================

        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
            "SinBorde",
            (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(
                        Android.Graphics.Color.Transparent);

#elif WINDOWS
                var transparente =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent);

                handler.PlatformView.BorderThickness =
                    new Microsoft.UI.Xaml.Thickness(0);

                handler.PlatformView.Resources[
                    "TextControlBorderBrush"] = transparente;

                handler.PlatformView.Resources[
                    "TextControlBorderBrushPointerOver"] = transparente;

                handler.PlatformView.Resources[
                    "TextControlBorderBrushFocused"] = transparente;

                handler.PlatformView.Resources[
                    "TextControlBackground"] = transparente;

                handler.PlatformView.Resources[
                    "TextControlBackgroundPointerOver"] = transparente;

                handler.PlatformView.Resources[
                    "TextControlBackgroundFocused"] = transparente;
#endif
            });

#if WINDOWS
        // ============================================
        // QUITAR BORDES NATIVOS DE PICKER
        // ============================================

        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping(
            "SinBorde",
            (handler, view) =>
            {
                handler.PlatformView.BorderThickness =
                    new Microsoft.UI.Xaml.Thickness(0);

                handler.PlatformView.Background =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent);
            });

        // ============================================
        // QUITAR BORDES NATIVOS DE DATEPICKER
        // ============================================

        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping(
            "SinBorde",
            (handler, view) =>
            {
                handler.PlatformView.BorderThickness =
                    new Microsoft.UI.Xaml.Thickness(0);

                handler.PlatformView.Background =
                    new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Transparent);
            });
#endif

        return builder.Build();
    }
}
