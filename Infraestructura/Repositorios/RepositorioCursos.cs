using Dominio.Entidades;
using Aplicacion.Interfaces;
using Infraestructura.DBContext;

namespace Infraestructura.Repositorios;

public class RepositorioCursos : IRepositorioCursos
{
    private readonly ContextoBaseDatos _contexto;

    public RepositorioCursos(ContextoBaseDatos contexto) => _contexto = contexto;

    public async Task<List<Curso>> ObtenerPorUsuario(int usuarioId)
    {
        var db = await _contexto.ObtenerDb();
        return await db.Table<Curso>().Where(c => c.UsuarioId == usuarioId).ToListAsync();
    }

    public async Task<Curso?> ObtenerPorNombre(int usuarioId, string nombre)
    {
        var db = await _contexto.ObtenerDb();
        return await db.Table<Curso>()
            .Where(c => c.UsuarioId == usuarioId && c.Nombre == nombre).FirstOrDefaultAsync();
    }

    public async Task<int> ContarPorUsuario(int usuarioId)
    {
        var db = await _contexto.ObtenerDb();
        return await db.Table<Curso>().Where(c => c.UsuarioId == usuarioId).CountAsync();
    }

    public async Task Agregar(Curso curso)
    {
        var db = await _contexto.ObtenerDb();
        await db.InsertAsync(curso);
    }

    // Comprobacion atomica: no borrar cursos con tareas (pendientes o completadas).
    // La consulta tambien comprueba que el curso sea del usuario autenticado.
    public async Task<bool> EliminarSiNoTieneTareas(int usuarioId, int cursoId)
    {
        var db = await _contexto.ObtenerDb();

        int filasEliminadas = await db.ExecuteAsync(
            "DELETE FROM Curso " +
            "WHERE Id = ? AND UsuarioId = ? " +
            "AND NOT EXISTS (" +
            "SELECT 1 FROM Tarea WHERE CursoId = ?)",
            cursoId,
            usuarioId,
            cursoId);

        return filasEliminadas == 1;
    }
}
