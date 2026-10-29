
using Aplicacion.DTOs;
using Aplicacion.Interfaces;
using Aplicacion.Mappings;
using Dominio.Entidades;

namespace Aplicacion.CasosDeUso;

public class ServicioTareas
{
    private readonly IRepositorioTareas _tareas;
    private readonly IRepositorioCursos _cursos;
    private readonly ServicioAutenticacion _auth;

    public ServicioTareas(
        IRepositorioTareas tareas,
        IRepositorioCursos cursos,
        ServicioAutenticacion auth)
    {
        _tareas = tareas;
        _cursos = cursos;
        _auth = auth;
    }

    // LISTAR TAREAS
    public async Task<List<TareaDTO>> ObtenerTareas()
    {
        if (_auth.UsuarioActual == null)
            return new();

        var usuarioId = _auth.UsuarioActual.Id;

        var cursos = (await _cursos.ObtenerPorUsuario(usuarioId))
            .ToDictionary(c => c.Id);

        var lista = await _tareas.ObtenerPorUsuario(usuarioId);

        return lista.Select(t =>
        {
            cursos.TryGetValue(t.CursoId, out var c);

            return t.ADto(
                c?.Nombre ?? "",
                c?.Color ?? "#304BFF");

        }).ToList();
    }

    // OBTENER DETALLE DE UNA TAREA
    public async Task<TareaDTO?> ObtenerPorId(int id)
    {
        if (_auth.UsuarioActual == null)
            return null;

        var tarea = await _tareas.ObtenerPorId(id);

        if (tarea == null ||
            tarea.UsuarioId != _auth.UsuarioActual.Id)
            return null;

        var cursos = await _cursos.ObtenerPorUsuario(
            _auth.UsuarioActual.Id);

        var curso = cursos.FirstOrDefault(
            c => c.Id == tarea.CursoId);

        return tarea.ADto(
            curso?.Nombre ?? "",
            curso?.Color ?? "#304BFF");
    }

    // AGREGAR TAREA - MÉTODO ORIGINAL
    public async Task<(bool Exito, string Mensaje)> AgregarTarea(
        string titulo,
        string descripcion,
        int cursoId,
        DateTime fechaLimite,
        string prioridad,
        int minutosAviso)
    {
        var resultado = await AgregarTareaConId(
            titulo,
            descripcion,
            cursoId,
            fechaLimite,
            prioridad,
            minutosAviso);

        return (resultado.Exito, resultado.Mensaje);
    }

    // HU05 - AGREGAR TAREA Y DEVOLVER SU ID
    public async Task<(bool Exito, string Mensaje, int TareaId)>
        AgregarTareaConId(
            string titulo,
            string descripcion,
            int cursoId,
            DateTime fechaLimite,
            string prioridad,
            int minutosAviso)
    {
        if (_auth.UsuarioActual == null)
            return (false, "No hay sesión iniciada.", 0);

        titulo = titulo.Trim();

        if (titulo == "")
            return (false, "Escribe el título de la tarea.", 0);

        if (cursoId == 0)
            return (false, "Elige un curso.", 0);

        if (fechaLimite < DateTime.Now)
            return (false, "La fecha límite no puede ser pasada.", 0);

        var cursos = await _cursos.ObtenerPorUsuario(
            _auth.UsuarioActual.Id);

        if (!cursos.Any(c => c.Id == cursoId))
            return (false, "El curso seleccionado no es válido.", 0);

        if (prioridad != "Baja" &&
            prioridad != "Media" &&
            prioridad != "Alta")
        {
            return (false, "La prioridad no es válida.", 0);
        }

        if (minutosAviso < 0)
            return (false, "El recordatorio no es válido.", 0);

        var tarea = new Tarea
        {
            UsuarioId = _auth.UsuarioActual.Id,
            CursoId = cursoId,
            Titulo = titulo,
            Descripcion = descripcion?.Trim() ?? "",
            FechaLimite = fechaLimite,
            Prioridad = prioridad,
            MinutosAviso = minutosAviso
        };

        // GUARDAR EN SQLITE
        await _tareas.Agregar(tarea);

        // SQLITE ASIGNA EL ID A LA ENTIDAD
        return (true, "", tarea.Id);
    }

    // EDITAR TAREA
    public async Task<(bool Exito, string Mensaje)> EditarTarea(
        int id,
        string titulo,
        string descripcion,
        int cursoId,
        DateTime fechaLimite,
        string prioridad,
        int minutosAviso)
    {
        if (_auth.UsuarioActual == null)
            return (false, "No hay sesión iniciada.");

        var tarea = await _tareas.ObtenerPorId(id);

        if (tarea == null ||
            tarea.UsuarioId != _auth.UsuarioActual.Id)
            return (false, "No se encontró la tarea.");

        titulo = titulo.Trim();

        if (titulo == "")
            return (false, "Escribe el título de la tarea.");

        if (cursoId == 0)
            return (false, "Elige un curso.");

        var cursos = await _cursos.ObtenerPorUsuario(
            _auth.UsuarioActual.Id);

        if (!cursos.Any(c => c.Id == cursoId))
            return (false, "El curso seleccionado no es válido.");

        if (fechaLimite < DateTime.Now &&
            fechaLimite != tarea.FechaLimite)
        {
            return (false,
                "La nueva fecha límite no puede ser pasada.");
        }

        tarea.Titulo = titulo;
        tarea.Descripcion = descripcion?.Trim() ?? "";
        tarea.CursoId = cursoId;
        tarea.FechaLimite = fechaLimite;
        tarea.Prioridad = prioridad;
        tarea.MinutosAviso = minutosAviso;

        await _tareas.Actualizar(tarea);

        return (true, "");
    }

    // MARCAR O DESMARCAR COMO COMPLETADA
    public async Task AlternarCompletada(int id)
    {
        if (_auth.UsuarioActual == null)
            return;

        var tarea = await _tareas.ObtenerPorId(id);

        if (tarea == null ||
            tarea.UsuarioId != _auth.UsuarioActual.Id)
            return;

        tarea.Completada = !tarea.Completada;

        await _tareas.Actualizar(tarea);
    }

    // ELIMINAR TAREA
    public async Task<(bool Exito, string Mensaje)>
        EliminarTarea(int id)
    {
        if (_auth.UsuarioActual == null)
            return (false, "No hay sesión iniciada.");

        var tarea = await _tareas.ObtenerPorId(id);

        if (tarea == null ||
            tarea.UsuarioId != _auth.UsuarioActual.Id)
            return (false, "No se encontró la tarea.");

        await _tareas.Eliminar(id);

        return (true, "");
    }
}
