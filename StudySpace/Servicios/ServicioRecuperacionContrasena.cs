using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace StudySpace.Servicios;

/// <summary>
/// Recuperacion de contrasena mediante la API PHP de Wopres.
/// No guarda codigos ni contrasenas en el dispositivo.
/// </summary>
public sealed class ServicioRecuperacionContrasena
{
    private readonly HttpClient _clientePhp;

    public ServicioRecuperacionContrasena(
        [FromKeyedServices("ApiPHP")] HttpClient clientePhp)
    {
        _clientePhp = clientePhp;
    }

    // PASO 1: Solicitar un codigo al correo.
    // El servidor siempre responde de forma generica para no
    // revelar si la direccion pertenece a una cuenta.
    public async Task<(bool Ok, string Mensaje)> SolicitarCodigoAsync(
        string correo)
    {
        correo = correo?.Trim().ToLowerInvariant() ?? "";

        if (!CorreoTieneFormatoBasico(correo))
        {
            return (false, "Ingresa un correo electronico valido.");
        }

        try
        {
            using var respuesta = await _clientePhp.PostAsJsonAsync(
                "recuperacion-iniciar.php",
                new { correo });

            var contenido = await LeerRespuestaAsync(respuesta);

            if (!respuesta.IsSuccessStatusCode)
            {
                return (
                    false,
                    contenido?.Mensaje ??
                    "No se pudo solicitar el codigo. Intentalo mas tarde.");
            }

            if (contenido?.Estado != "solicitud_recibida")
            {
                return (false, "El servidor devolvio una respuesta inesperada.");
            }

            return (
                true,
                contenido.Mensaje ??
                "Si existe una cuenta con ese correo, recibiras un codigo.");
        }
        catch (TaskCanceledException)
        {
            return (false, "Se agoto el tiempo de espera. Comprueba tu conexion.");
        }
        catch (HttpRequestException)
        {
            return (false, "No se pudo conectar con el servidor de Study Space.");
        }
        catch (JsonException)
        {
            return (false, "El servidor devolvio una respuesta no valida.");
        }
    }

    // PASO 2: Comprobar el codigo y establecer una contrasena nueva.
    // El codigo y la contrasena se envian solo por HTTPS;
    // nunca se almacenan en Preferences ni en SQLite.
    public async Task<(bool Ok, string Mensaje)> RestablecerContrasenaAsync(
        string correo,
        string codigo,
        string nuevaContrasena)
    {
        correo = correo?.Trim().ToLowerInvariant() ?? "";
        codigo = codigo?.Trim() ?? "";
        nuevaContrasena ??= "";

        if (!CorreoTieneFormatoBasico(correo))
        {
            return (false, "Ingresa un correo electronico valido.");
        }

        if (codigo.Length != 6 ||
            !codigo.All(c => c >= '0' && c <= '9'))
        {
            return (false, "El codigo debe tener exactamente seis numeros.");
        }

        int bytes = Encoding.UTF8.GetByteCount(nuevaContrasena);

        if (bytes < 8 || bytes > 72)
        {
            return (false, "La contrasena debe tener entre 8 y 72 bytes.");
        }

        try
        {
            using var respuesta = await _clientePhp.PostAsJsonAsync(
                "recuperacion-restablecer.php",
                new
                {
                    correo,
                    codigo,
                    nueva_contrasena = nuevaContrasena
                });

            var contenido = await LeerRespuestaAsync(respuesta);

            if (!respuesta.IsSuccessStatusCode)
            {
                return (
                    false,
                    contenido?.Mensaje ??
                    "No se pudo cambiar la contrasena. Revisa el codigo.");
            }

            if (contenido?.Estado != "contrasena_actualizada")
            {
                return (false, "El servidor no confirmo el cambio de contrasena.");
            }

            return (
                true,
                contenido.Mensaje ??
                "Contrasena actualizada. Inicia sesion nuevamente.");
        }
        catch (TaskCanceledException)
        {
            return (false, "Se agoto el tiempo de espera. Comprueba tu conexion.");
        }
        catch (HttpRequestException)
        {
            return (false, "No se pudo conectar con el servidor de Study Space.");
        }
        catch (JsonException)
        {
            return (false, "El servidor devolvio una respuesta no valida.");
        }
    }

    private static bool CorreoTieneFormatoBasico(string correo)
    {
        int arroba = correo.IndexOf('@');

        return correo.Length > 3 &&
               correo.Length <= 254 &&
               arroba > 0 &&
               arroba < correo.Length - 1 &&
               correo.LastIndexOf('@') == arroba &&
               !correo.Any(char.IsWhiteSpace);
    }

    private static Task<RespuestaApi?> LeerRespuestaAsync(
        HttpResponseMessage respuesta)
    {
        return respuesta.Content.ReadFromJsonAsync<RespuestaApi>();
    }

    private sealed class RespuestaApi
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("mensaje")]
        public string? Mensaje { get; set; }
    }
}
