using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySpace.Servicios;

namespace StudySpace.VistaModelos;

public partial class VerificarCorreoVM : ObservableObject
{
    private readonly ServicioVerificacionCorreo _verificacion;
    private readonly ServicioRegistroPendiente _registroPendiente;

    // DATOS DE LA PANTALLA
    [ObservableProperty]
    private string correo = "";

    [ObservableProperty]
    private string codigo = "";

    // ESTADO
    [ObservableProperty]
    private bool ocupado;

    // MENSAJES DE ERROR
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string mensajeError = "";

    public bool TieneError =>
        !string.IsNullOrWhiteSpace(MensajeError);

    // MENSAJES INFORMATIVOS
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeInfo))]
    private string mensajeInfo = "";

    public bool TieneMensajeInfo =>
        !string.IsNullOrWhiteSpace(MensajeInfo);

    public VerificarCorreoVM(
        ServicioVerificacionCorreo verificacion,
        ServicioRegistroPendiente registroPendiente)
    {
        _verificacion = verificacion;
        _registroPendiente = registroPendiente;

        // EL PRIMER CODIGO YA FUE SOLICITADO EN RegistroVM.
        // NO SOLICITAMOS OTRO AL ABRIR ESTA PANTALLA.
        var pendiente = _registroPendiente.Obtener();

        if (pendiente != null)
        {
            Correo = pendiente.Correo;

            MensajeInfo =
                "Te enviamos un código a tu correo. " +
                "Revisa tu bandeja de entrada.";
        }
        else
        {
            MensajeError =
                "No hay un registro pendiente. " +
                "Regresa a Crear cuenta para comenzar.";
        }
    }

    private void LimpiarMensajes()
    {
        MensajeError = "";
        MensajeInfo = "";
    }

    // EL CORREO NO PUEDE CAMBIARSE DURANTE LA VERIFICACION.
    private string? ObtenerCorreoRegistro()
    {
        var pendiente = _registroPendiente.Obtener();

        if (pendiente == null)
        {
            MensajeError =
                "No se encontró tu registro pendiente. " +
                "Regresa a Crear cuenta para comenzar nuevamente.";

            return null;
        }

        Correo = pendiente.Correo;
        return pendiente.Correo;
    }

    // =====================================================
    // REENVIAR CODIGO CON LA API PHP DE WOPRES
    // =====================================================
    // Solo enviamos el correo del registro pendiente.
    // No pedimos ni enviamos la contrasena.
    //
    // La respuesta de la API es deliberadamente generica:
    // no confirma si realmente se envio un nuevo codigo.
    // El servidor aplica sus limites y tiempo de espera.
    // =====================================================

    [RelayCommand]
    private async Task EnviarCodigo()
    {
        if (Ocupado)
            return;

        LimpiarMensajes();

        string? correoRegistro = ObtenerCorreoRegistro();

        if (correoRegistro == null)
            return;

        Ocupado = true;

        try
        {
            var (ok, mensaje) =
                await _verificacion.ReenviarCodigoAsync(
                    correoRegistro);

            if (!ok)
            {
                MensajeError = mensaje;
                return;
            }

            // NO borrar Codigo: el servidor puede haber
            // rechazado silenciosamente el reenvio por
            // tiempo de espera o limite de solicitudes.
            // Tampoco afirmar que el correo se envio.
            MensajeInfo = mensaje;
        }
        catch (Exception)
        {
            MensajeError =
                "No se pudo solicitar otro código. " +
                "Comprueba tu conexión e inténtalo nuevamente.";
        }
        finally
        {
            Ocupado = false;
        }
    }

    // =====================================================
    // VERIFICAR Y ACTIVAR LA CUENTA EN MYSQL
    // =====================================================
    // La API PHP comprueba el codigo y crea la cuenta
    // verificada. La aplicacion no registra usuarios
    // en SQLite durante este proceso.
    // =====================================================

    [RelayCommand]
    private async Task VerificarCodigo()
    {
        if (Ocupado)
            return;

        LimpiarMensajes();

        string? correoRegistro = ObtenerCorreoRegistro();

        if (correoRegistro == null)
            return;

        string codigoIngresado = Codigo?.Trim() ?? "";

        if (codigoIngresado.Length != 6 ||
            codigoIngresado.Any(c => c < '0' || c > '9'))
        {
            MensajeError =
                "El código debe contener exactamente 6 números.";
            return;
        }

        Ocupado = true;
        bool cuentaActivada = false;

        try
        {
            var (ok, mensaje) =
                await _verificacion.ActivarRegistroAsync(
                    correoRegistro,
                    codigoIngresado);

            if (!ok)
            {
                MensajeError = mensaje;
                return;
            }

            cuentaActivada = true;

            // La cuenta ya existe en MySQL.
            // Eliminar solo el estado temporal local.
            _registroPendiente.Limpiar();
            Codigo = "";

            MensajeInfo =
                "Tu correo fue verificado y tu cuenta " +
                "se creó correctamente.";

            await Shell.Current.DisplayAlert(
                "¡Cuenta verificada!",
                "Tu cuenta de Study Space fue creada " +
                "correctamente.\n\n" +
                "Ahora podrás iniciar sesión con " +
                "tu correo y contraseña.",
                "Aceptar");

            await Shell.Current.GoToAsync("../login");
        }
        catch (Exception)
        {
            if (cuentaActivada)
            {
                MensajeInfo =
                    "Tu cuenta ya está activada. " +
                    "Regresa a Inicio de sesión.";
            }
            else
            {
                MensajeError =
                    "No se pudo comprobar el código. " +
                    "Revisa tu conexión a internet " +
                    "e inténtalo nuevamente.";
            }
        }
        finally
        {
            Ocupado = false;
        }
    }

    // VOLVER AL REGISTRO
    [RelayCommand]
    private async Task Volver()
    {
        if (Ocupado)
            return;

        await Shell.Current.GoToAsync("..");
    }
}

