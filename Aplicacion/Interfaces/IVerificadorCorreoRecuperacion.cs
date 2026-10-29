
namespace Aplicacion.Interfaces;

public interface IVerificadorCorreoRecuperacion
{
    // SOLICITAR UN CÓDIGO TEMPORAL
    // EL SERVICIO SE ENCARGARÁ DE ENVIARLO
    // AL CORREO DEL ESTUDIANTE
    Task SolicitarCodigoAsync(string correo);

    // VERIFICAR QUE EL CÓDIGO SEA VÁLIDO,
    // NO HAYA CADUCADO Y CORRESPONDA
    // AL CORREO INDICADO
    Task<bool> VerificarCodigoAsync(
        string correo,
        string codigo);
}
