using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aplicacion.CasosDeUso;
using Dominio.Entidades;
using Infraestructura.DBContext;
using Microsoft.Maui.Storage;

namespace StudySpace.Servicios;

/// <summary>
/// HU16, paso 1: exportación cifrada de cursos y tareas del perfil autenticado.
/// No incluye contraseñas, tokens, usuarios locales ni IDs de calendarios Android.
/// Este servicio NO restaura datos todavía; eso se desarrollará y probará aparte.
/// </summary>
public sealed class ServicioRespaldoCifrado
{
    private const int VersionFormato = 1;
    private const int IteracionesPbkdf2 = 210_000;
    private const int LongitudSal = 16;
    private const int LongitudNonce = 12;
    private const int LongitudClave = 32;
    private const int LongitudEtiqueta = 16;

    private readonly ContextoBaseDatos _contexto;
    private readonly ServicioAutenticacion _autenticacion;

    public ServicioRespaldoCifrado(
        ContextoBaseDatos contexto,
        ServicioAutenticacion autenticacion)
    {
        _contexto = contexto;
        _autenticacion = autenticacion;
    }

    /// <summary>
    /// Crea un archivo .ssbackup TEMPORAL en el caché de Study Space.
    /// La interfaz (paso 2) deberá ofrecerlo con Share para que el usuario
    /// lo guarde FUERA de la aplicación; el caché no es una copia permanente.
    /// </summary>
    public async Task<ResultadoRespaldo> CrearRespaldoAsync(string fraseSecreta)
    {
        if (string.IsNullOrWhiteSpace(fraseSecreta) ||
            fraseSecreta.Length < 12 || fraseSecreta.Length > 256)
        {
            throw new ArgumentException(
                "Utiliza una contraseña de respaldo de 12 a 256 caracteres.",
                nameof(fraseSecreta));
        }

        int usuarioLocalId = _autenticacion.UsuarioActual?.Id ?? 0;
        if (usuarioLocalId <= 0)
            throw new InvalidOperationException("Debes iniciar sesión para crear un respaldo.");

        var db = await _contexto.ObtenerDb();

        // El ID remoto vincula el respaldo a la cuenta MySQL, no a un ID
        // SQLite que puede cambiar al restaurar en otro dispositivo.
        var vinculo = await db.Table<VinculoUsuarioRemoto>()
            .Where(v => v.UsuarioLocalId == usuarioLocalId)
            .FirstOrDefaultAsync();
        if (vinculo is null || vinculo.UsuarioRemotoId <= 0)
            throw new InvalidOperationException(
                "No se encontró la vinculación de tu cuenta. Inicia sesión nuevamente.");

        var cursos = await db.Table<Curso>()
            .Where(c => c.UsuarioId == usuarioLocalId)
            .ToListAsync();
        var tareas = await db.Table<Tarea>()
            .Where(t => t.UsuarioId == usuarioLocalId)
            .ToListAsync();

        // No incluir tareas que apunten a un curso inexistente: la importación
        // posterior exige conservar la relación entre curso y tarea.
        var idsCursos = cursos.Select(c => c.Id).ToHashSet();
        if (tareas.Any(t => !idsCursos.Contains(t.CursoId)))
            throw new InvalidOperationException(
                "Hay tareas sin un curso válido. No se generó una copia incompleta.");

        var fechaUtc = DateTimeOffset.UtcNow;
        var datos = new DatosRespaldo
        {
            Formato = "studyspace-academico",
            Version = VersionFormato,
            CuentaRemotaId = vinculo.UsuarioRemotoId,
            CreadoUtc = fechaUtc,
            Cursos = cursos.Select(c => new CursoRespaldo
            {
                IdOriginal = c.Id,
                Nombre = c.Nombre,
                Color = c.Color
            }).ToList(),
            Tareas = tareas.Select(t => new TareaRespaldo
            {
                IdOriginal = t.Id,
                CursoIdOriginal = t.CursoId,
                Titulo = t.Titulo,
                Descripcion = t.Descripcion,
                FechaLimite = t.FechaLimite,
                Prioridad = t.Prioridad,
                Completada = t.Completada,
                FechaCompletada = t.FechaCompletada,
                MinutosAviso = t.MinutosAviso
            }).ToList()
        };

        byte[] plano = JsonSerializer.SerializeToUtf8Bytes(datos);
        byte[] sal = RandomNumberGenerator.GetBytes(LongitudSal);
        byte[] nonce = RandomNumberGenerator.GetBytes(LongitudNonce);
        byte[] clave = Rfc2898DeriveBytes.Pbkdf2(
            fraseSecreta, sal, IteracionesPbkdf2, HashAlgorithmName.SHA256, LongitudClave);

        try
        {
            byte[] cifrado = new byte[plano.Length];
            byte[] etiqueta = new byte[LongitudEtiqueta];
            using (var aes = new AesGcm(clave, LongitudEtiqueta))
            {
                aes.Encrypt(nonce, plano, cifrado, etiqueta);
            }

            var contenedor = new ContenedorCifrado
            {
                Formato = "studyspace-backup-aes256gcm",
                Version = VersionFormato,
                Kdf = "PBKDF2-SHA256",
                Iteraciones = IteracionesPbkdf2,
                Sal = Convert.ToBase64String(sal),
                Nonce = Convert.ToBase64String(nonce),
                Etiqueta = Convert.ToBase64String(etiqueta),
                DatosCifrados = Convert.ToBase64String(cifrado)
            };

            string archivo = Path.Combine(
                FileSystem.CacheDirectory,
                $"StudySpace_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.ssbackup");
            byte[] salida = JsonSerializer.SerializeToUtf8Bytes(contenedor);
            await File.WriteAllBytesAsync(archivo, salida);

            return new ResultadoRespaldo(
                archivo, cursos.Count, tareas.Count, fechaUtc);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
            CryptographicOperations.ZeroMemory(plano);
        }
    }

    public sealed record ResultadoRespaldo(
        string RutaTemporal,
        int CantidadCursos,
        int CantidadTareas,
        DateTimeOffset CreadoUtc);

    // Estos modelos no contienen identificadores locales del perfil, hashes,
    // tokens, configuración de notificaciones ni vínculos con otros dispositivos.
    private sealed class DatosRespaldo
    {
        public string Formato { get; set; } = "";
        public int Version { get; set; }
        public long CuentaRemotaId { get; set; }
        public DateTimeOffset CreadoUtc { get; set; }
        public List<CursoRespaldo> Cursos { get; set; } = new();
        public List<TareaRespaldo> Tareas { get; set; } = new();
    }

    private sealed class CursoRespaldo
    {
        public int IdOriginal { get; set; }
        public string Nombre { get; set; } = "";
        public string Color { get; set; } = "";
    }

    private sealed class TareaRespaldo
    {
        public int IdOriginal { get; set; }
        public int CursoIdOriginal { get; set; }
        public string Titulo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public DateTime FechaLimite { get; set; }
        public string Prioridad { get; set; } = "Media";
        public bool Completada { get; set; }
        public DateTime? FechaCompletada { get; set; }
        public int MinutosAviso { get; set; }
    }

    private sealed class ContenedorCifrado
    {
        public string Formato { get; set; } = "";
        public int Version { get; set; }
        public string Kdf { get; set; } = "";
        public int Iteraciones { get; set; }
        public string Sal { get; set; } = "";
        public string Nonce { get; set; } = "";
        public string Etiqueta { get; set; } = "";
        public string DatosCifrados { get; set; } = "";
    }
}
