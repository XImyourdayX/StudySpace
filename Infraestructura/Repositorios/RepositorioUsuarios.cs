using Dominio.Entidades;
using Aplicacion.Interfaces;
using Infraestructura.DBContext;

namespace Infraestructura.Repositorios;

public class RepositorioUsuarios : IRepositorioUsuarios
{
    private readonly ContextoBaseDatos _contexto;

    public RepositorioUsuarios(ContextoBaseDatos contexto) => _contexto = contexto;

    public async Task<Usuario?> ObtenerPorCorreo(string correo)
    {
        var db = await _contexto.ObtenerDb();
        return await db.Table<Usuario>().Where(u => u.Correo == correo).FirstOrDefaultAsync();
    }

    public async Task<Usuario?> ObtenerPorId(int id)
    {
        var db = await _contexto.ObtenerDb();
        return await db.Table<Usuario>().Where(u => u.Id == id).FirstOrDefaultAsync();
    }

    public async Task Agregar(Usuario usuario)
    {
        var db = await _contexto.ObtenerDb();
        await db.InsertAsync(usuario);
    }
}
