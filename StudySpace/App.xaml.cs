
using System.Diagnostics;
using Aplicacion.CasosDeUso;
using StudySpace.Servicios;

#if ANDROID
using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;
#endif

namespace StudySpace;

public partial class App : Application
{
    private readonly ServicioSesionRemota _sesionRemota;
    private readonly ServicioAutenticacion _auth;
    private readonly ServicioTareas _tareas;
    private readonly ServicioNotificaciones _notificaciones;

    // HU13: se retiene la acción hasta que la sesión remota se valide.
    private readonly SemaphoreSlim _bloqueoPosponer = new(1, 1);
    private int _tareaPendienteDePosponer;
#if ANDROID
    private bool _accionDeArranqueRevisada;
#endif

    // Evitar validaciones simultaneas.
    private bool _validandoSesion;

    public App(
        ServicioSesionRemota sesionRemota,
        ServicioAutenticacion auth,
        ServicioTareas tareas,
        ServicioNotificaciones notificaciones)
    {
        InitializeComponent();

        _sesionRemota = sesionRemota;
        _auth = auth;
        _tareas = tareas;
        _notificaciones = notificaciones;

#if ANDROID
        // HU13: registrar el manejador de la acción configurada en MauiProgram.
        LocalNotificationCenter.Current.NotificationActionTapped +=
            AlPulsarAccionDeNotificacion;
#endif

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

#if ANDROID
        // Si Android inició la app desde la acción, conservar la tarea.
        RecuperarAccionDeArranque();
#endif

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

                // HU13: procesar únicamente después de validar la identidad.
                // Se mantiene en cola si el usuario está sin conexión o sin sesión.
                await ProcesarPosposicionPendienteAsync();

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
    // =====================================================
    // HU13 - POSPONER DESDE UNA NOTIFICACIÓN ANDROID
    // =====================================================

#if ANDROID
    private void AlPulsarAccionDeNotificacion(NotificationActionEventArgs e)
    {
        // El ID 1301 coincide con MauiProgram.cs.
        // Ignorar el toque normal y el descarte de notificaciones.
        if (e.ActionId != 1301 || e.IsTapped || e.IsDismissed ||
            e.Request is null)
        {
            return;
        }

        RegistrarPosposicionPendiente(e.Request.NotificationId);
    }

    private void RecuperarAccionDeArranque()
    {
        if (_accionDeArranqueRevisada)
            return;

        _accionDeArranqueRevisada = true;
        var detalles = LocalNotificationCenter.LaunchNotificationDetails;

        if (detalles?.DidNotificationLaunchApp == true &&
            detalles.ActionId == 1301 &&
            detalles.Request is not null)
        {
            RegistrarPosposicionPendiente(
                detalles.Request.NotificationId);
        }
    }

    private void RegistrarPosposicionPendiente(int tareaId)
    {
        if (tareaId <= 0)
            return;

        // Una sola petición pendiente; las pulsaciones repetidas de
        // la misma notificación se ignoran al programar el aviso.
        Interlocked.Exchange(ref _tareaPendienteDePosponer, tareaId);

        if (_auth.UsuarioActual is not null && !_validandoSesion)
        {
            _ = MainThread.InvokeOnMainThreadAsync(
                ProcesarPosposicionPendienteAsync);
        }
    }
#endif

    private async Task ProcesarPosposicionPendienteAsync()
    {
        await _bloqueoPosponer.WaitAsync();
        try
        {
            if (_auth.UsuarioActual is null)
                return;

            int tareaId = Interlocked.Exchange(
                ref _tareaPendienteDePosponer, 0);

            if (tareaId <= 0)
                return;

            // El caso de uso comprueba que la tarea pertenece
            // al usuario local actualmente autenticado.
            var tarea = await _tareas.ObtenerPorId(tareaId);

            if (tarea is null || tarea.Completada ||
                tarea.FechaLimite <= DateTime.Now)
            {
                Debug.WriteLine(
                    $"HU13: se descartó la posposición de tarea {tareaId}.");
                return;
            }

            bool pospuesta = await _notificaciones.PosponerRecordatorioAsync(
                tarea.Id,
                tarea.Titulo,
                tarea.FechaLimite,
                tarea.MinutosAviso,
                tarea.CursoNombre);

            Debug.WriteLine(
                $"HU13: tarea {tareaId}, pospuesta: {pospuesta}.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"HU13: error al posponer: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _bloqueoPosponer.Release();
        }
    }
}
