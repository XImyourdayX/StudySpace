
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using StudySpaceAPI.Endpoints;
using StudySpaceAPI.Servicios;

var builder = WebApplication.CreateBuilder(args);

// SERVICIOS GENERALES
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// PROTECCIÓN DEL TOKEN DE GMAIL
builder.Services.AddDataProtection();
builder.Services.AddSingleton<AlmacenTokenGmail>();

// CORREO SMTP DE WOPRES
builder.Services.AddTransient<ServicioCorreo>();

// CÓDIGOS TEMPORALES
builder.Services.AddSingleton<ServicioCodigosRecuperacion>();

// LIMITAR LAS PETICIONES POR DIRECCIÓN IP
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = 429;

    // SOLICITAR CÓDIGOS
    opciones.AddPolicy("SolicitarCodigo", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ObtenerIp(contexto),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // VERIFICAR Y CONFIRMAR CÓDIGOS
    opciones.AddPolicy("VerificarCodigo", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ObtenerIp(contexto),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

// =====================================================
// ESTADO DE LA API
// =====================================================
app.MapGet("/api/estado", () =>
{
    return Results.Ok(new
    {
        Aplicacion = "Study Space",
        Servicio = "Verificación de correo",
        Estado = "Funcionando"
    });
});

// =====================================================
// SOLICITAR CÓDIGO POR CORREO
// =====================================================
app.MapPost("/api/correo/solicitar-codigo",
    async (
        SolicitudCodigoCorreo solicitud,
        ServicioCodigosRecuperacion codigos,
        ILogger<Program> logger) =>
    {
        string correo = solicitud.Correo?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(correo))
        {
            return Results.BadRequest(new
            {
                Mensaje = "Ingresa un correo electrónico."
            });
        }

        try
        {
            bool enviado =
                await codigos.SolicitarCodigoAsync(correo);

            if (!enviado)
            {
                return Results.Json(new
                {
                    Mensaje = "Espera antes de solicitar " +
                              "otro código."
                },
                statusCode: 429);
            }

            return Results.Ok(new
            {
                Mensaje = "Si el envío se completó, " +
                          "revisa tu correo electrónico.",
                ValidezMinutos = 10
            });
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new
            {
                Mensaje = "El correo electrónico no es válido."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error al solicitar un código de correo.");

            return Results.Problem(
                "No se pudo procesar la solicitud.",
                statusCode: 503);
        }
    })
    .RequireRateLimiting("SolicitarCodigo");

// =====================================================
// VERIFICAR CÓDIGO RECIBIDO
// =====================================================
app.MapPost("/api/correo/verificar-codigo",
    async (
        VerificacionCodigoCorreo solicitud,
        ServicioCodigosRecuperacion codigos,
        ILogger<Program> logger) =>
    {
        string correo = solicitud.Correo?.Trim() ?? "";
        string codigo = solicitud.Codigo ?? "";

        if (codigo.Length != 6 ||
            codigo.Any(c => c < '0' || c > '9'))
        {
            return Results.BadRequest(new
            {
                Mensaje = "El código debe contener " +
                          "seis números."
            });
        }

        try
        {
            string? autorizacion =
                await codigos.VerificarCodigoAsync(
                    correo,
                    codigo);

            if (string.IsNullOrWhiteSpace(autorizacion))
            {
                return Results.BadRequest(new
                {
                    Mensaje = "El código es incorrecto, " +
                              "caducó o ya fue utilizado."
                });
            }

            // AUTORIZACIÓN TEMPORAL DE 5 MINUTOS.
            // TODAVÍA NO ACTIVA NINGUNA CUENTA.
            return Results.Ok(new
            {
                Mensaje = "Correo verificado correctamente.",
                Correo = correo,
                Autorizacion = autorizacion,
                ValidezMinutos = 5
            });
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new
            {
                Mensaje = "El correo electrónico no es válido."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error al verificar un código de correo.");

            return Results.Problem(
                "No se pudo procesar la verificación.",
                statusCode: 503);
        }
    })
    .RequireRateLimiting("VerificarCodigo");

// =====================================================
// NUEVO: CONFIRMAR AUTORIZACIÓN DEL REGISTRO
// =====================================================
//
// UTILIZA LA RUTA DEFINIDA EN:
// Endpoints/EndpointsRegistro.cs
//
// POST /api/correo/confirmar-registro
//
app.MapearConfirmacionRegistro();

// =====================================================
// RUTAS EXCLUSIVAS DE DESARROLLO LOCAL
// =====================================================
if (app.Environment.IsDevelopment())
{
    // PRUEBA DEL SERVIDOR SMTP
    app.MapPost("/api/pruebas/correo",
        async (
            HttpContext contexto,
            IConfiguration configuracion,
            ServicioCorreo correo) =>
        {
            if (!EsPruebaLocal(contexto))
                return Results.NotFound();

            string? destinatario =
                configuracion["CorreoSMTP:Remitente"];

            if (string.IsNullOrWhiteSpace(destinatario))
            {
                return Results.Problem(
                    "Falta configurar el remitente SMTP.");
            }

            try
            {
                await correo.EnviarCorreoAsync(
                    destinatario,
                    "Study Space - Prueba de correo",
                    "¡Hola!\n\n" +
                    "StudySpaceAPI puede enviar correos " +
                    "mediante Wopres.\n\n" +
                    "Study Space");

                return Results.Ok(new
                {
                    Estado = "Correo enviado al servidor SMTP"
                });
            }
            catch (Exception)
            {
                return Results.Problem(
                    "No se pudo enviar el correo.",
                    statusCode: 502);
            }
        });

    // SOLICITAR UN CÓDIGO DE PRUEBA
    app.MapPost("/api/pruebas/codigo",
        async (
            HttpContext contexto,
            IConfiguration configuracion,
            ServicioCodigosRecuperacion codigos) =>
        {
            if (!EsPruebaLocal(contexto))
                return Results.NotFound();

            string? correo =
                configuracion["CorreoSMTP:Remitente"];

            if (string.IsNullOrWhiteSpace(correo))
            {
                return Results.Problem(
                    "No se configuró el correo de pruebas.");
            }

            try
            {
                bool enviado =
                    await codigos.SolicitarCodigoAsync(correo);

                if (!enviado)
                {
                    return Results.Json(new
                    {
                        Estado = "Espera 60 segundos " +
                                 "antes de solicitar otro código."
                    },
                    statusCode: 429);
                }

                return Results.Ok(new
                {
                    Estado = "Código de recuperación enviado"
                });
            }
            catch (Exception)
            {
                return Results.Problem(
                    "No se pudo enviar el código.",
                    statusCode: 502);
            }
        });

    // VERIFICAR UN CÓDIGO DE PRUEBA
    app.MapPost("/api/pruebas/verificar-codigo",
        async (
            HttpContext contexto,
            SolicitudVerificacionPrueba solicitud,
            IConfiguration configuracion,
            ServicioCodigosRecuperacion codigos) =>
        {
            if (!EsPruebaLocal(contexto))
                return Results.NotFound();

            string codigo = solicitud.Codigo ?? "";

            if (codigo.Length != 6 ||
                codigo.Any(c => c < '0' || c > '9'))
            {
                return Results.BadRequest(new
                {
                    Estado = "Código no válido."
                });
            }

            string? correo =
                configuracion["CorreoSMTP:Remitente"];

            if (string.IsNullOrWhiteSpace(correo))
            {
                return Results.Problem(
                    "No se configuró el correo de pruebas.");
            }

            try
            {
                string? autorizacion =
                    await codigos.VerificarCodigoAsync(
                        correo,
                        codigo);

                if (string.IsNullOrWhiteSpace(autorizacion))
                {
                    return Results.BadRequest(new
                    {
                        Estado = "Código incorrecto, " +
                                 "caducado o ya utilizado."
                    });
                }

                bool consumida =
                    await codigos.ConsumirAutorizacionAsync(
                        correo,
                        autorizacion);

                return consumida
                    ? Results.Ok(new
                    {
                        Estado = "Código verificado correctamente",
                        Nota = "No se modificó ninguna contraseña."
                    })
                    : Results.BadRequest(new
                    {
                        Estado = "No se completó la verificación."
                    });
            }
            catch (Exception)
            {
                return Results.Problem(
                    "No se pudo verificar el código.",
                    statusCode: 500);
            }
        });

    // INICIAR AUTORIZACIÓN DE GOOGLE
    app.MapGet("/oauth/google/iniciar",
        (
            HttpContext contexto,
            IConfiguration configuracion,
            IMemoryCache cache) =>
        {
            if (!IPAddress.IsLoopback(
                contexto.Connection.RemoteIpAddress ??
                IPAddress.None))
            {
                return Results.NotFound();
            }

            string? clienteId =
                configuracion["GoogleOAuth:ClientId"];

            string? uriRetorno =
                configuracion["GoogleOAuth:RedirectUri"];

            if (string.IsNullOrWhiteSpace(clienteId) ||
                string.IsNullOrWhiteSpace(uriRetorno))
            {
                return Results.Problem(
                    "Falta la configuración de GoogleOAuth.");
            }

            string estado = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            cache.Set(
                $"OAuthEstado:{estado}",
                true,
                TimeSpan.FromMinutes(10));

            string permiso =
                "https://www.googleapis.com/auth/gmail.send";

            string url =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                "?response_type=code" +
                "&client_id=" +
                Uri.EscapeDataString(clienteId) +
                "&redirect_uri=" +
                Uri.EscapeDataString(uriRetorno) +
                "&scope=" +
                Uri.EscapeDataString(permiso) +
                "&access_type=offline" +
                "&prompt=consent" +
                "&state=" +
                Uri.EscapeDataString(estado);

            return Results.Redirect(url);
        });

    // RECIBIR LA RESPUESTA DE GOOGLE
    app.MapGet("/oauth/google/callback",
        async (
            HttpContext contexto,
            IConfiguration configuracion,
            IMemoryCache cache,
            IHttpClientFactory fabricaHttp,
            AlmacenTokenGmail almacen) =>
        {
            if (!IPAddress.IsLoopback(
                contexto.Connection.RemoteIpAddress ??
                IPAddress.None))
            {
                return Results.NotFound();
            }

            var consulta = contexto.Request.Query;

            string estado = consulta["state"].ToString();

            if (string.IsNullOrWhiteSpace(estado))
            {
                return Results.BadRequest(
                    "No se recibió el estado OAuth.");
            }

            string claveEstado = $"OAuthEstado:{estado}";

            if (!cache.TryGetValue(claveEstado, out _))
            {
                return Results.BadRequest(
                    "La autorización ha caducado o no es válida.");
            }

            cache.Remove(claveEstado);

            if (consulta.ContainsKey("error"))
            {
                return Results.Redirect(
                    "/oauth/google/resultado?ok=0");
            }

            string codigo = consulta["code"].ToString();

            if (string.IsNullOrWhiteSpace(codigo))
            {
                return Results.Redirect(
                    "/oauth/google/resultado?ok=0");
            }

            string? clienteId =
                configuracion["GoogleOAuth:ClientId"];

            string? clienteSecreto =
                configuracion["GoogleOAuth:ClientSecret"];

            string? uriRetorno =
                configuracion["GoogleOAuth:RedirectUri"];

            if (string.IsNullOrWhiteSpace(clienteId) ||
                string.IsNullOrWhiteSpace(clienteSecreto) ||
                string.IsNullOrWhiteSpace(uriRetorno))
            {
                return Results.Problem(
                    "La configuración OAuth está incompleta.");
            }

            var datos = new Dictionary<string, string>
            {
                ["code"] = codigo,
                ["client_id"] = clienteId,
                ["client_secret"] = clienteSecreto,
                ["redirect_uri"] = uriRetorno,
                ["grant_type"] = "authorization_code"
            };

            var clienteHttp = fabricaHttp.CreateClient();

            try
            {
                using var contenidoSolicitud =
                    new FormUrlEncodedContent(datos);

                using var respuesta =
                    await clienteHttp.PostAsync(
                        "https://oauth2.googleapis.com/token",
                        contenidoSolicitud);

                if (!respuesta.IsSuccessStatusCode)
                {
                    return Results.Redirect(
                        "/oauth/google/resultado?ok=0");
                }

                using var documento =
                    await JsonDocument.ParseAsync(
                        await respuesta.Content.ReadAsStreamAsync());

                var raiz = documento.RootElement;

                if (!raiz.TryGetProperty(
                        "access_token",
                        out var accessToken) ||
                    string.IsNullOrWhiteSpace(
                        accessToken.GetString()))
                {
                    return Results.Redirect(
                        "/oauth/google/resultado?ok=0");
                }

                string? refreshToken = null;

                if (raiz.TryGetProperty(
                    "refresh_token",
                    out var propiedadRefresh))
                {
                    refreshToken =
                        propiedadRefresh.GetString();
                }

                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    await almacen.GuardarAsync(refreshToken);

                    return Results.Redirect(
                        "/oauth/google/resultado?ok=1");
                }

                string? anterior =
                    await almacen.ObtenerAsync();

                return Results.Redirect(
                    string.IsNullOrWhiteSpace(anterior)
                        ? "/oauth/google/resultado?ok=0"
                        : "/oauth/google/resultado?ok=1");
            }
            catch (Exception ex) when (
                ex is HttpRequestException ||
                ex is JsonException ||
                ex is IOException ||
                ex is CryptographicException)
            {
                return Results.Redirect(
                    "/oauth/google/resultado?ok=0");
            }
        });

    // RESULTADO DE AUTORIZACIÓN
    app.MapGet("/oauth/google/resultado",
        (HttpContext contexto) =>
        {
            if (!IPAddress.IsLoopback(
                contexto.Connection.RemoteIpAddress ??
                IPAddress.None))
            {
                return Results.NotFound();
            }

            bool correcto =
                contexto.Request.Query["ok"] == "1";

            return Results.Ok(new
            {
                Aplicacion = "Study Space",
                OAuth = correcto
                    ? "Autorización completada correctamente"
                    : "No se pudo completar la autorización",
                Token = correcto
                    ? "Token de renovación guardado o disponible"
                    : "No se confirmó un token guardado"
            });
        });
}

app.Run();

// =====================================================
// FUNCIONES AUXILIARES
// =====================================================

// OBTENER LA IP DE LA CONEXIÓN
static string ObtenerIp(HttpContext contexto)
{
    return contexto.Connection.RemoteIpAddress?.ToString()
           ?? "desconocida";
}

// COMPROBAR QUE LAS PRUEBAS SEAN LOCALES
static bool EsPruebaLocal(HttpContext contexto)
{
    bool esLocal = IPAddress.IsLoopback(
        contexto.Connection.RemoteIpAddress ??
        IPAddress.None);

    bool cabeceraValida =
        contexto.Request.Headers[
            "X-StudySpace-Prueba"].ToString() == "local";

    return esLocal && cabeceraValida;
}

// =====================================================
// MODELOS DE SOLICITUD
// =====================================================
public record SolicitudCodigoCorreo(string Correo);

public record VerificacionCodigoCorreo(
    string Correo,
    string Codigo);

public record SolicitudVerificacionPrueba(
    string Codigo);
