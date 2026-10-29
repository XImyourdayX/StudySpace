
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaNuevaTarea : ContentPage, IQueryAttributable
{
    private readonly NuevaTareaVM _vm;
    private int _tareaId;

    public PaginaNuevaTarea(NuevaTareaVM vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // RECIBIR ID DE LA TAREA QUE SE EDITARÁ
    public void ApplyQueryAttributes(
        IDictionary<string, object> query)
    {
        _tareaId = 0;

        if (query.TryGetValue("id", out var valor) &&
            int.TryParse(valor?.ToString(), out int id) &&
            id > 0)
        {
            _tareaId = id;
        }
    }

    // CARGAR FORMULARIO
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _vm.Cargar(_tareaId);
    }
}
