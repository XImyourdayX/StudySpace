
using System.Collections.ObjectModel;
using System.Globalization;
using Aplicacion.CasosDeUso;
using Aplicacion.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StudySpace.VistaModelos;

public partial class CalendarioVM : ObservableObject
{
    private readonly ServicioTareas _tareas;
    private List<TareaDTO> _todasLasTareas = new();

    public ObservableCollection<DiaCalendario> Dias { get; } = new();
    public ObservableCollection<TareaDTO> TareasDelDia { get; } = new();

    private readonly CultureInfo _cultura =
        new("es-PE");

    [ObservableProperty]
    private DateTime mesActual = new(
        DateTime.Today.Year,
        DateTime.Today.Month,
        1);

    [ObservableProperty]
    private DateTime fechaSeleccionada = DateTime.Today;

    [ObservableProperty]
    private string tituloMes = "";

    [ObservableProperty]
    private string tituloFecha = "";

    [ObservableProperty]
    private bool sinTareas = true;

    public CalendarioVM(ServicioTareas tareas)
    {
        _tareas = tareas;
    }

    // CARGAR CALENDARIO
    public async Task Cargar()
    {
        _todasLasTareas = await _tareas.ObtenerTareas();

        GenerarCalendario();
    }

    // GENERAR DÍAS DEL MES
    private void GenerarCalendario()
    {
        Dias.Clear();

        TituloMes = _cultura.TextInfo.ToTitleCase(
            MesActual.ToString("MMMM yyyy", _cultura));

        var primerDia = new DateTime(
            MesActual.Year,
            MesActual.Month,
            1);

        int desplazamiento =
            ((int)primerDia.DayOfWeek + 6) % 7;

        int diasDelMes = DateTime.DaysInMonth(
            MesActual.Year,
            MesActual.Month);

        // ESPACIOS ANTES DEL PRIMER DÍA
        for (int i = 0; i < desplazamiento; i++)
        {
            Dias.Add(new DiaCalendario
            {
                EsVisible = false
            });
        }

        // DÍAS DEL MES
        for (int dia = 1; dia <= diasDelMes; dia++)
        {
            var fecha = new DateTime(
                MesActual.Year,
                MesActual.Month,
                dia);

            var tareasFecha = _todasLasTareas
                .Where(t => t.FechaLimite.Date == fecha)
                .ToList();

            Dias.Add(new DiaCalendario
            {
                Fecha = fecha,
                Numero = dia.ToString(),
                EsVisible = true,
                EsHoy = fecha == DateTime.Today,
                EstaSeleccionado = fecha == FechaSeleccionada,
                TieneTareas = tareasFecha.Count > 0,
                ColorIndicador = tareasFecha.Any(
                    t => !t.Completada && t.EstaVencida)
                    ? "#EF4444"
                    : tareasFecha.Any(t => !t.Completada)
                        ? "#5544E9"
                        : "#22B981"
            });
        }

        CargarTareasDelDia();
    }

    // MOSTRAR TAREAS DEL DÍA SELECCIONADO
    private void CargarTareasDelDia()
    {
        TituloFecha = _cultura.TextInfo.ToTitleCase(
            FechaSeleccionada.ToString(
                "dddd, d 'de' MMMM", _cultura));

        TareasDelDia.Clear();

        var lista = _todasLasTareas
            .Where(t => t.FechaLimite.Date ==
                        FechaSeleccionada.Date)
            .OrderBy(t => t.Completada)
            .ThenBy(t => t.FechaLimite);

        foreach (var tarea in lista)
            TareasDelDia.Add(tarea);

        SinTareas = TareasDelDia.Count == 0;
    }

    // SELECCIONAR UN DÍA
    [RelayCommand]
    private void SeleccionarDia(DiaCalendario? dia)
    {
        if (dia == null || !dia.EsVisible)
            return;

        FechaSeleccionada = dia.Fecha;

        GenerarCalendario();
    }

    // MES ANTERIOR
    [RelayCommand]
    private void MesAnterior()
    {
        MesActual = MesActual.AddMonths(-1);
        FechaSeleccionada = MesActual;

        GenerarCalendario();
    }

    // MES SIGUIENTE
    [RelayCommand]
    private void MesSiguiente()
    {
        MesActual = MesActual.AddMonths(1);
        FechaSeleccionada = MesActual;

        GenerarCalendario();
    }

    // VOLVER AL DÍA DE HOY
    [RelayCommand]
    private void IrAHoy()
    {
        FechaSeleccionada = DateTime.Today;

        MesActual = new DateTime(
            FechaSeleccionada.Year,
            FechaSeleccionada.Month,
            1);

        GenerarCalendario();
    }

    // ABRIR DETALLE DE TAREA
    [RelayCommand]
    private async Task AbrirTarea(TareaDTO? tarea)
    {
        if (tarea == null)
            return;

        await Shell.Current.GoToAsync(
            "detalletarea",
            new Dictionary<string, object>
            {
                { "id", tarea.Id }
            });
    }

    // CREAR NUEVA TAREA
    [RelayCommand]
    private async Task NuevaTarea()
    {
        await Shell.Current.GoToAsync("nuevatarea");
    }
}

// REPRESENTA UN DÍA DEL CALENDARIO
public class DiaCalendario
{
    public DateTime Fecha { get; set; }

    public string Numero { get; set; } = "";

    public bool EsVisible { get; set; }

    public bool EsHoy { get; set; }

    public bool EstaSeleccionado { get; set; }

    public bool TieneTareas { get; set; }

    public string ColorIndicador { get; set; } = "#5544E9";
}
