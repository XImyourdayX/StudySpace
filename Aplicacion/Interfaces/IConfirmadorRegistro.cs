
namespace Aplicacion.Interfaces;

public interface IConfirmadorRegistro
{
    // CONFIRMAR EN STUDYSPACEAPI QUE EL CORREO
    // FUE VERIFICADO MEDIANTE UN CÓDIGO.

    // LA AUTORIZACIÓN ES TEMPORAL
    // Y SE CONSUME UNA SOLA VEZ.
    Task<(bool Ok, string Mensaje)> ConfirmarRegistroAsync(
        string correo,
        string autorizacion);
}
