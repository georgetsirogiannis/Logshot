using System;
using System.IO;
using System.Linq;
using Logshot.Models;
using Logshot.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Logshot.Services;

public class PdfExportService
{
    private readonly ProjectViewModel _project;
    private readonly DayViewModel _day;

    public PdfExportService(ProjectViewModel project, DayViewModel day)
    {
        _project = project;
        _day = day;
    }

    public void Generate(Stream stream)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                // 1. A4 Portrait Format
                page.Size(PageSizes.A4.Portrait());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);

                // Default text styling optimized for portrait grid density
                page.DefaultTextStyle(x => x.FontFamily("Roboto Condensed").FontSize(8.5f).FontColor(Colors.Black));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
            });
        })
        .GeneratePdf(stream);
    }

    private void ComposeHeader(IContainer container)
    {
        container.PaddingBottom(12).Column(column =>
        {
            // 4. Header title centered on top: [Project Name] - ΔΕΛΤΙΟ ΛΗΨΕΩΝ
            string projectName = _project.Name?.ToUpper() ?? "UNTITLED PROJECT";
            column.Item().AlignCenter().Text(text =>
            {
                text.Span($"{projectName} - ΔΕΛΤΙΟ ΛΗΨΕΩΝ").FontSize(14).SemiBold();
            });

            // 5. First Metadata Line: Director, Production Company, DOP
            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem(3).Text(t =>
                {
                    t.Span("ΣΚΗΝΟΘΕΣΙΑ: ").SemiBold();
                    t.Span(_project.Director ?? "");
                });
                row.RelativeItem(3).Text(t =>
                {
                    t.Span("ΠΑΡΑΓΩΓΗ: ").SemiBold();
                    t.Span(_project.ProductionCompany ?? "");
                });
                row.RelativeItem(4).Text(t =>
                {
                    t.Span("ΔΙΕΥΘΥΝΣΗ ΦΩΤΟΓΡΑΦΙΑΣ: ").SemiBold();
                    t.Span(_project.Dop ?? "");
                });
            });

            // 5. Second Metadata Line: Date, Shoot Day, Page
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem(3).Text(t =>
                {
                    t.Span("ΗΜΕΡΟΜΗΝΙΑ: ").SemiBold();
                    t.Span(_day.CalendarDate.ToString("dd/MM/yyyy"));
                });
                row.RelativeItem(3).Text(t =>
                {
                    t.Span("ΗΜΕΡΑ ΓΥΡΙΣΜΑΤΟΣ: ").SemiBold();
                    t.Span(_day.ShootDayNumber?.ToString() ?? "");
                });
                row.RelativeItem(4).Text(t =>
                {
                    t.Span("ΣΕΛΙΔΑ: ").SemiBold();
                    t.CurrentPageNumber();
                });
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        var extraCameras = _day.ExtraActiveCameras?.ToList() ?? new();
        int totalColumnsCount = 2 + extraCameras.Count + 6; // CAM A, CAM B + Extras + Sound, ΕΠ, ΣΚ, ΠΛΑΝΟ, ΛΗΨΗ, ΠΑΡΑΤΗΡΗΣΕΙΣ

        container.Table(table =>
        {
            // 2. Exact column widths reflecting the desktop app order: CAM A, CAM B, [Extras], Sound, ΕΠ, ΣΚ, ΠΛΑΝΟ, ΛΗΨΗ, ΠΑΡΑΤΗΡΗΣΕΙΣ
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(45); // CAM A
                columns.ConstantColumn(45); // CAM B
                foreach (var _ in extraCameras)
                {
                    columns.ConstantColumn(45); // Dynamic Extra Cameras
                }
                columns.ConstantColumn(50); // SOUND ROLL
                columns.ConstantColumn(22); // ΕΠ
                columns.ConstantColumn(22); // ΣΚ
                columns.ConstantColumn(35); // ΠΛΑΝΟ
                columns.ConstantColumn(30); // ΛΗΨΗ
                columns.RelativeColumn();   // ΠΑΡΑΤΗΡΗΣΕΙΣ (Fills remaining space)
            });

            // 2 & 4. Table Header matching desktop and Greek labels
            table.Header(header =>
            {
                header.Cell().Element(HeaderStyle).Text(t => t.Span("CAM A").SemiBold());
                header.Cell().Element(HeaderStyle).Text(t => t.Span("CAM B").SemiBold());
                foreach (var cam in extraCameras)
                {
                    header.Cell().Element(HeaderStyle).Text(t => t.Span(cam).SemiBold());
                }
                header.Cell().Element(HeaderStyle).Text(t => t.Span("SOUND").SemiBold());
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΕΠ").SemiBold());
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΣΚ").SemiBold());
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΠΛΑΝΟ").SemiBold());
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΛΗΨΗ").SemiBold());
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΠΑΡΑΤΗΡΗΣΕΙΣ").SemiBold());

                // 3. Thin grid lines separating all cells
                IContainer HeaderStyle(IContainer c) => c.Border(0.5f).BorderColor(Colors.Black).Padding(3).AlignCenter().AlignMiddle();
            });

            // Data Rows
            if (_day.Takes != null && _day.Takes.Any())
            {
                foreach (var take in _day.Takes)
                {
                    table.Cell().Element(CellStyle).Text(take.CamARoll ?? "");
                    table.Cell().Element(CellStyle).Text(take.CamBRoll ?? "");

                    foreach (var cam in extraCameras)
                    {
                        var extraCell = take.ExtraCameraRolls?.FirstOrDefault(c => c.Label == cam);
                        table.Cell().Element(CellStyle).Text(extraCell?.Roll ?? "");
                    }

                    table.Cell().Element(CellStyle).Text(take.SoundNotes ?? "");
                    table.Cell().Element(CellStyle).Text(take.DisplayEpisode ?? "");
                    table.Cell().Element(CellStyle).Text(take.DisplayScene ?? "");
                    table.Cell().Element(CellStyle).Text(take.DisplayShot ?? "");
                    table.Cell().Element(CellStyle).Text(take.DisplayTakeNumber ?? "");
                    table.Cell().Element(CellStyle).Text(take.TakeNotes ?? "");

                    // 3. Thin grid lines separating all cells
                    IContainer CellStyle(IContainer c) => c.Border(0.5f).BorderColor(Colors.Black).Padding(3).AlignMiddle();
                }
            }
            else
            {
                table.Cell().ColumnSpan((uint)totalColumnsCount).Element(EmptyStyle)
                     .Text(t => t.Span("Δεν υπάρχουν λήψεις για αυτή την ημέρα.").Italic().FontColor(Colors.Grey.Medium));

                IContainer EmptyStyle(IContainer c) => c.Border(0.5f).BorderColor(Colors.Black).Padding(10).AlignCenter().AlignMiddle();
            }
        });
    }
}