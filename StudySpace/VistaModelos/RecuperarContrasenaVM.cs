using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySpace.Servicios;

namespace StudySpace.VistaModelos;

public partial class RecuperarContrasenaVM : ObservableObject
{
    private readonly ServicioRecuperacionContrasena _recuperacion;

    // Correo al que se solicito el codigo. Se conserva solo en memoria
    // para impedir que se intente usar el codigo con otra direccion.
    private string? _correoDeSolicitud;

    [ObservableProperty]
    private string correo = "";

    [ObservableProperty]
    private string codigo = "";

    [ObservableProperty]
    private string nuevaContrasena = "";

    [ObservableProperty]
    private string confirmarContrasena = "";

    [ObservableProperty]
    private bool codigoSolicitado;

    [ObservableProperty]
    private bool ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string mensajeError = "";

    public bool TieneError =>
        !string.IsNullOrWhiteSpace(MensajeError);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeInfo))]
    private string mensajeInfo = "";

    public bool TieneMensajeInfo =>
        !string.IsNullOrWhiteSpace(MensajeInfo);

    public RecuperarContrasenaVM(
        ServicioRecuperacionContrasena recuperacion)
    {
        _recuperacion = recuperacion;
    }

    // Si se cambia de correo, ocultar los campos del codigo anterior.
    partial void OnCorreoChanged(string value)
    {
        if (_correoDeSolicitud == null ||
            string.Equals(
                _correoDeSolicitud,
                value.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _correoDeSolicitud = null;
        CodigoSolicitado = false;
        Codigo = "";
        NuevaContrasena = "";
        ConfirmarContrasena = "";
        MensajeInfo = "";
    }

    private void LimpiarMensajes()
    {
        MensajeError = "";
        MensajeInfo = "";
    }

    // PASO 1: solicitar el codigo al servidor de Wopres.
    [RelayCommand]
    private async Task SolicitarCodigo()
    {
        if (Ocupado)
            return;

        LimpiarMensajes();

        string correoNormalizado =
            Correo?.Trim().ToLowerInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(correoNormalizado))
        {
            MensajeError = "Ingresa tu correo electrónico.";
            return;
        }

        Ocupado = true;

        try
        {
            var (ok, mensaje) =
                await _recuperacion.SolicitarCodigoAsync(
                    correoNormalizado);

            if (!ok)
            {
                MensajeError = mensaje;
                return;
            }

            // La respuesta es generica: no confirma que exista una cuenta
            // ni garantiza que se haya enviado un correo.
            _correoDeSolicitud = correoNormalizado;
            CodigoSolicitado = true;
            Codigo = "";
            NuevaContrasena = "";
            ConfirmarContrasena = "";
            MensajeInfo = mensaje;
        }
        catch (Exception)
        {
            MensajeError =
                "No se pudo solicitar el código. Inténtalo nuevamente.";
        }
        finally
        {
            Ocupado = false;
        }
    }

    // PASO 2: verificar el codigo y cambiar la contrasena en MySQL.
    [RelayCommand]
    private async Task Restablecer()
    {
        if (Ocupado)
            return;

        LimpiarMensajes();

        if (!CodigoSolicitado ||
            string.IsNullOrWhiteSpace(_correoDeSolicitud))
        {
            MensajeError = "Primero solicita un código de recuperación.";
            return;
        }

        string codigoIngresado = Codigo?.Trim() ?? "";
        string contrasenaNueva = NuevaContrasena ?? "";
        string confirmacion = ConfirmarContrasena ?? "";

        if (codigoIngresado.Length != 6 ||
            codigoIngresado.Any(c => c < '0' || c > '9'))
        {
            MensajeError = "El código debe contener exactamente 6 números.";
            return;
        }

        int longitudBytes = Encoding.UTF8.GetByteCount(contrasenaNueva);

        if (longitudBytes < 8 || longitudBytes > 72)
        {
            MensajeError =
                "La contraseña debe tener entre 8 y 72 bytes.";
            return;
        }

        if (contrasenaNueva != confirmacion)
        {
            MensajeError = "Las contraseñas no coinciden.";
            return;
        }

        Ocupado = true;
        bool cambioConfirmado = false;

        try
        {
            var (ok, mensaje) =
                await _recuperacion.RestablecerContrasenaAsync(
                    _correoDeSolicitud,
                    codigoIngresado,
                    contrasenaNueva);

            if (!ok)
            {
                MensajeError = mensaje;
                return;
            }

            cambioConfirmado = true;

            // No conservar los datos sensibles en el formulario.
            Codigo = "";
            NuevaContrasena = "";
            ConfirmarContrasena = "";
            CodigoSolicitado = false;
            _correoDeSolicitud = null;

            MensajeInfo = mensaje;

            await Shell.Current.DisplayAlert(
                "Contraseña actualizada",
                "Tu contraseña se cambió correctamente. " +
                "Inicia sesión con la nueva contraseña.",
                "Aceptar");

            // La pantalla se abrirá desde la ruta de inicio de sesión.
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception)
        {
            MensajeError = cambioConfirmado
                ? "Tu contraseña ya fue actualizada. " +
                  "Regresa al inicio de sesión."
                : "No se pudo completar la operación. " +
                  "Comprueba tu conexión e inténtalo nuevamente.";
        }
        finally
        {
            Ocupado = false;
        }
    }

    // VOLVER A INICIO DE SESION.
    [RelayCommand]
    private async Task Volver()
    {
        if (Ocupado)
            return;

        Codigo = "";
        NuevaContrasena = "";
        ConfirmarContrasena = "";

        await Shell.Current.GoToAsync("..");
    }
}