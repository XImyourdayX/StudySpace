using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aplicacion.CasosDeUso;
using Infraestructura.Repositorios;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using StudySpace.Servicios;

namespace StudySpace.VistaModelos;

public partial class PerfilVM : ObservableObject
{
    private readonly ServicioAutenticacion _auth;
    private readonly ServicioTareas _tareas;
    private readonly ServicioNotificaciones _notificaciones;
    private readonly HttpClient _clientePhp;
    private readonly RepositorioVinculosUsuario _vinculos;
    private int _versionCargaPerfil;

    // Misma clave usada por InicioSesionVM.
    // El token se guarda en SecureStorage, no en Preferences.
    private const string ClaveToken = "studyspace_api_php_token";

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
        ServicioNotificaciones notificaciones,
        [FromKeyedServices("ApiPHP")] HttpClient clientePhp,
        RepositorioVinculosUsuario vinculos)
    {
        _auth = auth;
        _tareas = tareas;
        _notificaciones = notificaciones;
        _clientePhp = clientePhp;
        _vinculos = vinculos;
    }

    // CARGAR PERFIL
    public void Cargar()
    {
        var usuarioLocal = _auth.UsuarioActual;

        Nombre = usuarioLocal?.Nombre ?? "";

        // SQLite utiliza un correo interno para relacionar
        // el perfil con los cursos y las tareas. Ese correo
        // no se debe mostrar como el correo real del usuario.
        string correoLocal = usuarioLocal?.Correo ?? "";
        Correo = correoLocal.EndsWith(
            "@studyspace.invalid",
            StringComparison.OrdinalIgnoreCase)
            ? "Cuenta vinculada a Study Space"
            : correoLocal;

        ActualizarTextoTema();
        CargarPreferenciasNotificaciones();

        // La pagina llama a Cargar() de forma sincronica.
        // Consultar el perfil remoto sin bloquear la pantalla.
        // La tarea controla sus propias excepciones.
        int version = ++_versionCargaPerfil;

        if (usuarioLocal != null)
        {
            _ = CargarDatosRemotosAsync(usuarioLocal.Id, version);
        }
    }

    // LEER EL NOMBRE Y CORREO REALES DESDE MYSQL
    // SIN GUARDARLOS EN PREFERENCES NI EN SQLITE.
    private async Task CargarDatosRemotosAsync(
        int usuarioLocalId,
        int version)
    {
        try
        {
            string? token = await SecureStorage.Default.GetAsync(
                ClaveToken);

            if (string.IsNullOrWhiteSpace(token) ||
                token.Length != 64 ||
                !token.All(Uri.IsHexDigit))
            {
                return;
            }

            using var solicitud = new HttpRequestMessage(
                HttpMethod.Get,
                "sesion-validar.php");

            // El token solamente se envia en esta solicitud.
            solicitud.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            using var respuesta = await _clientePhp.SendAsync(
                solicitud);

            if (!respuesta.IsSuccessStatusCode)
                return;

            var contenido = await respuesta.Content
                .ReadFromJsonAsync<RespuestaValidarSesion>();

            var usuarioRemoto = contenido?.Usuario;

            if (contenido?.Estado != "sesion_valida" ||
                usuarioRemoto == null ||
                usuarioRemoto.Id <= 0 ||
                string.IsNullOrWhiteSpace(usuarioRemoto.Nombre) ||
                string.IsNullOrWhiteSpace(usuarioRemoto.Correo))
            {
                return;
            }

            // Asegurarnos de que la identidad devuelta por
            // MySQL corresponde al perfil SQLite activo.
            int? idLocalVinculado = await _vinculos
                .ObtenerUsuarioLocalIdAsync(usuarioRemoto.Id);

            if (idLocalVinculado != usuarioLocalId)
                return;

            // Descartar respuestas antiguas si la pantalla
            // se recargo o el usuario cerro/cambio sesion.
            if (version != _versionCargaPerfil ||
                _auth.UsuarioActual?.Id != usuarioLocalId)
            {
                return;
            }

            Nombre = usuarioRemoto.Nombre.Trim();
            Correo = usuarioRemoto.Correo.Trim();
        }
        catch (Exception)
        {
            // Sin conexion o error temporal: conservar el
            // perfil local sin mostrar correos internos.
            // El token y los cursos/tareas no se modifican.
        }
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

    // =====================================================
    // CERRAR SESION EN MYSQL Y EN EL DISPOSITIVO
    // =====================================================
    //
    // Primero se revoca el token en Wopres.
    // Despues se borra de SecureStorage y se limpia
    // el perfil local que usan cursos y tareas.
    //
    // Si no hay conexion, la sesion se conserva para
    // poder reintentar la revocacion mas tarde.
    // =====================================================
    [RelayCommand]
    private async Task CerrarSesion()
    {
        if (Ocupado)
            return;

        bool confirmar = await Shell.Current.DisplayAlert(
            "Cerrar sesión",
            "¿Seguro que quieres salir?",
            "Sí",
            "No");

        if (!confirmar)
            return;

        Ocupado = true;
        bool sesionLocalLimpiada = false;

        try
        {
            // La clave coincide con InicioSesionVM.
            string? token = await SecureStorage.Default.GetAsync(
                ClaveToken);

            if (!string.IsNullOrWhiteSpace(token))
            {
                // Cabecera exclusiva de esta peticion.
                // No modificar DefaultRequestHeaders de un
                // HttpClient compartido por otros servicios.
                using var solicitud = new HttpRequestMessage(
                    HttpMethod.Post,
                    "sesion-cerrar.php");

                solicitud.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        token);

                using var respuesta =
                    await _clientePhp.SendAsync(solicitud);

                var contenido = await respuesta.Content
                    .ReadFromJsonAsync<RespuestaCerrarSesion>();

                // El token puede estar ya revocado o vencido.
                // En ese caso tambien se puede limpiar el
                // dispositivo si el servidor lo confirma.
                bool cierreConfirmado =
                    respuesta.IsSuccessStatusCode &&
                    contenido?.Estado == "sesion_cerrada";

                bool sesionYaInvalida =
                    (respuesta.IsSuccessStatusCode ||
                     respuesta.StatusCode == HttpStatusCode.Unauthorized) &&
                    contenido?.Estado == "sesion_invalida";

                if (!cierreConfirmado && !sesionYaInvalida)
                {
                    await Shell.Current.DisplayAlert(
                        "No se pudo cerrar sesión",
                        contenido?.Mensaje ??
                        "El servidor no confirmó el cierre de sesión. " +
                        "Inténtalo nuevamente.",
                        "Aceptar");

                    return;
                }
            }

            // Solo despues de confirmar el cierre remoto
            // (o si ya no existe un token en el dispositivo).
            SecureStorage.Default.Remove(ClaveToken);
            _auth.CerrarSesion();
            sesionLocalLimpiada = true;

            await Shell.Current.GoToAsync("//bienvenida");
        }
        catch (TaskCanceledException)
        {
            await MostrarErrorCierreAsync(
                sesionLocalLimpiada,
                "Se agotó el tiempo de espera. Revisa tu conexión " +
                "a internet e inténtalo nuevamente.");
        }
        catch (HttpRequestException)
        {
            await MostrarErrorCierreAsync(
                sesionLocalLimpiada,
                "No se pudo conectar con el servidor de Study Space. " +
                "La sesión sigue abierta para que puedas reintentarlo.");
        }
        catch (JsonException)
        {
            await MostrarErrorCierreAsync(
                sesionLocalLimpiada,
                "El servidor devolvió una respuesta no válida. " +
                "Inténtalo nuevamente.");
        }
        catch (Exception)
        {
            await MostrarErrorCierreAsync(
                sesionLocalLimpiada,
                "No se pudo completar el cierre de sesión. " +
                "Inténtalo nuevamente.");
        }
        finally
        {
            Ocupado = false;
        }
    }

    private static Task MostrarErrorCierreAsync(
        bool sesionLocalLimpiada,
        string mensaje)
    {
        return Shell.Current.DisplayAlert(
            sesionLocalLimpiada
                ? "Sesión cerrada"
                : "No se pudo cerrar sesión",
            sesionLocalLimpiada
                ? "La sesión ya se cerró, pero no fue posible " +
                  "abrir la pantalla de bienvenida."
                : mensaje,
            "Aceptar");
    }

    private sealed class RespuestaCerrarSesion
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("mensaje")]
        public string? Mensaje { get; set; }
    }
    private sealed class RespuestaValidarSesion
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("usuario")]
        public UsuarioRemotoApi? Usuario { get; set; }
    }

    private sealed class UsuarioRemotoApi
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("nombre")]
        public string? Nombre { get; set; }

        [JsonPropertyName("correo")]
        public string? Correo { get; set; }
    }

}
