using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
namespace StudySpaceAPI.Servicios;

public class ServicioCorreo
{
    private readonly IConfiguration _configuracion;
    private readonly ILogger<ServicioCorreo> _logger;

    public ServicioCorreo(
        IConfiguration configuracion,
        ILogger<ServicioCorreo> logger)
    {
        _configuracion = configuracion;
        _logger = logger;
    }

    // ENVIAR UN CORREO MEDIANTE SMTP SEGURO
    public async Task EnviarCorreoAsync(
        string destinatario,
        string asunto,
        string contenido)
    {
        // OBTENER CONFIGURACIÓN DESDE USER SECRETS
        string servidor =
            _configuracion["CorreoSMTP:Servidor"]
            ?? throw new InvalidOperationException(
                "Falta configurar el servidor SMTP.");

        int puerto =
            _configuracion.GetValue<int>(
                "CorreoSMTP:Puerto");

        string usuario =
            _configuracion["CorreoSMTP:Usuario"]
            ?? throw new InvalidOperationException(
                "Falta configurar el usuario SMTP.");

        string password =
            _configuracion["CorreoSMTP:Password"]
            ?? throw new InvalidOperationException(
                "Falta configurar la contraseña SMTP.");

        string remitente =
            _configuracion["CorreoSMTP:Remitente"]
            ?? usuario;

        string nombreRemitente =
            _configuracion["CorreoSMTP:NombreRemitente"]
            ?? "Study Space";

        // VALIDAR DESTINATARIO
        var correoDestino =
            MailboxAddress.Parse(destinatario);

        // PREPARAR MENSAJE
        var mensaje = new MimeMessage();

        mensaje.From.Add(
            new MailboxAddress(
                nombreRemitente,
                remitente));

        mensaje.To.Add(correoDestino);

        mensaje.Subject = asunto;

        mensaje.Body = new TextPart("plain")
        {
            Text = contenido
        };

        // CONECTAR CON SMTP UTILIZANDO SSL/TLS
        using var cliente = new SmtpClient();

        try
        {
            await cliente.ConnectAsync(
                servidor,
                puerto,
                SecureSocketOptions.SslOnConnect);

            await cliente.AuthenticateAsync(
                usuario,
                password);

            await cliente.SendAsync(mensaje);

            await cliente.DisconnectAsync(true);

            _logger.LogInformation(
                "Correo enviado correctamente mediante SMTP.");
        }
        catch (Exception ex)
        {
            // NO REGISTRAR CONTRASEÑAS NI CONTENIDO
            // DE CÓDIGOS DE RECUPERACIÓN
            _logger.LogError(
                "No se pudo enviar el correo SMTP: {TipoError}",
                ex.GetType().Name);

            throw new InvalidOperationException(
                "No se pudo enviar el correo. " +
                "Comprueba la configuración SMTP " +
                "y la conexión con el servidor.",
                ex);
        }
    }
}
