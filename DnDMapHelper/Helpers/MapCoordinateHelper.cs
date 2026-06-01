using System.Windows;

namespace DnDMapHelper.Helpers;

public readonly record struct MapViewport(double OffsetX, double OffsetY, double Scale, double ImageWidth, double ImageHeight)
{
    public Point CanvasToImage(Point canvasPoint) =>
        new(
            (canvasPoint.X - OffsetX) / Scale,
            (canvasPoint.Y - OffsetY) / Scale);

    public Point ImageToCanvas(Point imagePoint) =>
        new(
            imagePoint.X * Scale + OffsetX,
            imagePoint.Y * Scale + OffsetY);

    public Rect ImageToCanvas(Rect imageRect) =>
        new(ImageToCanvas(imageRect.TopLeft), ImageToCanvas(imageRect.BottomRight));

    public Rect CanvasToImage(Rect canvasRect)
    {
        var topLeft = CanvasToImage(canvasRect.TopLeft);
        var bottomRight = CanvasToImage(canvasRect.BottomRight);
        return new Rect(topLeft, bottomRight);
    }
}

public static class MapCoordinateHelper
{
    public static MapViewport Calculate(Size canvasSize, double imageWidth, double imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || canvasSize.Width <= 0 || canvasSize.Height <= 0)
            return new MapViewport(0, 0, 1, imageWidth, imageHeight);

        var scale = Math.Min(canvasSize.Width / imageWidth, canvasSize.Height / imageHeight);
        var displayWidth = imageWidth * scale;
        var displayHeight = imageHeight * scale;
        var offsetX = (canvasSize.Width - displayWidth) / 2;
        var offsetY = (canvasSize.Height - displayHeight) / 2;
        return new MapViewport(offsetX, offsetY, scale, imageWidth, imageHeight);
    }
}
