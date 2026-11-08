
using Aplicacion.CasosDeUso;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySpace.Servicios;

namespace StudySpace.VistaModelos;

public partial class RegistroVM : ObservableObject
{
    private readonly ServicioAutenticacion _auth;
    private readonly ServicioRegistroPendiente _registroPendiente;
    private readonly ServicioVerificacionCorreo _verificacionCorreo;

    [ObservableProperty]
    private string nombre = "";

    [ObservableProperty]
    private string correo = "";

    [ObservableProperty]
    private string password = "";

    [ObservableProperty]
    private string confirmar = "";

    [ObservableProperty]
    private bool ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string mensajeError = "";

    public bool TieneError =>
        !string.IsNullOrWhiteSpace(MensajeError);

    public RegistroVM(
        ServicioAutenticacion auth,
        ServicioRegistroPendiente registroPendiente,
        ServicioVerificacionCorreo verificacionCorreo)
    {
        _auth = auth;
        _registroPendiente = registroPendiente;
        _verificacionCorreo = verificacionCorreo;
    }

    // =====================================================
    // CREAR CUENTA CON LA API PHP DE WOPRES
    // =====================================================
    //
    // 1. Validar los datos del formulario.
    // 2. Solicitar el registro al servidor PHP.
    // 3. El servidor guarda el registro pendiente
    //    y envia el codigo de verificacion.
    // 4. Abrir la pantalla de verificacion.
    //
    // NO se crea una cuenta local en SQLite.
    // La cuenta se activara en MySQL al verificar
    // correctamente el codigo recibido.
    // =====================================================

    [RelayCommand]
    private async Task CrearCuenta()
    {
        if (Ocupado)
            return;

        MensajeError = "";
        Ocupado = true;

        try
        {
            // 1. VALIDAR EL FORMULARIO SIN CREAR USUARIOS
            var (valido, mensaje) =
                await _auth.ValidarRegistro(
                    Nombre,
                    Correo,
                    Password,
                    Confirmar);

            if (!valido)
            {
                MensajeError = mensaje;
                return;
            }

            // 2. NORMALIZAR LOS DATOS
            string nombreNormalizado = Nombre.Trim();

            string correoNormalizado =
                Correo.Trim().ToLowerInvariant();

            // 3. INICIAR EL REGISTRO EN LA API PHP
            //
            // Se envia la contrasena original mediante HTTPS.
            // El servidor PHP genera su hash y almacena
            // el registro pendiente en MySQL.
            //
            // NO enviamos el hash BCrypt local.
            var (enviado, mensajeEnvio) =
                await _verificacionCorreo.IniciarRegistroAsync(
                    nombreNormalizado,
                    correoNormalizado,
                    Password);

            if (!enviado)
            {
                _registroPendiente.Limpiar();

                MensajeError = mensajeEnvio;
                return;
            }

            // 4. MANTENER TEMPORALMENTE LOS DATOS
            // NECESARIOS PARA LA PANTALLA DE VERIFICACION
            //
            // ServicioRegistroPendiente guarda un hash
            // en memoria para conservar la compatibilidad
            // con VerificarCorreoVM.
            //
            // La cuenta real ya esta pendiente en MySQL.
            _registroPendiente.Preparar(
                nombreNormalizado,
                correoNormalizado,
                Password);

            // 5. LIMPIAR LAS CONTRASENAS DEL FORMULARIO
            Password = "";
            Confirmar = "";

            // 6. ABRIR AUTOMATICAMENTE LA VERIFICACION
            await Shell.Current.GoToAsync(
                "verificarcorreo");
        }
        catch (Exception)
        {
            _registroPendiente.Limpiar();

            MensajeError =
                "No se pudo iniciar el registro. " +
                "Comprueba tu conexion a internet " +
                "e intentalo nuevamente.";
        }
        finally
        {
            Ocupado = false;
        }
    }

    // =====================================================
    // NAVEGACION
    // =====================================================

    // VOLVER
    [RelayCommand]
    private async Task Volver()
    {
        if (Ocupado)
            return;

        await Shell.Current.GoToAsync("..");
    }

    // IR AL INICIO DE SESION
    [RelayCommand]
    private async Task IrAInicioSesion()
    {
        if (Ocupado)
            return;

        await Shell.Current.GoToAsync("../login");
    }
}

