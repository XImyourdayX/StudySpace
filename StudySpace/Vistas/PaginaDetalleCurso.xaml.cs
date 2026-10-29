
using StudySpace.VistaModelos;

namespace StudySpace.Vistas;

public partial class PaginaDetalleCurso : ContentPage, IQueryAttributable
{
    private readonly DetalleCursoVM _vm;
    private int _cursoId;

    public PaginaDetalleCurso(DetalleCursoVM vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = vm;
    }

    // RECIBIR EL ID DEL CURSO
    public void ApplyQueryAttributes(
        IDictionary<string, object> query)
    {
        _cursoId = 0;

        if (query.TryGetValue("id", out var valor) &&
            int.TryParse(valor?.ToString(), out int id) &&
            id > 0)
        {
            _cursoId = id;
        }
    }

    // CARGAR INFORMACIÓN DEL CURSO
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_cursoId > 0)
            await _vm.Cargar(_cursoId);
    }
}
