using System.Security.Cryptography;
using System.Text;
using MailKit;
using MimeKit;

namespace StudySpaceAPI.Servicios;

public class ServicioCodigosRecuperacion
{
    private readonly ServicioCorreo _correo;

    private readonly SemaphoreSlim _bloqueo =
        new(1, 1);

    private readonly Dictionary<string, SolicitudRecuperacion>
        _solicitudes = new();

    private const int MinutosValidez = 10;
    private const int MaximoIntentos = 5;
    private const int SegundosEntreSolicitudes = 60;

    public ServicioCodigosRecuperacion(
        ServicioCorreo correo)
    {
        _correo = correo;
    }

    // DATOS INTERNOS DE CADA SOLICITUD
    private sealed class SolicitudRecuperacion
    {
        public required byte[] HashCodigo { get; init; }

        public required byte[] Sal { get; init; }

        public required DateTimeOffset Vencimiento { get; init; }

        public required DateTimeOffset ProximaSolicitud { get; init; }

        public int Intentos { get; set; }

        public byte[]? HashAutorizacion { get; set; }

        public DateTimeOffset? VencimientoAutorizacion { get; set; }
    }

    // NORMALIZAR CORREO
    private static string NormalizarCorreo(string correo)
    {
        if (string.IsNullOrWhiteSpace(correo))
            throw new ArgumentException(
                "Debes escribir un correo electrónico.");

        string normalizado =
            correo.Trim().ToLowerInvariant();

        if (normalizado.Length > 254 ||
            !MailboxAddress.TryParse(normalizado, out _))
        {
            throw new ArgumentException(
                "El correo electrónico no es válido.");
        }

        return normalizado;
    }

    // CALCULAR HASH DEL CÓDIGO
    private static byte[] CalcularHash(
        byte[] sal,
        string valor)
    {
        byte[] datos = Encoding.UTF8.GetBytes(valor);

        byte[] entrada = new byte[sal.Length + datos.Length];

        Buffer.BlockCopy(
            sal, 0, entrada, 0, sal.Length);

        Buffer.BlockCopy(
            datos, 0, entrada, sal.Length, datos.Length);

        return SHA256.HashData(entrada);
    }

    // LIMPIAR SOLICITUDES CADUCADAS
    private void LimpiarCaducadas()
    {
        var ahora = DateTimeOffset.UtcNow;

        var caducadas = _solicitudes
            .Where(x =>
                x.Value.Vencimiento <= ahora &&
                (x.Value.VencimientoAutorizacion == null ||
                 x.Value.VencimientoAutorizacion <= ahora))
            .Select(x => x.Key)
            .ToList();

        foreach (string correo in caducadas)
            _solicitudes.Remove(correo);
    }

    // GENERAR Y ENVIAR CÓDIGO
    public async Task<bool> SolicitarCodigoAsync(
        string correo)
    {
        correo = NormalizarCorreo(correo);

        await _bloqueo.WaitAsync();

        try
        {
            LimpiarCaducadas();

            var ahora = DateTimeOffset.UtcNow;

            // EVITAR SOLICITUDES DEMASIADO FRECUENTES
            if (_solicitudes.TryGetValue(
                    correo,
                    out var anterior) &&
                anterior.ProximaSolicitud > ahora)
            {
                return false;
            }

            // GENERAR CÓDIGO SEGURO DE SEIS DÍGITOS
            string codigo =
                RandomNumberGenerator
                    .GetInt32(0, 1_000_000)
                    .ToString("D6");

            byte[] sal =
                RandomNumberGenerator.GetBytes(32);

            byte[] hash =
                CalcularHash(sal, codigo);

            var solicitud = new SolicitudRecuperacion
            {
                HashCodigo = hash,
                Sal = sal,
                Vencimiento =
                    ahora.AddMinutes(MinutosValidez),
                ProximaSolicitud =
                    ahora.AddSeconds(SegundosEntreSolicitudes),
                Intentos = 0
            };

            // PREPARAR MENSAJE
            string mensaje =
                "Hola,\n\n" +
                "Recibimos una solicitud para recuperar " +
                "tu contraseña de Study Space.\n\n" +
                $"Tu código de verificación es: {codigo}\n\n" +
                "Este código caduca en 10 minutos.\n" +
                "No lo compartas con nadie.\n\n" +
                "Si no solicitaste recuperar tu contraseña, " +
                "puedes ignorar este mensaje.\n\n" +
                "Study Space";

            // ENVIAR A TRAVÉS DEL SMTP DE WOPRES
            await _correo.EnviarCorreoAsync(
                correo,
                "Study Space - Código de recuperación",
                mensaje);

            // GUARDAR ÚNICAMENTE EL HASH DEL CÓDIGO
            _solicitudes[correo] = solicitud;

            return true;
        }
        finally
        {
            _bloqueo.Release();
        }
    }

    // VERIFICAR CÓDIGO Y GENERAR AUTORIZACIÓN
    public async Task<string?> VerificarCodigoAsync(
        string correo,
        string codigo)
    {
        correo = NormalizarCorreo(correo);

        await _bloqueo.WaitAsync();

        try
        {
            LimpiarCaducadas();

            if (!_solicitudes.TryGetValue(
                    correo,
                    out var solicitud))
            {
                return null;
            }

            if (solicitud.Vencimiento <= DateTimeOffset.UtcNow ||
                solicitud.Intentos >= MaximoIntentos ||
                solicitud.HashAutorizacion != null)
            {
                return null;
            }

            // CADA VERIFICACIÓN CONSUME UN INTENTO
            solicitud.Intentos++;

            byte[] hashRecibido =
                CalcularHash(solicitud.Sal, codigo);

            bool correcto =
                CryptographicOperations.FixedTimeEquals(
                    solicitud.HashCodigo,
                    hashRecibido);

            if (!correcto)
                return null;

            // CREAR UNA AUTORIZACIÓN ALEATORIA
            // DIFERENTE DEL CÓDIGO DE SEIS DÍGITOS
            string autorizacion =
                Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32));

            solicitud.HashAutorizacion =
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(autorizacion));

            solicitud.VencimientoAutorizacion =
                DateTimeOffset.UtcNow.AddMinutes(5);

            return autorizacion;
        }
        finally
        {
            _bloqueo.Release();
        }
    }

    // CONSUMIR AUTORIZACIÓN UNA SOLA VEZ
    public async Task<bool> ConsumirAutorizacionAsync(
        string correo,
        string autorizacion)
    {
        correo = NormalizarCorreo(correo);

        await _bloqueo.WaitAsync();

        try
        {
            LimpiarCaducadas();

            if (!_solicitudes.TryGetValue(
                    correo,
                    out var solicitud))
            {
                return false;
            }

            if (solicitud.HashAutorizacion == null ||
                solicitud.VencimientoAutorizacion == null ||
                solicitud.VencimientoAutorizacion <=
                    DateTimeOffset.UtcNow)
            {
                return false;
            }

            byte[] hashRecibido =
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(autorizacion));

            bool correcto =
                CryptographicOperations.FixedTimeEquals(
                    solicitud.HashAutorizacion,
                    hashRecibido);

            if (!correcto)
                return false;

            // ELIMINAR LA AUTORIZACIÓN AL UTILIZARLA
            _solicitudes.Remove(correo);

            return true;
        }
        finally
        {
            _bloqueo.Release();
        }
    }
}
