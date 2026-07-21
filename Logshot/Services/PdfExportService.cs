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
            // Header title centered on top: [Project Name] - ΔΕΛΤΙΟ ΛΗΨΕΩΝ
            string projectName = _project.Name?.ToUpper() ?? "UNTITLED PROJECT";
            column.Item().AlignCenter().Text(text =>
            {
                text.Span($"{projectName} - ΔΕΛΤΙΟ ΛΗΨΕΩΝ").FontSize(14).SemiBold();
            });

            // First Metadata Line: Director, Production Company, DOP
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

            // Second Metadata Line: Date, Shoot Day, Page
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
        int extraCount = extraCameras.Count;
        int totalColumnsCount = 2 + extraCount + 6; // CAM A, CAM B + Extras + Sound, ΕΠ, ΣΚ, ΠΛΑΝΟ, ΛΗΨΗ, ΠΑΡΑΤΗΡΗΣΕΙΣ

        container.Table(table =>
        {
            // Dynamic column widths using RelativeColumn to span the full page width between margins
            table.ColumnsDefinition(columns =>
            {
                if (extraCount == 0)
                {
                    columns.RelativeColumn(12.5f); // CAM A
                    columns.RelativeColumn(12.5f); // CAM B
                    columns.RelativeColumn(11.0f); // SOUND ROLL
                    columns.RelativeColumn(4.5f);  // ΕΠ
                    columns.RelativeColumn(6.25f); // ΣΚ
                    columns.RelativeColumn(6.25f); // ΠΛΑΝΟ
                    columns.RelativeColumn(6.25f); // ΛΗΨΗ
                    columns.RelativeColumn(40.75f);// ΠΑΡΑΤΗΡΗΣΕΙΣ
                }
                else if (extraCount == 1)
                {
                    columns.RelativeColumn(11.11f); // CAM A
                    columns.RelativeColumn(11.11f); // CAM B
                    columns.RelativeColumn(11.11f); // CAM C
                    columns.RelativeColumn(9.78f);  // SOUND ROLL
                    columns.RelativeColumn(4.00f);  // ΕΠ
                    columns.RelativeColumn(5.56f);  // ΣΚ
                    columns.RelativeColumn(5.56f);  // ΠΛΑΝΟ
                    columns.RelativeColumn(5.56f);  // ΛΗΨΗ
                    columns.RelativeColumn(36.22f); // ΠΑΡΑΤΗΡΗΣΕΙΣ
                }
                else
                {
                    float totalUnits = 8f + extraCount;
                    float camWeight = 1.0f / totalUnits * 100f;
                    columns.RelativeColumn(camWeight); // CAM A
                    columns.RelativeColumn(camWeight); // CAM B
                    foreach (var _ in extraCameras)
                    {
                        columns.RelativeColumn(camWeight);
                    }
                    columns.RelativeColumn(0.88f / totalUnits * 100f); // SOUND ROLL
                    columns.RelativeColumn(0.36f / totalUnits * 100f); // ΕΠ
                    columns.RelativeColumn(0.50f / totalUnits * 100f); // ΣΚ
                    columns.RelativeColumn(0.50f / totalUnits * 100f); // ΠΛΑΝΟ
                    columns.RelativeColumn(0.50f / totalUnits * 100f); // ΛΗΨΗ
                    columns.RelativeColumn(3.26f / totalUnits * 100f); // ΠΑΡΑΤΗΡΗΣΕΙΣ
                }
            });

            // Table Header matching desktop, centered except Notes
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
                header.Cell().Element(NotesHeaderStyle).Text(t => t.Span("ΠΑΡΑΤΗΡΗΣΕΙΣ").SemiBold());

                IContainer HeaderStyle(IContainer c) => c.Border(0.5f).BorderColor(Colors.Black).Padding(3).AlignCenter().AlignMiddle();
                IContainer NotesHeaderStyle(IContainer c) => c.Border(0.5f).BorderColor(Colors.Black).Padding(3).AlignLeft().AlignMiddle();
            });

            // Data Rows
            if (_day.Takes != null && _day.Takes.Any())
            {
                foreach (var take in _day.Takes)
                {
                    string camAVal = take.ShowCamARoll ? (take.CamARoll ?? "") : "—″—";
                    string camBVal = take.ShowCamBRoll ? (take.CamBRoll ?? "") : "—″—";
                    string soundVal = take.ShowSoundNotes ? (take.SoundNotes ?? "") : "—″—";

                    table.Cell().Element(CellStyle).Text(camAVal);
                    table.Cell().Element(CellStyle).Text(camBVal);

                    foreach (var cam in extraCameras)
                    {
                        var extraCell = take.ExtraCameraRolls?.FirstOrDefault(c => c.Label == cam);
                        bool showExtraRoll = extraCell?.ShowRoll ?? true;
                        string extraVal = showExtraRoll ? (extraCell?.Roll ?? "") : "—″—";
                        table.Cell().Element(CellStyle).Text(extraVal);
                    }

                    table.Cell().Element(CellStyle).Text(soundVal);
                    table.Cell().Element(CellStyle).Text(take.DisplayEpisode ?? "");
                    table.Cell().Element(CellStyle).Text(take.DisplayScene ?? "");
                    table.Cell().Element(CellStyle).Text(take.DisplayShot ?? "");
                    table.Cell().Element(CellStyle).Text(take.DisplayTakeNumber ?? "");
                    table.Cell().Element(NotesCellStyle).Text(take.TakeNotes ?? "");

                    // MinHeight(54) ensures ~12-13 rows per page while allowing automatic vertical expansion if needed
                    IContainer CellStyle(IContainer c) => c.Border(0.5f).BorderColor(Colors.Black).MinHeight(54).Padding(3).AlignCenter().AlignMiddle();
                    IContainer NotesCellStyle(IContainer c) => c.Border(0.5f).BorderColor(Colors.Black).MinHeight(54).Padding(3).AlignLeft().AlignMiddle();
                }
            }
            else
            {
                table.Cell().ColumnSpan((uint)totalColumnsCount).Element(EmptyStyle)
                     .Text(t => t.Span("Δεν υπάρχουν λήψεις για αυτή την ημέρα.").Italic().FontColor(Colors.Grey.Medium));

                IContainer EmptyStyle(IContainer c) => c.Border(0.5f).BorderColor(Colors.Black).MinHeight(54).Padding(10).AlignCenter().AlignMiddle();
            }
        });
    }
}