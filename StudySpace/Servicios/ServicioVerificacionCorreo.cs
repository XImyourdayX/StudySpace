using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace StudySpace.Servicios;

public class ServicioVerificacionCorreo
{
    // API PHP DE WOPRES: REGISTRO, ACTIVACION Y REENVIO.
    private readonly HttpClient _clientePhp;

    public ServicioVerificacionCorreo(
        [FromKeyedServices("ApiPHP")] HttpClient clientePhp)
    {
        _clientePhp = clientePhp;
    }

    // =====================================================
    // REGISTRO CON LA API PHP DE WOPRES
    // =====================================================

    // PASO 1: INICIAR REGISTRO Y SOLICITAR EL PRIMER CODIGO.
    // El servidor guarda el registro pendiente en MySQL.
    public async Task<(bool Ok, string Mensaje)> IniciarRegistroAsync(
        string nombre,
        string correo,
        string contrasena)
    {
        nombre = nombre?.Trim() ?? string.Empty;
        correo = correo?.Trim() ?? string.Empty;

        if (nombre.Length < 2 || nombre.Length > 120)
        {
            return (
                false,
                "El nombre debe tener entre 2 y 120 caracteres."
            );
        }

        if (string.IsNullOrWhiteSpace(correo))
        {
            return (
                false,
                "Ingresa tu correo electrónico."
            );
        }

        if (string.IsNullOrWhiteSpace(contrasena))
        {
            return (
                false,
                "Ingresa una contraseña."
            );
        }

        int longitudContrasena =
            Encoding.UTF8.GetByteCount(contrasena);

        if (longitudContrasena < 8 ||
            longitudContrasena > 72)
        {
            return (
                false,
                "La contraseña debe tener entre " +
                "8 y 72 bytes."
            );
        }

        try
        {
            var datos = new
            {
                nombre,
                correo,
                contrasena
            };

            using var respuesta = await _clientePhp.PostAsJsonAsync(
                "registro-iniciar.php",
                datos);

            var contenido = await LeerRespuestaAsync(respuesta);

            if (!respuesta.IsSuccessStatusCode ||
                contenido?.Estado != "codigo_enviado")
            {
                return (
                    false,
                    contenido?.Mensaje ??
                    "No se pudo iniciar el registro."
                );
            }

            return (
                true,
                contenido.Mensaje ??
                "Enviamos un código a tu correo electrónico."
            );
        }
        catch (TaskCanceledException)
        {
            return (
                false,
                "Se agotó el tiempo de espera. " +
                "Comprueba tu conexión a internet."
            );
        }
        catch (HttpRequestException)
        {
            return (
                false,
                "No se pudo conectar con el servidor " +
                "de Study Space."
            );
        }
        catch (JsonException)
        {
            return (
                false,
                "El servidor devolvió una respuesta no válida."
            );
        }
    }

    // PASO 2: VERIFICAR CODIGO Y ACTIVAR CUENTA EN MYSQL.
    public async Task<(bool Ok, string Mensaje)> ActivarRegistroAsync(
        string correo,
        string codigo)
    {
        correo = correo?.Trim() ?? string.Empty;
        codigo = codigo?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(correo))
        {
            return (
                false,
                "No se encontró el correo del registro."
            );
        }

        if (codigo.Length != 6 ||
            !codigo.All(c => c >= '0' && c <= '9'))
        {
            return (
                false,
                "El código debe contener exactamente " +
                "seis números."
            );
        }

        try
        {
            var datos = new
            {
                correo,
                codigo
            };

            using var respuesta = await _clientePhp.PostAsJsonAsync(
                "registro-verificar.php",
                datos);

            var contenido = await LeerRespuestaAsync(respuesta);

            if (!respuesta.IsSuccessStatusCode ||
                contenido?.Estado != "cuenta_activada")
            {
                return (
                    false,
                    contenido?.Mensaje ??
                    "No se pudo verificar el código."
                );
            }

            return (
                true,
                contenido.Mensaje ??
                "Tu cuenta fue creada y verificada correctamente."
            );
        }
        catch (TaskCanceledException)
        {
            return (
                false,
                "Se agotó el tiempo de espera " +
                "al verificar el código."
            );
        }
        catch (HttpRequestException)
        {
            return (
                false,
                "No se pudo conectar con el servidor " +
                "de Study Space."
            );
        }
        catch (JsonException)
        {
            return (
                false,
                "El servidor devolvió una respuesta no válida."
            );
        }
    }

    // =====================================================
    // REENVIAR CODIGO CON LA API PHP DE WOPRES
    // =====================================================
    // No enviamos nombre ni contrasena.
    // El servidor aplica el tiempo de espera y los limites.
    // Por privacidad responde de forma generica, por lo que
    // "solicitud_recibida" NO garantiza que se envio un correo.
    public async Task<(bool Ok, string Mensaje)> ReenviarCodigoAsync(
        string correo)
    {
        correo = correo?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(correo))
        {
            return (
                false,
                "No se encontró el correo del registro."
            );
        }

        try
        {
            var datos = new { correo };

            using var respuesta = await _clientePhp.PostAsJsonAsync(
                "registro-reenviar.php",
                datos);

            var contenido = await LeerRespuestaAsync(respuesta);

            if (!respuesta.IsSuccessStatusCode ||
                contenido?.Estado != "solicitud_recibida")
            {
                return (
                    false,
                    contenido?.Mensaje ??
                    "No se pudo procesar la solicitud de reenvío."
                );
            }

            return (
                true,
                contenido.Mensaje ??
                "Solicitud recibida. Si tienes un registro " +
                "pendiente y se permite el reenvío, " +
                "recibirás otro código por correo."
            );
        }
        catch (TaskCanceledException)
        {
            return (
                false,
                "Se agotó el tiempo de espera al solicitar " +
                "otro código."
            );
        }
        catch (HttpRequestException)
        {
            return (
                false,
                "No se pudo conectar con el servidor " +
                "de Study Space."
            );
        }
        catch (JsonException)
        {
            return (
                false,
                "El servidor devolvió una respuesta no válida."
            );
        }
    }

    // =====================================================
    // LECTURA DE RESPUESTAS JSON
    // =====================================================

    private static async Task<RespuestaApi?> LeerRespuestaAsync(
        HttpResponseMessage respuesta)
    {
        return await respuesta.Content.ReadFromJsonAsync<RespuestaApi>();
    }

    private sealed class RespuestaApi
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("mensaje")]
        public string? Mensaje { get; set; }

    }
}