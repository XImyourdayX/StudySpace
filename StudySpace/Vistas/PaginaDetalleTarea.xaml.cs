
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaDetalleTarea : ContentPage, IQueryAttributable
{
    private readonly DetalleTareaVM _vm;
    private int _tareaId;

    public PaginaDetalleTarea(DetalleTareaVM vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = vm;
    }

    // RECIBIR EL ID DE LA TAREA
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

    // ACTUALIZAR DATOS AL ABRIR O REGRESAR
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_tareaId > 0)
            await _vm.Cargar(_tareaId);
    }

    // ABRIR FORMULARIO EN MODO EDICIÓN
    private async void OnEditarClicked(
        object sender,
        EventArgs e)
    {
        if (_vm.Tarea == null)
            return;

        await Shell.Current.GoToAsync(
            "nuevatarea",
            new Dictionary<string, object>
            {
                { "id", _vm.Tarea.Id }
            });
    }
}
