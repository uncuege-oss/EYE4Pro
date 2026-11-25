using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Kst.Acoustics.App.Terrain.Rendering;

namespace Kst.Acoustics.App.Terrain.Views;

/// <summary>M60 orthographic viewport. The UI presents a cached image; a single worker
/// renders the latest requested camera without queuing intermediate pointer positions.</summary>
public sealed class Arazi3DControl : Control, IDisposable
{
    private sealed record Frame(TerrainMesh Mesh, int[]? Colors, TerrainCamera Camera,
        Size View, int Width, int Height, bool Heat);
    private readonly TerrainRasterizer _raster = new();
    private TerrainMesh? _mesh;
    private int[]? _colors;
    private bool _heat, _disposed, _running, _rotating;
    private IPointer? _pointer;
    private Point _last;
    private TerrainCamera _camera = TerrainCamera.Home;
    private Frame? _requested, _shown, _failed;
    private string? _error;
    private CancellationTokenSource? _active;
    private WriteableBitmap? _image;
    private double _scale;
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(12,22,36));

    internal void Scene(TerrainMesh? mesh, int[]? colors, bool heat, bool reset = false)
    {
        if (reset) _camera = TerrainCamera.Home;
        if (!reset && ReferenceEquals(mesh,_mesh) && ReferenceEquals(colors,_colors) && heat==_heat) return;
        _active?.Cancel();
        _mesh=mesh; _colors=colors; _heat=heat;
        _shown=_requested=_failed=null; _error=null;
        InvalidateVisual();
    }
    internal event Action<TerrainCamera>? CameraMoved;
    internal bool ShowColorKey { get; set; } = true;
    internal void MatchCamera(TerrainCamera camera) { _camera=camera; InvalidateVisual(); }
    public void ResetViewport() { MatchCamera(TerrainCamera.Home); CameraMoved?.Invoke(_camera); }

    public override void Render(DrawingContext ctx)
    {
        var view=Bounds.Size;
        var rect=new Rect(view);
        ctx.FillRectangle(BackgroundBrush,rect);
        if (_disposed || view.Width<=1 || view.Height<=1) return;
        if (_mesh==null || _mesh.Faces.Length==0) { Label(ctx,"Önce arazi yükleyin.",12,12); return; }
        double dpi=TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        // Only the pixel resolution is reduced during a drag. No triangle is dropped.
        double factor=Math.Min(dpi*(_pointer==null?1:.7),
            Math.Sqrt((_pointer==null?2_400_000:480_000)/(view.Width*view.Height)));
        _requested=new(_mesh,_colors,_camera,view,Math.Max(1,(int)Math.Round(view.Width*factor)),
            Math.Max(1,(int)Math.Round(view.Height*factor)),_heat);
        if (!_running && _requested!=_shown && _requested!=_failed) DrawAsync(_requested);
        if (_shown!=null && _image!=null)
        {
            ctx.DrawImage(_image,rect);
            DrawKey(ctx,rect,_shown);
        }
        else Label(ctx,_error ?? "3B arazi hazırlanıyor…",12,12);
        Label(ctx,"Sağ tuş: döndür  ·  Sol tuş: kaydır  ·  Tekerlek: yakınlaştır",12,view.Height-23);
    }

    private async void DrawAsync(Frame frame)
    {
        _running=true;
        using var stop=new CancellationTokenSource();
        _active=stop;
        try
        {
            double scale=await Task.Run(()=>_raster.Draw(frame.Mesh,frame.Colors,frame.Camera,frame.Width,
                frame.Height,frame.View.Width,frame.View.Height,stop.Token),stop.Token);
            if (_disposed || stop.IsCancellationRequested || !IsEffectivelyVisible ||
                !ReferenceEquals(frame.Mesh,_mesh) || !ReferenceEquals(frame.Colors,_colors)) return;
            var size=new PixelSize(frame.Width,frame.Height);
            if (_image?.PixelSize!=size)
            {
                _image?.Dispose();
                _image=new WriteableBitmap(size,new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Premul);
            }
            using (var fb=_image.Lock())
            {
                if(fb.RowBytes==size.Width*4) Marshal.Copy(_raster.Pixels,0,fb.Address,_raster.Pixels.Length);
                else for(int y=0;y<size.Height;y++)
                    Marshal.Copy(_raster.Pixels,y*size.Width,fb.Address+y*fb.RowBytes,size.Width);
            }
            _scale=scale; _shown=frame; _failed=null; _error=null;
        }
        catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
        catch(Exception ex) { _failed=frame; _error="3B görünüm oluşturulamadı: "+ex.Message; }
        finally
        {
            _running=false; _active=null;
            if(!_disposed && IsEffectivelyVisible) InvalidateVisual();
        }
    }
    private void DrawKey(DrawingContext ctx, Rect rect, Frame frame)
    {
        double unit=Math.Pow(10,Math.Floor(Math.Log10(100/_scale)));
        double v=100/_scale/unit;
        double metres=(v>=5?5:v>=2?2:1)*unit;
        double y=rect.Height-37;
        var pen=new Pen(Brushes.White,2);
        ctx.DrawLine(pen,new(16,y),new(16+metres*_scale,y));
        Label(ctx,$"{metres:G3} m",16,y-19);
        double x=rect.Width-183;
        if (x<210 || !ShowColorKey) return;
        Label(ctx,frame.Heat?"Dolgu                     Yarma":"Arazi kotu",x,y-21);
        for(int i=0;i<32;i++)
        {
            double t=i/31d;
            int rgb=frame.Heat?TerrainMesh.Heat(t):TerrainMesh.Elevation(t);
            ctx.FillRectangle(new SolidColorBrush(Color.FromUInt32((uint)rgb)),new Rect(x+i*5,y,5,7));
        }
        Label(ctx,frame.Heat?"Mavi: dolgu · kırmızı: yarma":$"{frame.Mesh.MinZ:N1} – {frame.Mesh.MaxZ:N1} m",x,y+9);
    }
    private static void Label(DrawingContext ctx,string text,double x,double y) =>
        ctx.DrawText(new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,
            Typeface.Default,11,Brushes.White),new(x,y));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var p=e.GetCurrentPoint(this);
        if(_mesh==null || _pointer!=null || !(p.Properties.IsRightButtonPressed || p.Properties.IsLeftButtonPressed || p.Properties.IsMiddleButtonPressed)) return;
        _rotating=p.Properties.IsRightButtonPressed; _last=p.Position; _pointer=e.Pointer;
        e.Pointer.Capture(this); e.Handled=true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if(e.Pointer!=_pointer) return;
        var p=e.GetPosition(this); var delta=p-_last; _last=p;
        _camera=_rotating?_camera with { Azimuth=_camera.Azimuth+delta.X*.008,
            Elevation=Math.Clamp(_camera.Elevation-delta.Y*.008,-1.52,1.52) }
            :_camera with { PanX=_camera.PanX+delta.X,PanY=_camera.PanY+delta.Y };
        CameraMoved?.Invoke(_camera);
        InvalidateVisual(); e.Handled=true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if(e.Pointer!=_pointer) return;
        _pointer=null; e.Pointer.Capture(null); InvalidateVisual(); e.Handled=true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    { base.OnPointerCaptureLost(e); _pointer=null; InvalidateVisual(); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if(_mesh==null) return;
        var p=e.GetPosition(this);
        double zoom=Math.Clamp(_camera.Zoom*Math.Pow(1.18,e.Delta.Y),.15,80);
        double ratio=zoom/_camera.Zoom;
        _camera=_camera with { Zoom=zoom,PanX=_camera.PanX*ratio+(p.X-Bounds.Width/2)*(1-ratio),
            PanY=_camera.PanY*ratio+(p.Y-Bounds.Height/2)*(1-ratio) };
        CameraMoved?.Invoke(_camera);
        InvalidateVisual(); e.Handled=true;
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { _active?.Cancel(); _pointer=null; base.OnDetachedFromVisualTree(e); }
    public void Dispose()
    {
        _disposed=true; _active?.Cancel(); _image?.Dispose(); _image=null;
        _mesh=null; _colors=null; _shown=_requested=_failed=null;
    }
}
