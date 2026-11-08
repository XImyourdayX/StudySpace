
namespace StudySpace.Servicios;

// DATOS DE UNA CUENTA QUE ESPERA
// LA VERIFICACIÓN DEL CORREO
public sealed record DatosRegistroPendiente(
    string Nombre,
    string Correo,
    string PasswordHash
);

public class ServicioRegistroPendiente
{
    private readonly object _bloqueo = new();

    private DatosRegistroPendiente? _datos;

    private DateTimeOffset _vencimiento;

    // CONSERVAR LOS DATOS DURANTE 15 MINUTOS
    private static readonly TimeSpan Duracion =
        TimeSpan.FromMinutes(15);

    // PREPARAR REGISTRO SIN CREAR LA CUENTA
    public void Preparar(
        string nombre,
        string correo,
        string password)
    {
        if (string.IsNullOrWhiteSpace(nombre) ||
            string.IsNullOrWhiteSpace(correo) ||
            string.IsNullOrEmpty(password))
        {
            throw new ArgumentException(
                "Los datos de registro están incompletos.");
        }

        string nombreNormalizado = nombre.Trim();

        string correoNormalizado =
            correo.Trim().ToLowerInvariant();

        // NO CONSERVAR LA CONTRASEÑA EN TEXTO PLANO
        string hash = BCrypt.Net.BCrypt.HashPassword(
            password);

        lock (_bloqueo)
        {
            _datos = new DatosRegistroPendiente(
                nombreNormalizado,
                correoNormalizado,
                hash);

            _vencimiento =
                DateTimeOffset.UtcNow.Add(Duracion);
        }
    }

    // CONSULTAR EL REGISTRO PENDIENTE
    public DatosRegistroPendiente? Obtener()
    {
        lock (_bloqueo)
        {
            if (_datos == null)
                return null;

            if (DateTimeOffset.UtcNow >= _vencimiento)
            {
                LimpiarInternamente();
                return null;
            }

            return _datos;
        }
    }

    // ELIMINAR LOS DATOS AL FINALIZAR
    // O CANCELAR EL REGISTRO
    public void Limpiar()
    {
        lock (_bloqueo)
        {
            LimpiarInternamente();
        }
    }

    private void LimpiarInternamente()
    {
        _datos = null;
        _vencimiento = default;
    }
}

