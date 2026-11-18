
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaNuevaTarea : ContentPage, IQueryAttributable
{
    private readonly NuevaTareaVM _vm;
    private int _tareaId;
    private int _cursoId;

    public PaginaNuevaTarea(NuevaTareaVM vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // RECIBIR ID DE TAREA (EDICIÓN) O CURSO (NUEVA TAREA)
    public void ApplyQueryAttributes(
        IDictionary<string, object> query)
    {
        _tareaId = 0;
        _cursoId = 0;

        if (query.TryGetValue("id", out var valor) &&
            int.TryParse(valor?.ToString(), out int id) &&
            id > 0)
        {
            _tareaId = id;
        }

        if (query.TryGetValue("cursoId", out var cursoValor) &&
            int.TryParse(cursoValor?.ToString(), out int cursoId) &&
            cursoId > 0)
        {
            _cursoId = cursoId;
        }
    }

    // CARGAR FORMULARIO
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _vm.Cargar(_tareaId, _cursoId);
    }
}
