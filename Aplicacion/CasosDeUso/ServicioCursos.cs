using Aplicacion.DTOs;
using Aplicacion.Interfaces;
using Aplicacion.Mappings;
using Dominio.Entidades;

namespace Aplicacion.CasosDeUso;

public class ServicioCursos
{
    private static readonly string[] Paleta =
        { "#304BFF", "#5544E9", "#22B981", "#F59E0B", "#EF4444", "#0EA5E9" };

    private readonly IRepositorioCursos _cursos;
    private readonly IRepositorioTareas _tareas;
    private readonly ServicioAutenticacion _auth;

    public ServicioCursos(IRepositorioCursos cursos, IRepositorioTareas tareas, ServicioAutenticacion auth)
    {
        _cursos = cursos;
        _tareas = tareas;
        _auth = auth;
    }

    public async Task<List<CursoDTO>> ObtenerCursos()
    {
        if (_auth.UsuarioActual == null) return new();

        var usuarioId = _auth.UsuarioActual.Id;
        var lista = await _cursos.ObtenerPorUsuario(usuarioId);
        var tareas = await _tareas.ObtenerPorUsuario(usuarioId);

        return lista.Select(c =>
        {
            var dto = c.ADto();
            dto.TareasPendientes = tareas.Count(t => t.CursoId == c.Id && !t.Completada);
            return dto;
        }).ToList();
    }

    public async Task<(bool Exito, string Mensaje)> AgregarCurso(string nombre)
    {
        nombre = nombre.Trim();
        if (nombre == "") return (false, "Escribe el nombre del curso.");
        if (_auth.UsuarioActual == null) return (false, "No hay sesión iniciada.");

        var usuarioId = _auth.UsuarioActual.Id;
        if (await _cursos.ObtenerPorNombre(usuarioId, nombre) != null)
            return (false, "Ya tienes un curso con ese nombre.");

        var cantidad = await _cursos.ContarPorUsuario(usuarioId);
        await _cursos.Agregar(new Curso
        {
            UsuarioId = usuarioId,
            Nombre = nombre,
            Color = Paleta[cantidad % Paleta.Length]
        });
        return (true, "");
    }

    public async Task<(bool Exito, string Mensaje)> EliminarCurso(int cursoId)
    {
        if (cursoId <= 0)
            return (false, "El curso seleccionado no es valido.");

        if (_auth.UsuarioActual == null)
            return (false, "No hay sesion iniciada.");

        int usuarioId = _auth.UsuarioActual.Id;
        var cursos = await _cursos.ObtenerPorUsuario(usuarioId);
        var curso = cursos.FirstOrDefault(c => c.Id == cursoId);

        if (curso == null)
            return (false, "El curso no existe o no pertenece a tu cuenta.");

        // Protege todas las tareas del curso, incluso las completadas.
        var tareas = await _tareas.ObtenerPorUsuario(usuarioId);

        if (tareas.Any(t => t.CursoId == cursoId))
            return (false,
                "Este curso tiene tareas registradas. " +
                "Eliminalas o reasignalas antes de borrar el curso.");

        // Segunda proteccion dentro de SQLite, en una sola sentencia DELETE.
        bool eliminado = await _cursos.EliminarSiNoTieneTareas(usuarioId, cursoId);

        return eliminado
            ? (true, "Curso eliminado correctamente.")
            : (false, "No se pudo eliminar el curso. Verifica que no contenga tareas.");
    }
}
