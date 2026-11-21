using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace StudySpace.Vistas;

/// <summary>
/// HU17: preguntas frecuentes y redaccion de reportes para el correo oficial.
/// El usuario revisa y envia el mensaje desde su aplicacion de correo.
/// </summary>
public partial class PaginaAyudaSoporte : ContentPage
{
    private const string CorreoSoporte = "studyspace.soporte@wopres.com";
    private bool _abriendoCorreo;

    public PaginaAyudaSoporte()
    {
        InitializeComponent();
    }

    private async void OnVolverClicked(object? sender, EventArgs e)
    {
        if (_abriendoCorreo)
            return;

        await Shell.Current.GoToAsync("..");
    }

    private string? PrepararComentario()
    {
        string detalle = DescripcionEditor.Text?.Trim() ?? string.Empty;
        if (detalle.Length < 10)
        {
            EstadoLabel.Text = "Escribe al menos 10 caracteres para explicar tu consulta o comentario.";
            EstadoLabel.IsVisible = true;
            return null;
        }

        string tipo = TipoPicker.SelectedItem?.ToString() ?? "Consulta";
        string version = AppInfo.Current.VersionString;
        EstadoLabel.IsVisible = false;

        // Solo se agrega el contenido que el usuario escribio y la version publica.
        // No se incluyen credenciales, identificadores, archivos ni bases de datos.
        return $"STUDY SPACE - {tipo}\n" +
               $"Versión de la aplicación: {version}\n\n" +
               $"Descripción:\n{detalle}\n\n" +
               "Mensaje redactado por el usuario desde Study Space.";
    }

    private string ObtenerAsunto()
    {
        return TipoPicker.SelectedIndex switch
        {
            0 => "Study Space - Reporte de problema",
            1 => "Study Space - Sugerencia",
            _ => "Study Space - Consulta"
        };
    }

    private async void OnEnviarCorreoClicked(object? sender, EventArgs e)
    {
        if (_abriendoCorreo)
            return;

        string? texto = PrepararComentario();
        if (texto is null)
            return;

        _abriendoCorreo = true;
        EnviarCorreoButton.IsEnabled = false;
        try
        {
            await Email.Default.ComposeAsync(new EmailMessage
            {
                To = new List<string> { CorreoSoporte },
                Subject = ObtenerAsunto(),
                Body = texto,
                BodyFormat = EmailBodyFormat.PlainText
            });
            // ComposeAsync solo abre la aplicacion de correo; NO confirma el envio.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU17 - Correo: {ex.GetType().Name}");
            await DisplayAlert("No se pudo abrir el correo",
                $"No encontramos una aplicación de correo disponible. Puedes copiar tu comentario y enviarlo manualmente a {CorreoSoporte}.",
                "Entendido");
        }
        finally
        {
            _abriendoCorreo = false;
            EnviarCorreoButton.IsEnabled = true;
        }
    }

    private async void OnCopiarClicked(object? sender, EventArgs e)
    {
        string? texto = PrepararComentario();
        if (texto is null)
            return;

        try
        {
            await Clipboard.Default.SetTextAsync(texto);
            await DisplayAlert("Comentario copiado",
                $"Pega el texto en un correo dirigido a {CorreoSoporte}. Study Space no ha enviado ningún mensaje.",
                "Aceptar");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU17 - Portapapeles: {ex.GetType().Name}");
            await DisplayAlert("No se pudo copiar",
                "No fue posible acceder al portapapeles en este momento.",
                "Entendido");
        }
    }
}
