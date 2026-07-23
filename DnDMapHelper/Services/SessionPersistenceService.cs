using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DnDMapHelper.Models;
using DnDMapHelper.Models.Persistence;

namespace DnDMapHelper.Services;

public static class SessionPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Save(GameSession session, string filePath)
    {
        if (session.MapImage is null)
            throw new InvalidOperationException("Нет загруженной карты для сохранения.");

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = filePath + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        try
        {
            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                WriteMapImage(archive, session.MapImage);
                WriteSessionJson(archive, CreateSaveData(session));
            }

            if (File.Exists(filePath))
                File.Delete(filePath);

            File.Move(tempPath, filePath);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    public static void Load(GameSession session, string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);

        var mapEntry = archive.GetEntry(SessionFileFormat.MapEntryName)
            ?? throw new InvalidDataException("В файле нет изображения карты.");

        var jsonEntry = archive.GetEntry(SessionFileFormat.SessionEntryName)
            ?? throw new InvalidDataException("В файле нет данных сессии.");

        SessionSaveData data;
        using (var jsonStream = jsonEntry.Open())
            data = JsonSerializer.Deserialize<SessionSaveData>(jsonStream, JsonOptions)
                   ?? throw new InvalidDataException("Не удалось прочитать данные сессии.");

        if (data.FormatVersion > SessionFileFormat.CurrentVersion)
            throw new InvalidDataException(
                $"Файл создан в более новой версии программы (формат {data.FormatVersion}).");

        BitmapSource mapImage;
        using (var mapStream = mapEntry.Open())
            mapImage = DecodeMapImage(mapStream);

        session.ImportSaveData(data, mapImage);
    }

    private static SessionSaveData CreateSaveData(GameSession session) =>
        new()
        {
            PartyPosition = session.PartyPosition is { } party
                ? PointDto.FromPoint(party)
                : null,
            SelectedTargetId = session.SelectedTargetId,
            SelectedRegionId = session.SelectedRegionId,
            SelectedEncounterId = session.SelectedEncounterId,
            SelectedQuestId = session.SelectedQuestId,
            SelectedRouteIndex = session.SelectedRouteIndex,
            ShowMapGrid = session.ShowMapGrid,
            GridCellSizePixels = session.GridCellSizePixels,
            KilometersPerCell = session.KilometersPerCell,
            Targets = session.Targets.Select(t => new TargetMarkerDto
            {
                Id = t.Id,
                Position = PointDto.FromPoint(t.Position),
                Label = t.Label
            }).ToList(),
            Regions = session.Regions.Select(r => new MapRegionDto
            {
                Id = r.Id,
                Outline = r.Outline.Select(PointDto.FromPoint).ToList(),
                Title = r.Title,
                Description = r.Description,
                VisibleToPlayers = r.VisibleToPlayers
            }).ToList(),
            Encounters = session.Encounters.Select(e => new EncounterPointDto
            {
                Id = e.Id,
                Position = PointDto.FromPoint(e.Position),
                Title = e.Title,
                Description = e.Description
            }).ToList(),
            Routes = session.Routes.Select(r => new MovementRouteDto
            {
                Id = r.Id,
                Order = r.Order,
                TargetId = r.TargetId,
                TargetLabel = r.TargetLabel,
                Points = r.Points.Select(PointDto.FromPoint).ToList()
            }).ToList(),
            Quests = session.Quests.Select(q => new QuestDto
            {
                Id = q.Id,
                Title = q.Title,
                Conditions = q.Conditions,
                Description = q.Description,
                Reward = q.Reward,
                Status = q.Status.ToString(),
                TurnInTargetId = q.TurnInTargetId,
                ObjectiveTargetIds = q.ObjectiveTargetIds.ToList(),
                RegionIds = q.RegionIds.ToList(),
                VisitedObjectiveTargetIds = q.VisitedObjectiveTargetIds.ToList()
            }).ToList()
        };

    private static void WriteMapImage(ZipArchive archive, BitmapSource mapImage)
    {
        var entry = archive.CreateEntry(SessionFileFormat.MapEntryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();

        if (TryWriteOriginalMapFile(mapImage, entryStream))
            return;

        var encodable = ToEncodableBitmap(mapImage);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(encodable));
        encoder.Save(entryStream);
    }

    private static bool TryWriteOriginalMapFile(BitmapSource mapImage, Stream destination)
    {
        if (mapImage is not BitmapImage { UriSource: { IsFile: true } uri })
            return false;

        var path = uri.LocalPath;
        if (!File.Exists(path))
            return false;

        using var fileStream = File.OpenRead(path);
        fileStream.CopyTo(destination);
        return true;
    }

    private static BitmapSource ToEncodableBitmap(BitmapSource source)
    {
        try
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            converted.Freeze();
            var copy = new WriteableBitmap(converted);
            copy.Freeze();
            return copy;
        }
        catch (NotSupportedException)
        {
            return RenderToBitmap(source);
        }
    }

    private static BitmapSource RenderToBitmap(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var dpiX = source.DpiX > 0 ? source.DpiX : 96;
        var dpiY = source.DpiY > 0 ? source.DpiY : 96;

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
            context.DrawImage(source, new Rect(0, 0, width, height));

        var target = new RenderTargetBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static void WriteSessionJson(ZipArchive archive, SessionSaveData data)
    {
        var entry = archive.CreateEntry(SessionFileFormat.SessionEntryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, data, JsonOptions);
    }

    private static BitmapSource DecodeMapImage(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var decoder = BitmapDecoder.Create(
            buffer,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        var frame = decoder.Frames[0];
        if (frame.CanFreeze)
            frame.Freeze();

        return frame;
    }
}
