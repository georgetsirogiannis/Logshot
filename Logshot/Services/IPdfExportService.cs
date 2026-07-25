using System.IO;
using System.Threading.Tasks;
using Logshot.ViewModels;

namespace Logshot.Services;

public interface IPdfExportService
{
    bool IsSupported { get; }
    Task GeneratePdfAsync(ProjectViewModel project, DayViewModel day, Stream stream);
}

public static class PdfExportServiceRegistry
{
    public static IPdfExportService? Instance { get; set; }
}