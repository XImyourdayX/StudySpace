
using System.Diagnostics;
using Aplicacion.CasosDeUso;
using StudySpace.Servicios;

namespace StudySpace;

public partial class App : Application
{
    private readonly ServicioSesionRemota _sesionRemota;
    private readonly ServicioAutenticacion _auth;

    // Evitar validaciones simultaneas.
    private bool _validandoSesion;

    public App(
        ServicioSesionRemota sesionRemota,
        ServicioAutenticacion auth)
    {
        InitializeComponent();

        _sesionRemota = sesionRemota;
        _auth = auth;

        // CONSERVAR EL TEMA ELEGIDO
        RestaurarTema();
    }

    // =====================================================
    // RESTAURAR TEMA
    // =====================================================

    private void RestaurarTema()
    {
        var temaGuardado = Preferences.Default.Get(
            "TemaAplicacion",
            "Sistema");

        UserAppTheme = temaGuardado switch
        {
            "Claro" => AppTheme.Light,
            "Oscuro" => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    // =====================================================
    // CREAR VENTANA PRINCIPAL
    // =====================================================

    protected override Window CreateWindow(
        IActivationState? activationState)
    {
        var shell = new AppShell();

        // Comprobar el token al abrir la aplicacion.
        shell.Loaded += AlCargarShell;

        var ventana = new Window(shell);

        // NUEVO:
        // Comprobar el token cuando la aplicacion
        // vuelve desde segundo plano.
        ventana.Resumed += AlReanudarVentana;

        return ventana;
    }

    // =====================================================
    // PRIMERA APERTURA DE STUDY SPACE
    // =====================================================

    private async void AlCargarShell(
        object? sender,
        EventArgs e)
    {
        if (sender is not AppShell shell)
            return;

        shell.Loaded -= AlCargarShell;

        await ComprobarSesionAsync(
            shell,
            aperturaInicial: true);
    }

    // =====================================================
    // REGRESAR DESDE SEGUNDO PLANO
    // =====================================================

    private async void AlReanudarVentana(
        object? sender,
        EventArgs e)
    {
        if (sender is not Window ventana ||
            ventana.Page is not AppShell shell)
        {
            return;
        }

        // IMPORTANTE:
        //
        // Si el usuario esta en bienvenida,
        // registro o verificacion de correo,
        // no interrumpimos esas pantallas.
        //
        // Esto permite cambiar a Gmail para
        // consultar el codigo y regresar
        // sin perder el formulario.
        if (_auth.UsuarioActual == null)
            return;

        // Si existe una sesion local activa,
        // volvemos a consultar el token en MySQL.
        await ComprobarSesionAsync(
            shell,
            aperturaInicial: false);
    }

    // =====================================================
    // VALIDAR SESION CON MYSQL
    // =====================================================

    private async Task ComprobarSesionAsync(
        AppShell shell,
        bool aperturaInicial)
    {
        if (_validandoSesion)
            return;

        _validandoSesion = true;

        // Bloquear la interaccion mientras
        // se comprueba la sesion.
        shell.IsEnabled = false;

        // Ocultar temporalmente el contenido
        // para no mostrar cursos o tareas
        // mientras se valida el acceso.
        shell.Opacity = 0;

        try
        {
            // ServicioSesionRemota:
            //
            // 1. Lee el token de SecureStorage.
            // 2. Consulta sesion-validar.php.
            // 3. Comprueba la respuesta de MySQL.
            // 4. Recupera el perfil local vinculado.
            var (restaurada, mensaje) =
                await _sesionRemota.RestaurarAsync();

            if (restaurada)
            {
                if (aperturaInicial)
                {
                    // Al abrir la aplicacion,
                    // entrar a la pantalla principal.
                    await shell.GoToAsync("//principal");
                }

                // Al regresar desde segundo plano,
                // conservar la pestaña actual.
                return;
            }

            Debug.WriteLine(
                "Study Space - Sesion: " + mensaje);

            if (!aperturaInicial)
            {
                // Si el token vencio, fue revocado
                // o no se pudo verificar, impedir
                // el acceso a las pantallas privadas.
                await shell.GoToAsync("//bienvenida");
            }

            // En la apertura inicial ya estamos
            // en bienvenida, por lo que no hace
            // falta navegar nuevamente.
        }
        catch (Exception ex)
        {
            // Ante un error inesperado,
            // no mantener una sesion local activa.
            _auth.CerrarSesion();

            Debug.WriteLine(
                "Study Space - Error de sesion: " +
                ex.GetType().Name);

            if (!aperturaInicial)
            {
                try
                {
                    await shell.GoToAsync("//bienvenida");
                }
                catch (Exception)
                {
                    Debug.WriteLine(
                        "Study Space - Error de navegacion.");
                }
            }
        }
        finally
        {
            // Restaurar la interfaz.
            shell.Opacity = 1;
            shell.IsEnabled = true;

            _validandoSesion = false;
        }
    }
}
