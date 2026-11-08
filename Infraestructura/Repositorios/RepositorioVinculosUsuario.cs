
using Dominio.Entidades;
using Infraestructura.DBContext;

namespace Infraestructura.Repositorios;

public class RepositorioVinculosUsuario
{
    private readonly ContextoBaseDatos _contexto;

    public RepositorioVinculosUsuario(
        ContextoBaseDatos contexto)
    {
        _contexto = contexto ??
            throw new ArgumentNullException(
                nameof(contexto));
    }

    // =====================================================
    // OBTENER EL PERFIL LOCAL DE UNA CUENTA MYSQL
    // =====================================================
    //
    // El identificador remoto debe provenir
    // de una respuesta autenticada de la API PHP.
    //
    // Nunca se busca por correo.
    // =====================================================

    public async Task<int?> ObtenerUsuarioLocalIdAsync(
        long usuarioRemotoId)
    {
        if (usuarioRemotoId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usuarioRemotoId));
        }

        var db = await _contexto.ObtenerDb();

        var vinculo = await db
            .Table<VinculoUsuarioRemoto>()
            .Where(v =>
                v.UsuarioRemotoId == usuarioRemotoId)
            .FirstOrDefaultAsync();

        return vinculo?.UsuarioLocalId;
    }

    // =====================================================
    // OBTENER O CREAR UN PERFIL LOCAL
    // =====================================================
    //
    // Si la cuenta MySQL ya tiene un perfil local
    // vinculado, devuelve su identificador.
    //
    // Si no tiene vinculo:
    //
    // 1. Crea un perfil local NUEVO.
    // 2. Guarda la vinculacion con MySQL.
    // 3. Devuelve el ID local.
    //
    // NO reutiliza usuarios antiguos por correo.
    // NO modifica cursos ni tareas existentes.
    //
    // Ambos registros se crean en una transaccion,
    // evitando dejar vinculaciones incompletas.
    // =====================================================

    public async Task<int> ObtenerOCrearPerfilLocalAsync(
        long usuarioRemotoId,
        string nombre)
    {
        if (usuarioRemotoId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usuarioRemotoId));
        }

        nombre = nombre?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException(
                "El nombre del usuario es obligatorio.",
                nameof(nombre));
        }

        if (nombre.Length > 120)
        {
            throw new ArgumentException(
                "El nombre del usuario es demasiado largo.",
                nameof(nombre));
        }

        var db = await _contexto.ObtenerDb();

        int usuarioLocalId = 0;

        await db.RunInTransactionAsync(conexion =>
        {
            // 1. COMPROBAR SI YA EXISTE VINCULACION
            var vinculoExistente = conexion
                .Table<VinculoUsuarioRemoto>()
                .Where(v =>
                    v.UsuarioRemotoId == usuarioRemotoId)
                .FirstOrDefault();

            if (vinculoExistente != null)
            {
                // COMPROBAR QUE EL PERFIL LOCAL
                // TODAVIA EXISTA.
                var usuarioExistente =
                    conexion.Find<Usuario>(
                        vinculoExistente.UsuarioLocalId);

                if (usuarioExistente == null)
                {
                    throw new InvalidOperationException(
                        "La vinculación existe, pero " +
                        "su perfil local no fue encontrado.");
                }

                usuarioLocalId = usuarioExistente.Id;

                return;
            }

            // 2. CREAR UN PERFIL LOCAL INDEPENDIENTE
            //
            // Utilizamos una direccion interna unica
            // para evitar conflictos con correos
            // de usuarios antiguos de SQLite.
            //
            // No es una direccion para enviar correos.
            // El correo real sigue en MySQL.
            string identificadorInterno =
                $"remoto-{usuarioRemotoId}-" +
                $"{Guid.NewGuid():N}@studyspace.invalid";

            var usuarioLocal = new Usuario
            {
                Nombre = nombre,

                Correo = identificadorInterno,

                // LAS CONTRASENAS SE GESTIONAN
                // EXCLUSIVAMENTE EN MYSQL.
                PasswordHash = "",

                // ESTE METODO SOLO DEBE LLAMARSE
                // TRAS AUTENTICAR CON LA API PHP.
                CorreoVerificado = true
            };

            conexion.Insert(usuarioLocal);

            // 3. VINCULAR AMBOS IDENTIFICADORES
            var nuevoVinculo = new VinculoUsuarioRemoto
            {
                UsuarioRemotoId = usuarioRemotoId,

                UsuarioLocalId = usuarioLocal.Id,

                FechaVinculacionUtc = DateTime.UtcNow
            };

            conexion.Insert(nuevoVinculo);

            usuarioLocalId = usuarioLocal.Id;
        });

        return usuarioLocalId;
    }
}
