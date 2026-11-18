using System.Collections.ObjectModel;
using System.Globalization;
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StudySpace.VistaModelos;

// HU14: un informe describe entregas que vencen en el periodo elegido.
// La cantidad de tareas finalizadas en ese mes se calcula por FechaCompletada.
public partial class ReportesVM : ObservableObject
{
    private readonly ServicioTareas _servicioTareas;
    private readonly ServicioCursos _servicioCursos;
    private readonly CultureInfo _cultura = CultureInfo.GetCultureInfo("es-PE");
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private List<TareaDTO> _todas = new();
    private readonly Dictionary<string, int> _idsPorCurso = new(StringComparer.Ordinal);

    public ObservableCollection<string> CursosDisponibles { get; } = new();
    public ObservableCollection<ResumenCursoReporte> ResumenPorCurso { get; } = new();

    [ObservableProperty] private string tituloMes = "";
    [ObservableProperty] private string? cursoSeleccionado;
    [ObservableProperty] private int totalEntregas;
    [ObservableProperty] private int completadas;
    [ObservableProperty] private int pendientes;
    [ObservableProperty] private int vencidas;
    [ObservableProperty] private int finalizadasDuranteMes;
    [ObservableProperty] private int completadasSinFecha;
    [ObservableProperty] private int porcentajeCumplimiento;
    [ObservableProperty] private double progresoCumplimiento;
    [ObservableProperty] private bool sinEntregas;
    [ObservableProperty] private bool estaCargando;
    [ObservableProperty] private string mensaje = "";

    public ReportesVM(ServicioTareas servicioTareas, ServicioCursos servicioCursos)
    {
        _servicioTareas = servicioTareas;
        _servicioCursos = servicioCursos;
    }

    public async Task Cargar()
    {
        if (EstaCargando) return;
        EstaCargando = true;
        try
        {
            // El servicio ya restringe los datos a la cuenta activa.
            _todas = await _servicioTareas.ObtenerTareas();
            var cursos = await _servicioCursos.ObtenerCursos();
            var seleccionadoAnterior = CursoSeleccionado;

            _idsPorCurso.Clear();
            CursosDisponibles.Clear();
            CursosDisponibles.Add("Todos los cursos");
            foreach (var curso in cursos.OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase))
            {
                // ServicioCursos ya impide nombres duplicados por usuario.
                var texto = curso.Nombre;
                _idsPorCurso[texto] = curso.Id;
                CursosDisponibles.Add(texto);
            }

            CursoSeleccionado = seleccionadoAnterior is not null &&
                CursosDisponibles.Contains(seleccionadoAnterior)
                ? seleccionadoAnterior
                : "Todos los cursos";
            ActualizarResumen();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HU14: {ex}");
            _todas = new();
            ActualizarResumen();
            Mensaje = "No se pudieron cargar los reportes. Inténtalo de nuevo.";
        }
        finally
        {
            EstaCargando = false;
        }
    }

    partial void OnCursoSeleccionadoChanged(string? value) => ActualizarResumen();

    [RelayCommand]
    private void MesAnterior()
    {
        _mes = _mes.AddMonths(-1);
        ActualizarResumen();
    }

    [RelayCommand]
    private void MesSiguiente()
    {
        _mes = _mes.AddMonths(1);
        ActualizarResumen();
    }

    private void ActualizarResumen()
    {
        var nombreMes = _mes.ToString("MMMM yyyy", _cultura);
        TituloMes = char.ToUpper(nombreMes[0], _cultura) + nombreMes[1..];
        var siguienteMes = _mes.AddMonths(1);

        var tareasFiltradasPorCurso = _todas.AsEnumerable();
        if (CursoSeleccionado is { } seleccionado &&
            _idsPorCurso.TryGetValue(seleccionado, out int cursoId))
            tareasFiltradasPorCurso = tareasFiltradasPorCurso.Where(t => t.CursoId == cursoId);

        // Indicador 1: las entregas cuyo VENCIMIENTO cae en el mes.
        var entregas = tareasFiltradasPorCurso
            .Where(t => t.FechaLimite >= _mes && t.FechaLimite < siguienteMes)
            .ToList();
        var hoy = DateTime.Now;
        TotalEntregas = entregas.Count;
        Completadas = entregas.Count(t => t.Completada);
        Vencidas = entregas.Count(t => !t.Completada && t.FechaLimite < hoy);
        Pendientes = TotalEntregas - Completadas - Vencidas;
        CompletadasSinFecha = entregas.Count(t => t.Completada && t.FechaCompletada is null);
        ProgresoCumplimiento = TotalEntregas == 0 ? 0d : (double)Completadas / TotalEntregas;
        PorcentajeCumplimiento = (int)Math.Round(ProgresoCumplimiento * 100,
            MidpointRounding.AwayFromZero);
        SinEntregas = TotalEntregas == 0;

        // Indicador 2: tareas FINALIZADAS en el mes, incluso si vencían en otro.
        // Las finalizadas antes de agregar FechaCompletada no se pueden reconstruir.
        FinalizadasDuranteMes = tareasFiltradasPorCurso.Count(t =>
            t.Completada && t.FechaCompletada is { } fecha &&
            fecha >= _mes && fecha < siguienteMes);

        Mensaje = SinEntregas
            ? "No hay tareas con fecha de entrega en este mes."
            : "El porcentaje corresponde a las tareas cuya entrega vence este mes.";

        ResumenPorCurso.Clear();
        foreach (var grupo in entregas.GroupBy(t => new { t.CursoId, t.CursoNombre })
                     .OrderBy(g => g.Key.CursoNombre, StringComparer.CurrentCultureIgnoreCase))
        {
            var total = grupo.Count();
            var hechas = grupo.Count(t => t.Completada);
            ResumenPorCurso.Add(new ResumenCursoReporte
            {
                Nombre = string.IsNullOrWhiteSpace(grupo.Key.CursoNombre) ? "Sin curso" : grupo.Key.CursoNombre,
                Total = total,
                Completadas = hechas,
                Porcentaje = (int)Math.Round((double)hechas / total * 100,
                    MidpointRounding.AwayFromZero),
                Progreso = (double)hechas / total
            });
        }
    }
}

public sealed class ResumenCursoReporte
{
    public string Nombre { get; init; } = "";
    public int Total { get; init; }
    public int Completadas { get; init; }
    public int Porcentaje { get; init; }
    public double Progreso { get; init; }
    public string TextoAvance => $"{Completadas} de {Total} tareas completadas";
    public string TextoPorcentaje => $"{Porcentaje}%";
}
