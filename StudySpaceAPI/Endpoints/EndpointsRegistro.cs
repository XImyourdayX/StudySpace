
using Microsoft.AspNetCore.RateLimiting;
using StudySpaceAPI.Servicios;

namespace StudySpaceAPI.Endpoints;

public static class EndpointsRegistro
{
    // REGISTRAR LA RUTA DE CONFIRMACIÓN
    public static IEndpointRouteBuilder MapearConfirmacionRegistro(
        this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/correo/confirmar-registro",
            async (
                SolicitudConfirmacionRegistro solicitud,
                ServicioCodigosRecuperacion codigos) =>
            {
                string correo =
                    solicitud.Correo?.Trim() ?? "";

                string autorizacion =
                    solicitud.Autorizacion?.Trim() ?? "";

                // VALIDAR EL FORMATO DEL TOKEN
                // GENERADO POR STUDYSPACEAPI
                if (string.IsNullOrWhiteSpace(correo) ||
                    autorizacion.Length != 64 ||
                    !autorizacion.All(Uri.IsHexDigit))
                {
                    return Results.BadRequest(new
                    {
                        Mensaje =
                            "La autorización no es válida."
                    });
                }

                try
                {
                    // COMPROBAR QUE LA AUTORIZACIÓN
                    // PERTENECE A ESTE MISMO CORREO
                    //
                    // SI ES VÁLIDA, SE CONSUME
                    // Y NO PODRÁ UTILIZARSE OTRA VEZ
                    bool confirmada =
                        await codigos.ConsumirAutorizacionAsync(
                            correo,
                            autorizacion);

                    if (!confirmada)
                    {
                        return Results.BadRequest(new
                        {
                            Mensaje =
                                "La autorización caducó, " +
                                "ya fue utilizada o no " +
                                "corresponde al correo."
                        });
                    }

                    // NO DEVOLVER EL TOKEN
                    // NI GUARDAR CONTRASEÑAS AQUÍ
                    return Results.Ok(new
                    {
                        Mensaje =
                            "Autorización de correo " +
                            "confirmada correctamente."
                    });
                }
                catch (ArgumentException)
                {
                    return Results.BadRequest(new
                    {
                        Mensaje =
                            "El correo electrónico no es válido."
                    });
                }
            })
            .RequireRateLimiting("VerificarCodigo");

        return app;
    }
}

// DATOS QUE RECIBIRÁ LA RUTA
public sealed record SolicitudConfirmacionRegistro(
    string? Correo,
    string? Autorizacion);
