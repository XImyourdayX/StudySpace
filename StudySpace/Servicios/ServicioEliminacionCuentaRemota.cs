using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aplicacion.CasosDeUso;
using Infraestructura.DBContext;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace StudySpace.Servicios;

/// <summary>
/// HU17: elimina EXCLUSIVAMENTE la cuenta MySQL autenticada.
/// Antes del POST verifica que la identidad remota del token corresponda
/// al perfil SQLite activo. El PHP vuelve a comprobar token y contraseña.
/// No modifica los datos locales; esa limpieza se ejecuta tras confirmación.
/// </summary>
public sealed class ServicioEliminacionCuentaRemota
{
    private const string ClaveToken = "studyspace_api_php_token";
    private readonly HttpClient _cliente;
    private readonly ContextoBaseDatos _contexto;
    private readonly ServicioAutenticacion _auth;

    public sealed record Resultado(bool Confirmada, string Mensaje);

    public ServicioEliminacionCuentaRemota(
        [FromKeyedServices("ApiPHP")] HttpClient cliente,
        ContextoBaseDatos contexto,
        ServicioAutenticacion auth)
    {
        _cliente = cliente;
        _contexto = contexto;
        _auth = auth;
    }

    public async Task<Resultado> SolicitarAsync(int usuarioLocalId, string contrasena)
    {
        if (usuarioLocalId <= 0 || _auth.UsuarioActual?.Id != usuarioLocalId)
            return new(false, "No hay una sesión activa para esta cuenta.");

        if (string.IsNullOrEmpty(contrasena) ||
            System.Text.Encoding.UTF8.GetByteCount(contrasena) > 1024)
            return new(false, "Introduce una contraseña válida.");

        try
        {
            string? token = await SecureStorage.Default.GetAsync(ClaveToken);
            if (string.IsNullOrEmpty(token) || token.Length != 64 ||
                !token.All(Uri.IsHexDigit))
                return new(false, "Tu sesión no es válida. Inicia sesión nuevamente.");

            // La identidad se compara con el vínculo local ANTES de solicitar
            // el borrado. Nunca se transmite un ID de usuario al endpoint.
            var db = await _contexto.ObtenerDb();
            var vinculo = await db.Table<VinculoUsuarioRemoto>()
                .Where(v => v.UsuarioLocalId == usuarioLocalId)
                .FirstOrDefaultAsync();
            if (vinculo is null || vinculo.UsuarioRemotoId <= 0)
                return new(false, "No se encontró la vinculación de tu cuenta. No se eliminó nada.");

            using (var consulta = new HttpRequestMessage(HttpMethod.Get, "sesion-validar.php"))
            {
                consulta.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                using var verificacion = await _cliente.SendAsync(consulta);
                if (!verificacion.IsSuccessStatusCode)
                    return new(false, "No se pudo verificar tu sesión con el servidor. No se eliminó nada.");

                var sesion = await verificacion.Content.ReadFromJsonAsync<RespuestaSesion>();
                if (sesion?.Estado != "sesion_valida" || sesion.Usuario is null ||
                    sesion.Usuario.Id != vinculo.UsuarioRemotoId)
                    return new(false, "La cuenta activa no coincide con la sesión remota. No se eliminó nada.");
            }

            // Detectar que el perfil local no haya cambiado durante la espera.
            if (_auth.UsuarioActual?.Id != usuarioLocalId)
                return new(false, "La cuenta activa cambió. No se envió la eliminación.");

            using var solicitud = new HttpRequestMessage(HttpMethod.Post, "cuenta-eliminar.php");
            solicitud.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            solicitud.Content = JsonContent.Create(new
            {
                contrasena,
                confirmacion = "ELIMINAR MI CUENTA"
            });

            using var respuesta = await _cliente.SendAsync(solicitud);
            var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaApi>();
            if (respuesta.IsSuccessStatusCode && contenido?.Estado == "cuenta_eliminada")
                return new(true, "La cuenta fue eliminada del servidor.");

            return contenido?.Estado switch
            {
                "contrasena_incorrecta" when respuesta.StatusCode == HttpStatusCode.Forbidden =>
                    new(false, "Contraseña incorrecta. No se eliminó ningún dato."),
                "demasiados_intentos" when respuesta.StatusCode == HttpStatusCode.TooManyRequests =>
                    new(false, "Demasiados intentos. Espera 15 minutos antes de probar otra vez."),
                "sesion_invalida" when respuesta.StatusCode == HttpStatusCode.Unauthorized =>
                    new(false, "Tu sesión venció. Inicia sesión nuevamente."),
                _ => new(false, "El servidor no confirmó la eliminación. No se borraron datos locales. Si hubo un corte de conexión, comprueba el estado de tu cuenta antes de repetirlo.")
            };
        }
        catch (HttpRequestException)
        {
            return new(false, "No hay conexión con el servidor. No se borraron datos locales. Si el envío se interrumpió, comprueba tu cuenta antes de reintentarlo.");
        }
        catch (TaskCanceledException)
        {
            return new(false, "La solicitud tardó demasiado. No se borraron datos locales. Comprueba el estado de tu cuenta antes de reintentarlo.");
        }
        catch (JsonException)
        {
            return new(false, "No se pudo interpretar la respuesta. No se borraron datos locales. Comprueba el estado de tu cuenta antes de reintentarlo.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU17 - Cuenta remota: {ex.GetType().Name}");
            return new(false, "No se pudo completar la solicitud. No se borraron datos locales.");
        }
    }

    private sealed class RespuestaSesion
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }
        [JsonPropertyName("usuario")]
        public UsuarioApi? Usuario { get; set; }
    }

    private sealed class UsuarioApi
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    private sealed class RespuestaApi
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }
    }
}
