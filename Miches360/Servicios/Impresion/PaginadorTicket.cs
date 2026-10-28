using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Miches360.Servicios.Impresion;

internal sealed class PaginadorTicket : DocumentPaginator
{
    private readonly FrameworkElement _contenido;
    private readonly Point _origen;
    private readonly double _altoUtil;
    private readonly int _paginas;
    private Size _tamanoPagina;
    private DocumentPage? _paginaUnica;

    public PaginadorTicket(FrameworkElement contenido, double anchoPapel, Point origen, double altoUtil)
    {
        _contenido = contenido;
        _origen = origen;
        _altoUtil = Math.Max(altoUtil, 50);

        var alto = contenido.ActualHeight;
        _paginas = Math.Max(1, (int)Math.Ceiling(alto / _altoUtil - 0.001));
        var altoPagina = _paginas == 1 ? alto : _altoUtil;
        _tamanoPagina = new Size(anchoPapel, origen.Y + altoPagina);
    }

    public override bool IsPageCountValid => true;

    public override int PageCount => _paginas;

    public override Size PageSize
    {
        get => _tamanoPagina;
        set => _tamanoPagina = value;
    }

    public override IDocumentPaginatorSource? Source => null;

    public override DocumentPage GetPage(int pageNumber)
    {
        var areaUtil = new Rect(_origen.X, _origen.Y, _contenido.ActualWidth, _tamanoPagina.Height - _origen.Y);

        if (_paginas == 1)
        {
            if (_paginaUnica is null)
            {
                var pagina = new ContainerVisual { Transform = new TranslateTransform(_origen.X, _origen.Y) };
                pagina.Children.Add(_contenido);
                _paginaUnica = new DocumentPage(pagina, _tamanoPagina, new Rect(_tamanoPagina), areaUtil);
            }

            return _paginaUnica;
        }

        var desplazamiento = pageNumber * _altoUtil;
        var tamano = new Size(_contenido.ActualWidth, _contenido.ActualHeight);
        var pincel = new VisualBrush(_contenido)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(tamano),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(tamano)
        };

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(areaUtil));
            dc.PushTransform(new TranslateTransform(_origen.X, _origen.Y - desplazamiento));
            dc.DrawRectangle(pincel, null, new Rect(tamano));
            dc.Pop();
            dc.Pop();
        }

        return new DocumentPage(visual, _tamanoPagina, new Rect(_tamanoPagina), areaUtil);
    }
}
