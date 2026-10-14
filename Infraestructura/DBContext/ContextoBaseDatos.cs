using Dominio.Entidades;
using SQLite;

namespace Infraestructura.DBContext;

public class ContextoBaseDatos
{
    private readonly string _ruta;
    private SQLiteAsyncConnection? _db;

    public ContextoBaseDatos(string ruta) => _ruta = ruta;

    public async Task<SQLiteAsyncConnection> ObtenerDb()
    {
        if (_db != null) return _db;

        SQLitePCL.Batteries_V2.Init();
        _db = new SQLiteAsyncConnection(_ruta);

        var banderas = CreateFlags.ImplicitPK | CreateFlags.AutoIncPK;
        await _db.CreateTableAsync<Usuario>(banderas);
        await _db.CreateTableAsync<Curso>(banderas);
        await _db.CreateTableAsync<Tarea>(banderas);
        await _db.CreateIndexAsync("Usuario", "Correo", unique: true);
        return _db;
    }
}