
using Dominio.Entidades;
using SQLite;

namespace Infraestructura.DBContext;

public class ContextoBaseDatos
{
    private readonly string _ruta;

    private SQLiteAsyncConnection? _db;

    // EVITAR INICIALIZACIONES SIMULTANEAS
    private readonly SemaphoreSlim _bloqueo = new(1, 1);

    public ContextoBaseDatos(string ruta)
    {
        _ruta = ruta;
    }

    public async Task<SQLiteAsyncConnection> ObtenerDb()
    {
        // SI YA ESTA INICIALIZADA, REUTILIZARLA
        if (_db != null)
            return _db;

        await _bloqueo.WaitAsync();

        try
        {
            // COMPROBAR DE NUEVO DESPUES DEL BLOQUEO
            if (_db != null)
                return _db;

            SQLitePCL.Batteries_V2.Init();

            var conexion = new SQLiteAsyncConnection(_ruta);

            var banderas =
                CreateFlags.ImplicitPK |
                CreateFlags.AutoIncPK;

            // ============================================
            // TABLAS EXISTENTES
            // ============================================
            //
            // Se mantienen para conservar todos los
            // usuarios, cursos y tareas del dispositivo.
            //
            // CreateTableAsync no borra los registros
            // de las tablas que ya existen.
            // ============================================

            await conexion.CreateTableAsync<Usuario>(
                banderas);

            await conexion.CreateTableAsync<Curso>(
                banderas);

            await conexion.CreateTableAsync<Tarea>(
                banderas);

            // ============================================
            // NUEVA TABLA DE VINCULACION MYSQL - SQLITE
            // ============================================
            //
            // Relaciona el ID real de MySQL con
            // un ID de perfil local de SQLite.
            //
            // Los indices unicos declarados en
            // VinculoUsuarioRemoto evitan duplicar
            // relaciones entre cuentas y perfiles.
            //
            // Por ahora solo creamos la tabla.
            // NO vinculamos cuentas automaticamente.
            // ============================================

            await conexion.CreateTableAsync<VinculoUsuarioRemoto>(
                banderas);

            // ============================================
            // CONFIGURACION EXISTENTE DE USUARIOS
            // ============================================

            // LAS CUENTAS EXISTENTES NO DEBEN
            // CONSIDERARSE VERIFICADAS SIN CONFIRMACION
            await conexion.ExecuteAsync(
                "UPDATE \"Usuario\" " +
                "SET \"CorreoVerificado\" = 0 " +
                "WHERE \"CorreoVerificado\" IS NULL");

            // MANTENER CORREOS UNICOS EN SQLITE
            await conexion.CreateIndexAsync(
                "Usuario",
                "Correo",
                unique: true);

            // ============================================
            // FINALIZAR INICIALIZACION
            // ============================================

            // MARCAR COMO INICIALIZADA SOLO DESPUES
            // DE TERMINAR CORRECTAMENTE
            _db = conexion;

            return _db;
        }
        finally
        {
            _bloqueo.Release();
        }
    }
}
