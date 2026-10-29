
#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;
#endif

namespace StudySpace.Servicios;

public class ServicioNotificaciones
{
    // HU11 - CLAVES DE CONFIGURACIÓN
    // DEBEN COINCIDIR CON PERFILVM
    private const string ClaveNotificaciones =
        "HU11_NotificacionesActivas";

    private const string ClaveHoraInicio =
        "HU11_HoraInicio";

    private const string ClaveHoraFin =
        "HU11_HoraFin";

    // HU11 - CONSULTAR SI LAS NOTIFICACIONES
    // ESTÁN ACTIVADAS EN LAS PREFERENCIAS
    public bool NotificacionesActivas()
    {
        return Preferences.Default.Get(
            ClaveNotificaciones,
            true);
    }

    // HU11 - CALCULAR UNA HORA PERMITIDA
    private DateTime? CalcularHoraPermitida(
        DateTime fechaAviso,
        DateTime fechaLimite)
    {
        int horaInicio = Preferences.Default.Get(
            ClaveHoraInicio,
            7);

        int horaFin = Preferences.Default.Get(
            ClaveHoraFin,
            22);

        // PROTEGERNOS ANTE DATOS NO VÁLIDOS
        if (horaInicio < 0 ||
            horaInicio > 23 ||
            horaFin < 0 ||
            horaFin > 23 ||
            horaInicio >= horaFin)
        {
            horaInicio = 7;
            horaFin = 22;
        }

        TimeSpan inicio =
            TimeSpan.FromHours(horaInicio);

        TimeSpan fin =
            TimeSpan.FromHours(horaFin);

        DateTime horaAjustada = fechaAviso;

        // SI ES ANTES DEL HORARIO, MOVER
        // AL INICIO DEL HORARIO DEL MISMO DÍA
        if (fechaAviso.TimeOfDay < inicio)
        {
            horaAjustada = fechaAviso.Date + inicio;
        }
        // SI ES DESPUÉS DEL HORARIO, MOVER
        // AL INICIO DEL DÍA SIGUIENTE
        else if (fechaAviso.TimeOfDay >= fin)
        {
            horaAjustada =
                fechaAviso.Date.AddDays(1) + inicio;
        }

        // NO PROGRAMAR RECORDATORIOS PASADOS
        if (horaAjustada <= DateTime.Now)
            return null;

        // EL AVISO DEBE LLEGAR ANTES
        // DE QUE VENZA LA TAREA
        if (horaAjustada >= fechaLimite)
            return null;

        return horaAjustada;
    }

    // SOLICITAR PERMISO DE NOTIFICACIONES
    public async Task<bool> SolicitarPermiso()
    {
#if ANDROID
        if (await LocalNotificationCenter.Current
            .AreNotificationsEnabled())
        {
            return true;
        }

        return await LocalNotificationCenter.Current
            .RequestNotificationPermission();
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    // COMPROBAR PERMISO DE ALARMAS EXACTAS
    private bool TienePermisoAlarmasExactas()
    {
#if ANDROID
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
            return true;

        var contexto = Android.App.Application.Context;

        var alarmas = contexto.GetSystemService(
            Context.AlarmService) as AlarmManager;

        return alarmas?.CanScheduleExactAlarms() == true;
#else
        return false;
#endif
    }

    // ABRIR CONFIGURACIÓN DE ALARMAS EXACTAS
    private void AbrirConfiguracionAlarmas()
    {
#if ANDROID
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
            return;

        var contexto = Android.App.Application.Context;

        var intent = new Intent(
            Android.Provider.Settings.ActionRequestScheduleExactAlarm);

        intent.SetData(
            Android.Net.Uri.Parse(
                $"package:{contexto.PackageName}"));

        intent.AddFlags(ActivityFlags.NewTask);

        contexto.StartActivity(intent);
#endif
    }

    // PROGRAMAR RECORDATORIO DE UNA TAREA
    public async Task<bool> ProgramarRecordatorio(
        int tareaId,
        string titulo,
        DateTime fechaLimite,
        int minutosAviso,
        string nombreCurso = "")
    {
#if ANDROID
        if (tareaId <= 0 || minutosAviso < 0)
            return false;

        // CALCULAR HORA ORIGINAL DEL RECORDATORIO
        DateTime fechaAviso =
            fechaLimite.AddMinutes(-minutosAviso);

        // HU11 - SI LA TAREA YA TENÍA UN AVISO,
        // CANCELARLO ANTES DE APLICAR NUEVOS AJUSTES
        LocalNotificationCenter.Current.Cancel(tareaId);

        // HU11 - RESPETAR NOTIFICACIONES DESACTIVADAS
        if (!NotificacionesActivas())
        {
            System.Diagnostics.Debug.WriteLine(
                "HU11 - Las notificaciones están desactivadas.");

            return false;
        }

        // HU11 - RESPETAR HORARIO PERMITIDO
        DateTime? fechaPermitida =
            CalcularHoraPermitida(
                fechaAviso,
                fechaLimite);

        if (fechaPermitida == null)
        {
            System.Diagnostics.Debug.WriteLine(
                "HU11 - No hay una hora permitida " +
                "antes del vencimiento.");

            return false;
        }

        fechaAviso = fechaPermitida.Value;

        // COMPROBAR PERMISOS DE ANDROID
        if (!await SolicitarPermiso())
            return false;

        if (!TienePermisoAlarmasExactas())
        {
            try
            {
                AbrirConfiguracionAlarmas();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"HU05 - Error de permiso: {ex.Message}");
            }

            return false;
        }

        // PREPARAR DESCRIPCIÓN
        string descripcion =
            $"¡Tienes una tarea por entregar! {titulo}";

        if (!string.IsNullOrWhiteSpace(nombreCurso))
        {
            descripcion +=
                $"\nCurso: {nombreCurso.Trim()}";
        }

        descripcion +=
            $"\nVence el {fechaLimite:dd/MM/yyyy} " +
            $"a las {fechaLimite:hh:mm tt}.";

        // CONSERVAR ZONA HORARIA LOCAL
        var horaAviso = new DateTimeOffset(
            DateTime.SpecifyKind(
                fechaAviso,
                DateTimeKind.Local));

        // CREAR NOTIFICACIÓN
        var notificacion = new NotificationRequest
        {
            NotificationId = tareaId,

            Title = "Study Space",

            Description = descripcion,

            ReturningData = tareaId.ToString(),

            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = horaAviso,

                Android =
                {
                    ScheduleMode =
                        AndroidScheduleMode.ExactAllowWhileIdle
                }
            }
        };

        bool programada =
            await LocalNotificationCenter.Current
                .Show(notificacion);

        System.Diagnostics.Debug.WriteLine(
            $"HU11 - ID: {tareaId}");

        System.Diagnostics.Debug.WriteLine(
            $"HU11 - Curso: {nombreCurso}");

        System.Diagnostics.Debug.WriteLine(
            $"HU11 - Hora original: " +
            $"{fechaLimite.AddMinutes(-minutosAviso)}");

        System.Diagnostics.Debug.WriteLine(
            $"HU11 - Hora programada: {horaAviso}");

        System.Diagnostics.Debug.WriteLine(
            $"HU11 - Programada: {programada}");

        return programada;
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    // CANCELAR RECORDATORIO
    public void CancelarRecordatorio(int tareaId)
    {
#if ANDROID
        if (tareaId > 0)
        {
            LocalNotificationCenter.Current
                .Cancel(tareaId);
        }
#endif
    }
}
