using Dominio.Entidades;
using Aplicacion.Interfaces;
using Infraestructura.DBContext;

namespace Infraestructura.Repositorios;

public class RepositorioTareas : IRepositorioTareas
{
    private readonly ContextoBaseDatos _contexto;

    public RepositorioTareas(ContextoBaseDatos contexto) => _contexto = contexto;

    public async Task<List<Tarea>> ObtenerPorUsuario(int usuarioId)
    {
        var db = await _contexto.ObtenerDb();
        return await db.Table<Tarea>()
            .Where(t => t.UsuarioId == usuarioId)
            .OrderBy(t => t.FechaLimite)
            .ToListAsync();
    }

    public async Task<Tarea?> ObtenerPorId(int id)
    {
        var db = await _contexto.ObtenerDb();
        return await db.Table<Tarea>().Where(t => t.Id == id).FirstOrDefaultAsync();
    }

    public async Task Agregar(Tarea tarea)
    {
        var db = await _contexto.ObtenerDb();
        await db.InsertAsync(tarea);
    }

    public async Task Actualizar(Tarea tarea)
    {
        var db = await _contexto.ObtenerDb();
        await db.UpdateAsync(tarea);
    }

    public async Task Eliminar(int id)
    {
        var db = await _contexto.ObtenerDb();
        await db.DeleteAsync<Tarea>(id);
    }
}
