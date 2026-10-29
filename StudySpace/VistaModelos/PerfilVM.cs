using Aplicacion.CasosDeUso;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySpace.Servicios;

namespace StudySpace.VistaModelos;

public partial class PerfilVM : ObservableObject
{
    private readonly ServicioAutenticacion _auth;
    private readonly ServicioTareas _tareas;
    private readonly ServicioNotificaciones _notificaciones;

    // HU11 - CLAVES DE CONFIGURACIÓN
    private const string ClaveNotificaciones =
        "HU11_NotificacionesActivas";

    private const string ClaveAnticipacion =
        "HU11_AnticipacionMinutos";

    private const string ClaveHoraInicio =
        "HU11_HoraInicio";

    private const string ClaveHoraFin =
        "HU11_HoraFin";

    [ObservableProperty]
    private string nombre = "";

    [ObservableProperty]
    private string correo = "";

    [ObservableProperty]
    private string temaActual = "Sistema";

    // HU11 - PREFERENCIAS
    [ObservableProperty]
    private bool notificacionesActivas = true;

    [ObservableProperty]
    private int minutosAnticipacion = 1440;

    [ObservableProperty]
    private int horaInicio = 7;

    [ObservableProperty]
    private int horaFin = 22;

    [ObservableProperty]
    private bool ocupado;

    public PerfilVM(
        ServicioAutenticacion auth,
        ServicioTareas tareas,
        ServicioNotificaciones notificaciones)
    {
        _auth = auth;
        _tareas = tareas;
        _notificaciones = notificaciones;
    }

    // CARGAR PERFIL
    public void Cargar()
    {
        Nombre = _auth.UsuarioActual?.Nombre ?? "";
        Correo = _auth.UsuarioActual?.Correo ?? "";

        ActualizarTextoTema();
        CargarPreferenciasNotificaciones();
    }

    // CARGAR PREFERENCIAS GUARDADAS
    private void CargarPreferenciasNotificaciones()
    {
        NotificacionesActivas = Preferences.Default.Get(
            ClaveNotificaciones,
            true);

        MinutosAnticipacion = Preferences.Default.Get(
            ClaveAnticipacion,
            1440);

        HoraInicio = Preferences.Default.Get(
            ClaveHoraInicio,
            7);

        HoraFin = Preferences.Default.Get(
            ClaveHoraFin,
            22);
    }

    // MOSTRAR TEMA ACTUAL
    private void ActualizarTextoTema()
    {
        var tema = Application.Current?.UserAppTheme
            ?? AppTheme.Unspecified;

        TemaActual = tema switch
        {
            AppTheme.Light => "Claro",
            AppTheme.Dark => "Oscuro",
            _ => "Sistema"
        };
    }

    // HU11 - SINCRONIZAR RECORDATORIOS
    // DEVUELVE LA CANTIDAD DE AVISOS PROGRAMADOS
    private async Task<int> SincronizarRecordatorios(
        bool activar)
    {
        var tareas = await _tareas.ObtenerTareas();

        int programados = 0;

        foreach (var tarea in tareas)
        {
            // CANCELAR AVISO ANTERIOR
            _notificaciones.CancelarRecordatorio(tarea.Id);

            // NO PROGRAMAR SI ESTÁN DESACTIVADAS
            // O SI LA TAREA YA ESTÁ COMPLETADA
            if (!activar || tarea.Completada)
                continue;

            // NO PROGRAMAR TAREAS YA VENCIDAS
            if (tarea.FechaLimite <= DateTime.Now)
                continue;

            // EL SERVICIO CALCULARÁ EL HORARIO
            // PERMITIDO SEGÚN LAS PREFERENCIAS
            bool programada =
                await _notificaciones.ProgramarRecordatorio(
                    tarea.Id,
                    tarea.Titulo,
                    tarea.FechaLimite,
                    tarea.MinutosAviso,
                    tarea.CursoNombre);

            if (programada)
                programados++;
        }

        return programados;
    }

    // HU11 - ACTIVAR O DESACTIVAR NOTIFICACIONES
    [RelayCommand]
    private async Task Notificaciones()
    {
        if (Ocupado)
            return;

        string estado = NotificacionesActivas
            ? "Activadas"
            : "Desactivadas";

        string? opcion = await Shell.Current.DisplayActionSheet(
            $"Notificaciones: {estado}",
            "Cancelar",
            null,
            "Activar notificaciones",
            "Desactivar notificaciones");

        bool activar;

        switch (opcion)
        {
            case "Activar notificaciones":
                activar = true;
                break;

            case "Desactivar notificaciones":
                activar = false;
                break;

            default:
                return;
        }

        Ocupado = true;

        try
        {
            NotificacionesActivas = activar;

            Preferences.Default.Set(
                ClaveNotificaciones,
                activar);

            int programados =
                await SincronizarRecordatorios(activar);

            string mensaje = activar
                ? $"Notificaciones activadas.\n" +
                  $"Recordatorios programados: {programados}."
                : "Notificaciones desactivadas.\n" +
                  "Se cancelaron los recordatorios " +
                  "de tus tareas actuales.";

            await Shell.Current.DisplayAlert(
                "Configuración guardada",
                mensaje,
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Aviso",
                "La preferencia se guardó, pero no fue " +
                "posible actualizar todos los recordatorios.\n" +
                ex.Message,
                "OK");
        }
        finally
        {
            Ocupado = false;
        }
    }

    // HU11 - CONFIGURAR RECORDATORIOS
    [RelayCommand]
    private async Task Recordatorios()
    {
        if (Ocupado)
            return;

        string? opcion = await Shell.Current.DisplayActionSheet(
            "Configuración de recordatorios",
            "Cancelar",
            null,
            "Anticipación predeterminada",
            "Horario permitido");

        switch (opcion)
        {
            case "Anticipación predeterminada":
                await ConfigurarAnticipacion();
                break;

            case "Horario permitido":
                await ConfigurarHorario();
                break;
        }
    }

    // HU11 - ELEGIR ANTICIPACIÓN PREDETERMINADA
    private async Task ConfigurarAnticipacion()
    {
        string? opcion = await Shell.Current.DisplayActionSheet(
            "¿Con cuánta anticipación deseas recibir avisos?",
            "Cancelar",
            null,
            "15 minutos antes",
            "1 hora antes",
            "1 día antes",
            "2 días antes");

        int minutos = opcion switch
        {
            "15 minutos antes" => 15,
            "1 hora antes" => 60,
            "1 día antes" => 1440,
            "2 días antes" => 2880,
            _ => -1
        };

        if (minutos < 0)
            return;

        MinutosAnticipacion = minutos;

        Preferences.Default.Set(
            ClaveAnticipacion,
            MinutosAnticipacion);

        await Shell.Current.DisplayAlert(
            "Configuración guardada",
            $"Anticipación predeterminada: {opcion}.\n" +
            "Se aplicará al crear nuevas tareas. " +
            "Las tareas existentes conservarán " +
            "su propia anticipación.",
            "OK");
    }

    // HU11 - CONFIGURAR HORARIO PERMITIDO
    private async Task ConfigurarHorario()
    {
        if (Ocupado)
            return;

        string horarioActual =
            $"{HoraInicio:00}:00 - {HoraFin:00}:00";

        string? opcion = await Shell.Current.DisplayActionSheet(
            $"Horario actual: {horarioActual}",
            "Cancelar",
            null,
            "Cambiar hora de inicio",
            "Cambiar hora de fin");

        if (opcion != "Cambiar hora de inicio" &&
            opcion != "Cambiar hora de fin")
        {
            return;
        }

        bool cambiarInicio =
            opcion == "Cambiar hora de inicio";

        string[] horas = Enumerable.Range(0, 24)
            .Select(h => $"{h:00}:00")
            .ToArray();

        string? horaElegida = await Shell.Current.DisplayActionSheet(
            cambiarInicio
                ? "Selecciona la hora de inicio"
                : "Selecciona la hora de fin",
            "Cancelar",
            null,
            horas);

        if (string.IsNullOrWhiteSpace(horaElegida))
            return;

        if (!int.TryParse(
            horaElegida.Split(':')[0],
            out int horaSeleccionada))
        {
            return;
        }

        int nuevoInicio = cambiarInicio
            ? horaSeleccionada
            : HoraInicio;

        int nuevoFin = cambiarInicio
            ? HoraFin
            : horaSeleccionada;

        if (nuevoInicio >= nuevoFin)
        {
            await Shell.Current.DisplayAlert(
                "Horario no válido",
                "La hora de inicio debe ser anterior " +
                "a la hora de fin.",
                "OK");

            return;
        }

        Ocupado = true;

        try
        {
            // GUARDAR NUEVO HORARIO
            HoraInicio = nuevoInicio;
            HoraFin = nuevoFin;

            Preferences.Default.Set(
                ClaveHoraInicio,
                HoraInicio);

            Preferences.Default.Set(
                ClaveHoraFin,
                HoraFin);

            // HU11 - ACTUALIZAR RECORDATORIOS EXISTENTES
            // SOLO SI LAS NOTIFICACIONES ESTÁN ACTIVADAS
            int programados = await SincronizarRecordatorios(
                NotificacionesActivas);

            await Shell.Current.DisplayAlert(
                "Horario actualizado",
                $"Nuevo horario: " +
                $"{HoraInicio:00}:00 - {HoraFin:00}:00.\n" +
                $"Recordatorios programados: {programados}.\n" +
                "Los avisos que no puedan programarse " +
                "antes del vencimiento se omitirán.",
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Aviso",
                "El horario se guardó, pero no fue posible " +
                "actualizar todos los recordatorios.\n" +
                ex.Message,
                "OK");
        }
        finally
        {
            Ocupado = false;
        }
    }

    // CAMBIAR TEMA
    [RelayCommand]
    private async Task CambiarTema()
    {
        var opcion = await Shell.Current.DisplayActionSheet(
            "Tema de la aplicación",
            "Cancelar",
            null,
            "Claro",
            "Oscuro",
            "Sistema");

        if (Application.Current == null)
            return;

        switch (opcion)
        {
            case "Claro":
                Application.Current.UserAppTheme =
                    AppTheme.Light;
                break;

            case "Oscuro":
                Application.Current.UserAppTheme =
                    AppTheme.Dark;
                break;

            case "Sistema":
                Application.Current.UserAppTheme =
                    AppTheme.Unspecified;
                break;

            default:
                return;
        }

        Preferences.Default.Set(
            "TemaAplicacion",
            opcion);

        ActualizarTextoTema();
    }

    // AYUDA Y SOPORTE
    [RelayCommand]
    private async Task Ayuda()
    {
        await Shell.Current.DisplayAlert(
            "Ayuda y soporte",
            "Study Space te ayuda a organizar tus cursos, " +
            "tareas y fechas de entrega.",
            "Entendido");
    }

    // CERRAR SESIÓN
    [RelayCommand]
    private async Task CerrarSesion()
    {
        var confirmar = await Shell.Current.DisplayAlert(
            "Cerrar sesión",
            "¿Seguro que quieres salir?",
            "Sí",
            "No");

        if (!confirmar)
            return;

        _auth.CerrarSesion();

        await Shell.Current.GoToAsync("//bienvenida");
    }
}
