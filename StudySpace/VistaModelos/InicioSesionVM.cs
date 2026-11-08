using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aplicacion.CasosDeUso;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infraestructura.Repositorios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace StudySpace.VistaModelos;

public partial class InicioSesionVM : ObservableObject
{
    private readonly HttpClient _clientePhp;
    private readonly ServicioAutenticacion _auth;
    private readonly RepositorioVinculosUsuario _vinculos;

    // TOKEN REMOTO GUARDADO EN ALMACENAMIENTO SEGURO.
    // NO UTILIZAR PREFERENCES PARA ESTE TOKEN.
    private const string ClaveToken =
        "studyspace_api_php_token";

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
        !string.IsNullOrWhiteSpace(MensajeError);

    public InicioSesionVM(
        [FromKeyedServices("ApiPHP")] HttpClient clientePhp,
        ServicioAutenticacion auth,
        RepositorioVinculosUsuario vinculos)
    {
        _clientePhp = clientePhp;
        _auth = auth;
        _vinculos = vinculos;
    }

    // =====================================================
    // INICIAR SESION CON MYSQL Y ACTIVAR PERFIL SQLITE
    // =====================================================

    [RelayCommand]
    private async Task IniciarSesion()
    {
        if (Ocupado)
            return;

        MensajeError = "";

        string correoNormalizado =
            Correo?.Trim().ToLowerInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(correoNormalizado))
        {
            MensajeError = "Ingresa tu correo electrónico.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            MensajeError = "Ingresa tu contraseña.";
            return;
        }

        Ocupado = true;

        bool tokenGuardado = false;
        bool perfilActivado = false;
        bool accesoCompletado = false;

        try
        {
            // ============================================
            // 1. AUTENTICAR EN LA API PHP DE WOPRES
            // ============================================

            var datos = new
            {
                correo = correoNormalizado,
                contrasena = Password
            };

            using var respuesta =
                await _clientePhp.PostAsJsonAsync(
                    "iniciar-sesion.php",
                    datos);

            var contenido =
                await respuesta.Content
                    .ReadFromJsonAsync<RespuestaInicioSesion>();

            if (!respuesta.IsSuccessStatusCode ||
                contenido?.Estado != "sesion_iniciada")
            {
                MensajeError =
                    contenido?.Mensaje ??
                    "Correo o contraseña incorrectos.";

                return;
            }

            // ============================================
            // 2. VALIDAR LOS DATOS DE LA RESPUESTA
            // ============================================

            if (!TokenEsValido(contenido.Token) ||
                contenido.Usuario == null ||
                contenido.Usuario.Id <= 0 ||
                string.IsNullOrWhiteSpace(
                    contenido.Usuario.Nombre) ||
                string.IsNullOrWhiteSpace(
                    contenido.Usuario.Correo) ||
                !string.Equals(
                    contenido.Usuario.Correo.Trim(),
                    correoNormalizado,
                    StringComparison.OrdinalIgnoreCase))
            {
                MensajeError =
                    "El servidor devolvió datos de sesión " +
                    "incompletos o no válidos.";

                return;
            }

            // ============================================
            // 3. GUARDAR EL TOKEN EN SECURESTORAGE
            // ============================================

            await SecureStorage.Default.SetAsync(
                ClaveToken,
                contenido.Token!);

            tokenGuardado = true;

            // ============================================
            // 4. OBTENER O CREAR EL PERFIL LOCAL
            // ============================================
            //
            // Utilizamos exclusivamente el ID que
            // devolvio el servidor autenticado.
            //
            // No buscamos usuarios antiguos por correo.
            // No mezclamos sus cursos ni tareas.
            // ============================================

            int usuarioLocalId =
                await _vinculos.ObtenerOCrearPerfilLocalAsync(
                    contenido.Usuario.Id,
                    contenido.Usuario.Nombre!);

            // ============================================
            // 5. ACTIVAR LA SESION LOCAL VINCULADA
            // ============================================

            perfilActivado =
                await _auth.ActivarPerfilLocalAsync(
                    usuarioLocalId);

            if (!perfilActivado)
            {
                MensajeError =
                    "No fue posible activar tu perfil " +
                    "en este dispositivo.";

                return;
            }

            // ============================================
            // 6. ENTRAR A LA PANTALLA PRINCIPAL
            // ============================================

            Password = "";

            await Shell.Current.GoToAsync("//principal");

            accesoCompletado = true;
        }
        catch (TaskCanceledException)
        {
            MensajeError =
                "Se agotó el tiempo de espera. " +
                "Comprueba tu conexión a internet.";
        }
        catch (HttpRequestException)
        {
            MensajeError =
                "No se pudo conectar con el servidor " +
                "de Study Space.";
        }
        catch (JsonException)
        {
            MensajeError =
                "El servidor devolvió una respuesta no válida.";
        }
        catch (Exception)
        {
            MensajeError =
                "No se pudo completar el inicio de sesión. " +
                "Comprueba el almacenamiento del dispositivo " +
                "e inténtalo nuevamente.";
        }
        finally
        {
            // Si falla algun paso posterior al login
            // remoto, no dejamos una sesion local
            // parcialmente activada.
            if (!accesoCompletado)
            {
                if (perfilActivado)
                {
                    _auth.CerrarSesion();
                }

                if (tokenGuardado)
                {
                    SecureStorage.Default.Remove(ClaveToken);
                }
            }

            Ocupado = false;
        }
    }

    // =====================================================
    // COMPROBAR EL FORMATO DEL TOKEN REMOTO
    // =====================================================

    private static bool TokenEsValido(string? token)
    {
        return !string.IsNullOrWhiteSpace(token) &&
               token.Length == 64 &&
               token.All(Uri.IsHexDigit);
    }


    // RECUPERAR CONTRASENA
    [RelayCommand]
    private async Task RecuperarPassword()
    {
        if (Ocupado)
            return;

        MensajeError = "";

        // ABRIR LA PANTALLA DE RECUPERACION
        await Shell.Current.GoToAsync(
            "recuperarcontrasena");
    }

    // =====================================================
    // NAVEGACION
    // =====================================================

    [RelayCommand]
    private async Task IrARegistro()
    {
        if (Ocupado)
            return;

        await Shell.Current.GoToAsync("registro");
    }

    [RelayCommand]
    private async Task Volver()
    {
        if (Ocupado)
            return;

        await Shell.Current.GoToAsync("..");
    }

    // =====================================================
    // MODELOS DE RESPUESTA DE LA API PHP
    // =====================================================

    private sealed class RespuestaInicioSesion
    {
        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("mensaje")]
        public string? Mensaje { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("expira_en_segundos")]
        public int ExpiraEnSegundos { get; set; }

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
