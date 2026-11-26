using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace StudySpace.Vistas;

// Componente exclusivamente visual. No modifica los datos ni el ViewModel.
public sealed class GraficoReporte : GraphicsView, IDrawable
{
    public static readonly BindableProperty TipoProperty = BindableProperty.Create(nameof(Tipo), typeof(string), typeof(GraficoReporte), "Cumplimiento", propertyChanged: Redibujar);
    public static readonly BindableProperty CompletadasProperty = BindableProperty.Create(nameof(Completadas), typeof(int), typeof(GraficoReporte), 0, propertyChanged: Redibujar);
    public static readonly BindableProperty PendientesProperty = BindableProperty.Create(nameof(Pendientes), typeof(int), typeof(GraficoReporte), 0, propertyChanged: Redibujar);
    public static readonly BindableProperty VencidasProperty = BindableProperty.Create(nameof(Vencidas), typeof(int), typeof(GraficoReporte), 0, propertyChanged: Redibujar);
    public static readonly BindableProperty FraccionProperty = BindableProperty.Create(nameof(Fraccion), typeof(double), typeof(GraficoReporte), 0d, propertyChanged: Redibujar);
    public string Tipo { get => (string)GetValue(TipoProperty); set => SetValue(TipoProperty, value); }
    public int Completadas { get => (int)GetValue(CompletadasProperty); set => SetValue(CompletadasProperty, value); }
    public int Pendientes { get => (int)GetValue(PendientesProperty); set => SetValue(PendientesProperty, value); }
    public int Vencidas { get => (int)GetValue(VencidasProperty); set => SetValue(VencidasProperty, value); }
    public double Fraccion { get => (double)GetValue(FraccionProperty); set => SetValue(FraccionProperty, value); }
    public GraficoReporte() { Drawable = this; InputTransparent = true; }
    private static void Redibujar(BindableObject b, object oldValue, object newValue) => ((GraficoReporte)b).Invalidate();
    private static readonly Color Violeta = Color.FromArgb("#AB65F5");
    private static readonly Color Verde = Color.FromArgb("#39C994");
    private static readonly Color Ambar = Color.FromArgb("#F4B443");
    private static readonly Color Rosa = Color.FromArgb("#F36598");
    public void Draw(ICanvas canvas, RectF rect)
    {
        float w = rect.Width, h = rect.Height;
        if (w <= 0 || h <= 0) return;
        var fondo = Color.FromArgb("#776A91").WithAlpha(0.38f);
        double total = (double)Math.Max(0, Completadas) + Math.Max(0, Pendientes) + Math.Max(0, Vencidas);
        double[] valores = { Math.Max(0, Completadas), Math.Max(0, Pendientes), Math.Max(0, Vencidas) };
        Color[] colores = { Verde, Ambar, Rosa };
        if (Tipo == "Segmentos" || Tipo == "Curso")
        {
            canvas.FillColor = fondo;
            canvas.FillRoundedRectangle(0, 0, w, h, h / 2);
            if (Tipo == "Curso")
            {
                float ancho = (float)Math.Clamp(Fraccion, 0d, 1d) * w;
                if (ancho > 0) { canvas.FillColor = Violeta; canvas.FillRoundedRectangle(0, 0, ancho, h, Math.Min(h / 2, ancho / 2)); }
            }
            else if (total > 0)
            {
                float x = 0;
                for (int i = 0; i < valores.Length; i++)
                {
                    float ancho = (float)(valores[i] / total * w);
                    if (ancho > 0) { canvas.FillColor = colores[i]; canvas.FillRectangle(x, 0, ancho, h); x += ancho; }
                }
            }
            return;
        }
        float grosor = 11, radio = Math.Max(0, (Math.Min(w, h) - grosor - 4) / 2);
        float cx = w / 2, cy = h / 2;
        canvas.StrokeSize = grosor;
        canvas.StrokeColor = fondo;
        canvas.DrawCircle(cx, cy, radio);
        if (total <= 0 || radio <= 0) return;
        // Dibujar arcos como segmentos de trayectoria: misma geometría en todas las plataformas.
        double inicio = -Math.PI / 2;
        int n = Tipo == "Cumplimiento" ? 1 : 3;
        for (int i = 0; i < n; i++)
        {
            double angulo = valores[i] / total * Math.PI * 2;
            if (angulo <= 0) continue;
            canvas.StrokeColor = Tipo == "Cumplimiento" ? Violeta : colores[i];
            var path = new PathF();
            int pasos = Math.Max(2, (int)Math.Ceiling(angulo * 32));
            for (int j = 0; j <= pasos; j++)
            {
                double a = inicio + angulo * j / pasos;
                float x = cx + radio * (float)Math.Cos(a), y = cy + radio * (float)Math.Sin(a);
                if (j == 0) path.MoveTo(x, y); else path.LineTo(x, y);
            }
            canvas.DrawPath(path);
            inicio += angulo;
        }
    }
}
