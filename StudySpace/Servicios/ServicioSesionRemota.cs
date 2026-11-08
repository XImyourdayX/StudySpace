
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aplicacion.CasosDeUso;
using Infraestructura.Repositorios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace StudySpace.Servicios;

public class ServicioSesionRemota
{
    private readonly HttpClient _clientePhp;
    private readonly ServicioAutenticacion _auth;
    private readonly RepositorioVinculosUsuario _vinculos;

    // MISMA CLAVE QUE UTILIZA InicioSesionVM
    private const string ClaveToken =
        "studyspace_api_php_token";

    public ServicioSesionRemota(
        [FromKeyedServices("ApiPHP")] HttpClient clientePhp,
        ServicioAutenticacion auth,
        RepositorioVinculosUsuario vinculos)
    {
        _clientePhp = clientePhp;
        _auth = auth;
        _vinculos = vinculos;
    }

    // =====================================================
    // RESTAURAR UNA SESION DESDE MYSQL
    // =====================================================
    //
    // Solo se activa el perfil local despues
    // de comprobar el token con la API PHP.
    //
    // Nunca se considera suficiente el ID
    // almacenado anteriormente en SQLite.
    // =====================================================

    public async Task<(bool Restaurada, string Mensaje)>
        RestaurarAsync()
    {
        // IMPEDIR QUE UNA SESION LOCAL ANTIGUA
        // SE CONSIDERE AUTENTICADA.
        _auth.CerrarSesion();

        string? token;

        try
        {
            // 1. LEER EL TOKEN DESDE SECURESTORAGE
            token = await SecureStorage.Default.GetAsync(
                ClaveToken);
        }
        catch (Exception)
        {
            return (
                false,
                "No se pudo acceder al almacenamiento " +
                "seguro del dispositivo."
            );
        }

        // 2. COMPROBAR SI EXISTE UN TOKEN
        if (string.IsNullOrWhiteSpace(token))
        {
            return (
                false,
                "No hay una sesión guardada."
            );
        }

        // 3. COMPROBAR EL FORMATO DEL TOKEN
        if (token.Length != 64 ||
            !token.All(Uri.IsHexDigit))
        {
            SecureStorage.Default.Remove(ClaveToken);

            return (
                false,
                "El token guardado no es válido."
            );
        }

        try
        {
            // 4. CONSULTAR LA API PHP DE WOPRES
            //
            // Enviamos el token mediante el
            // encabezado Authorization: Bearer.
            // No se incluye en la URL.

            using var solicitud = new HttpRequestMessage(
                HttpMethod.Get,
                "sesion-validar.php");

            solicitud.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);

            using var respuesta =
                await _clientePhp.SendAsync(solicitud);

            // 5. TOKEN REVOCADO O VENCIDO
            if (respuesta.StatusCode ==
                HttpStatusCode.Unauthorized)
            {
                SecureStorage.Default.Remove(ClaveToken);

                return (
                    false,
                    "Tu sesión ha vencido. " +
                    "Inicia sesión nuevamente."
                );
            }

            // 6. ERROR TEMPORAL DEL SERVIDOR
            //
            // No borramos el token por un error
            // del servidor o de conectividad.
            if (!respuesta.IsSuccessStatusCode)
            {
                return (
                    false,
                    "No fue posible verificar la sesión " +
                    "con el servidor."
                );
            }

            // 7. LEER LA RESPUESTA JSON
            var contenido = await respuesta.Content
                .ReadFromJsonAsync<RespuestaSesion>();

            if (contenido?.Estado == "sesion_invalida")
            {
                SecureStorage.Default.Remove(ClaveToken);

                return (
                    false,
                    "Tu sesión ya no es válida."
                );
            }

            // 8. VALIDAR LA IDENTIDAD REMOTA
            if (contenido?.Estado != "sesion_valida" ||
                contenido.Usuario == null ||
                contenido.Usuario.Id <= 0 ||
                string.IsNullOrWhiteSpace(
                    contenido.Usuario.Nombre) ||
                string.IsNullOrWhiteSpace(
                    contenido.Usuario.Correo))
            {
                return (
                    false,
                    "El servidor devolvió información " +
                    "de sesión incompleta."
                );
            }

            // 9. RECUPERAR EL PERFIL LOCAL VINCULADO
            //
            // Se utiliza el ID devuelto por MySQL.
            // Nunca se busca un usuario por correo.
            int usuarioLocalId =
                await _vinculos.ObtenerOCrearPerfilLocalAsync(
                    contenido.Usuario.Id,
                    contenido.Usuario.Nombre);

            // 10. ACTIVAR EL PERFIL LOCAL
            bool activado =
                await _auth.ActivarPerfilLocalAsync(
                    usuarioLocalId);

            if (!activado)
            {
                return (
                    false,
                    "No se pudo recuperar tu perfil " +
                    "local de Study Space."
                );
            }

            return (
                true,
                "Sesión restaurada correctamente."
            );
        }
        catch (HttpRequestException)
        {
            return (
                false,
                "No se pudo conectar con el servidor. " +
                "Comprueba tu conexión a internet."
            );
        }
        catch (TaskCanceledException)
        {
            return (
                false,
                "Se agotó el tiempo de espera " +
                "al comprobar tu sesión."
            );
        }
        catch (JsonException)
        {
            return (
                false,
                "El servidor devolvió una respuesta " +
                "que no se pudo interpretar."
            );
        }
        catch (Exception)
        {
            return (
                false,
                "No se pudo restaurar la sesión. " +
                "Puedes iniciar sesión nuevamente."
            );
        }
    }

    // =====================================================
    // MODELOS DE RESPUESTA DE LA API PHP
    // =====================================================

    private sealed class RespuestaSesion
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("mensaje")]
        public string? Mensaje { get; set; }

        [JsonPropertyName("usuario")]
        public UsuarioApi? Usuario { get; set; }
    }

    private sealed class UsuarioApi
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("nombre")]
        public string? Nombre { get; set; }

        [JsonPropertyName("correo")]
        public string? Correo { get; set; }
    }
}
