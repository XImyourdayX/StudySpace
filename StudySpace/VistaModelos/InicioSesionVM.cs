
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aplicacion.CasosDeUso;

namespace StudySpace.VistaModelos;

public partial class InicioSesionVM : ObservableObject
{
    private readonly ServicioAutenticacion _auth;

    [ObservableProperty]
    private string correo = "";

    [ObservableProperty]
    private string password = "";

    [ObservableProperty]
    private bool ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string mensajeError = "";

    public bool TieneError =>
        !string.IsNullOrEmpty(MensajeError);

    public InicioSesionVM(ServicioAutenticacion auth)
    {
        _auth = auth;
    }

    // INICIAR SESIÓN
    [RelayCommand]
    private async Task IniciarSesion()
    {
        if (Ocupado)
            return;

        MensajeError = "";
        Ocupado = true;

        try
        {
            var (ok, mensaje) =
                await _auth.IniciarSesion(
                    Correo,
                    Password);

            if (!ok)
            {
                MensajeError = mensaje;
                return;
            }

            // IR A LA PANTALLA PRINCIPAL
            await Shell.Current.GoToAsync("//principal");
        }
        catch (Exception ex)
        {
            MensajeError =
                $"No se pudo iniciar sesión: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    // RECUPERACIÓN DE CONTRASEÑA
    [RelayCommand]
    private async Task RecuperarPassword()
    {
        if (Ocupado)
            return;

        MensajeError = "";

        await Shell.Current.DisplayAlert(
            "Recuperar contraseña",
            "Para recuperar tu cuenta necesitaremos " +
            "verificar tu identidad mediante tu " +
            "correo electrónico.\n\n" +
            "Estamos preparando este proceso " +
            "para que puedas establecer una nueva " +
            "contraseña de forma segura.",
            "Entendido");
    }

    // IR A REGISTRO
    [RelayCommand]
    private async Task IrARegistro()
    {
        await Shell.Current.GoToAsync("registro");
    }

    // VOLVER
    [RelayCommand]
    private async Task Volver()
    {
        await Shell.Current.GoToAsync("..");
    }
}
