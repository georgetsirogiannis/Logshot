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

    private const string NoRollSlashSvg =
        "<svg viewBox='0 0 100 100' preserveAspectRatio='none' xmlns='http://www.w3.org/2000/svg'>" +
        "<line x1='100' y1='0' x2='0' y2='100' stroke='black' stroke-width='1.5'/>" +
        "</svg>";

    private const string CrossHatchSvg =
        "<svg viewBox='0 0 400 100' preserveAspectRatio='none' xmlns='http://www.w3.org/2000/svg'>" +
        "<defs>" +
        "<pattern id='hatch' width='12' height='12' patternUnits='userSpaceOnUse'>" +
        "<path d='M0 12L12 0M0 0L12 12' stroke='#888888' stroke-width='0.6'/>" +
        "</pattern>" +
        "</defs>" +
        "<rect width='100%' height='100%' fill='url(#hatch)'/>" +
        "</svg>";

    public PdfExportService(ProjectViewModel project, DayViewModel day)
    {
        _project = project;
        _day = day;
    }

    public void Generate(Stream stream)
    {
        _day.UpdateRowVisibilities();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);

                page.DefaultTextStyle(x => x.FontFamily("Roboto Condensed").FontSize(8.5f).FontColor(Colors.Black));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
            });
        })
        .GeneratePdf(stream);
    }

    private void ComposeHeader(IContainer container)
    {
        container.PaddingBottom(8).Column(column =>
        {
            string projectName = _project.Name?.ToUpper() ?? "UNTITLED PROJECT";
            column.Item().AlignCenter().Text(text =>
            {
                text.Span($"{projectName} - ΔΕΛΤΙΟ ΛΗΨΕΩΝ").FontSize(14).Bold();
            });

            column.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem(1).AlignLeft().Text(t =>
                {
                    t.Span("ΣΚΗΝΟΘΕΣΙΑ: ").SemiBold();
                    t.Span(_project.Director ?? "");
                });
                row.RelativeItem(1).AlignCenter().Text(t =>
                {
                    t.Span("ΠΑΡΑΓΩΓΗ: ").SemiBold();
                    t.Span(_project.ProductionCompany ?? "");
                });
                row.RelativeItem(1).AlignRight().Text(t =>
                {
                    t.Span("ΔΙΕΥΘΥΝΣΗ ΦΩΤΟΓΡΑΦΙΑΣ: ").SemiBold();
                    t.Span(_project.Dop ?? "");
                });
            });

            column.Item().PaddingTop(6).Background("#EFEFEF").PaddingHorizontal(12).PaddingVertical(6).Row(row =>
            {
                row.RelativeItem(1).AlignLeft().Text(t =>
                {
                    t.Span("ΗΜΕΡΟΜΗΝΙΑ: ").SemiBold();
                    t.Span(_day.CalendarDate.ToString("dd/MM/yyyy"));
                });
                row.RelativeItem(1).AlignCenter().Text(t =>
                {
                    t.Span("ΗΜΕΡΑ ΓΥΡΙΣΜΑΤΟΣ: ").SemiBold();
                    t.Span(_day.ShootDayNumber?.ToString() ?? "");
                });
                row.RelativeItem(1).AlignRight().Text(t =>
                {
                    t.Span("ΣΕΛΙΔΑ: ").SemiBold();
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });

            // General Notes - only on first page
            if (!string.IsNullOrWhiteSpace(_day.GeneralNotes))
            {
                column.Item().ShowIf(x => x.PageNumber == 1).PaddingTop(4).Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                      .Background(Colors.Grey.Lighten4).Padding(6).Text(t =>
                      {
                          t.Span("ΠΑΡΑΤΗΡΗΣΕΙΣ ΗΜΕΡΑΣ: ").Bold().FontSize(8f);
                          t.Span(_day.GeneralNotes).FontSize(8f);
                      });
            }
        });
    }

    private void ComposeContent(IContainer container)
    {
        var extraCameras = _day.ExtraActiveCameras?.ToList() ?? new();
        int extraCount = extraCameras.Count;
        int totalColumnsCount = 2 + extraCount + 6;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                if (extraCount == 0)
                {
                    columns.RelativeColumn(12.5f);
                    columns.RelativeColumn(12.5f);
                    columns.RelativeColumn(11.0f);
                    columns.RelativeColumn(4.5f);
                    columns.RelativeColumn(6.25f);
                    columns.RelativeColumn(6.25f);
                    columns.RelativeColumn(6.25f);
                    columns.RelativeColumn(40.75f);
                }
                else if (extraCount == 1)
                {
                    columns.RelativeColumn(11.11f);
                    columns.RelativeColumn(11.11f);
                    columns.RelativeColumn(11.11f);
                    columns.RelativeColumn(9.78f);
                    columns.RelativeColumn(4.00f);
                    columns.RelativeColumn(5.56f);
                    columns.RelativeColumn(5.56f);
                    columns.RelativeColumn(5.56f);
                    columns.RelativeColumn(36.22f);
                }
                else
                {
                    float totalUnits = 8f + extraCount;
                    float camWeight = 1.0f / totalUnits * 100f;
                    columns.RelativeColumn(camWeight);
                    columns.RelativeColumn(camWeight);
                    foreach (var _ in extraCameras) columns.RelativeColumn(camWeight);
                    columns.RelativeColumn(0.88f / totalUnits * 100f);
                    columns.RelativeColumn(0.36f / totalUnits * 100f);
                    columns.RelativeColumn(0.50f / totalUnits * 100f);
                    columns.RelativeColumn(0.50f / totalUnits * 100f);
                    columns.RelativeColumn(0.50f / totalUnits * 100f);
                    columns.RelativeColumn(3.26f / totalUnits * 100f);
                }
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderStyle).Text(t => t.Span("CAM A").Bold().FontColor(Colors.White));
                header.Cell().Element(HeaderStyle).Text(t => t.Span("CAM B").Bold().FontColor(Colors.White));
                foreach (var cam in extraCameras)
                {
                    header.Cell().Element(HeaderStyle).Text(t => t.Span(cam).Bold().FontColor(Colors.White));
                }
                header.Cell().Element(HeaderStyle).Text(t => t.Span("SOUND").Bold().FontColor(Colors.White));
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΕΠ").Bold().FontColor(Colors.White));
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΣΚ").Bold().FontColor(Colors.White));
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΠΛΑΝΟ").Bold().FontColor(Colors.White));
                header.Cell().Element(HeaderStyle).Text(t => t.Span("ΛΗΨΗ").Bold().FontColor(Colors.White));
                header.Cell().Element(NotesHeaderStyle).Text(t => t.Span("ΠΑΡΑΤΗΡΗΣΕΙΣ").Bold().FontColor(Colors.White));

                IContainer HeaderStyle(IContainer c) => c.Background("#505050").Border(0.5f).BorderColor(Colors.Black).PaddingVertical(5).PaddingHorizontal(2).AlignCenter().AlignMiddle();
                IContainer NotesHeaderStyle(IContainer c) => c.Background("#505050").Border(0.5f).BorderColor(Colors.Black).PaddingVertical(5).PaddingHorizontal(6).AlignLeft().AlignMiddle();
            });

            if (_day.Takes != null && _day.Takes.Any())
            {
                foreach (var take in _day.Takes)
                {
                    RenderCameraCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take,
                        showRoll: take.ShowCamARoll, rollVal: take.CamARoll,
                        isNoRoll: take.IsCamANoRoll, isVoided: take.IsCamAVoided,
                        isRollChangeMarked: take.IsCamARollChangeMarked, rollNumber: take.CamARollNumber);

                    RenderCameraCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take,
                        showRoll: take.ShowCamBRoll, rollVal: take.CamBRoll,
                        isNoRoll: take.IsCamBNoRoll, isVoided: take.IsCamBVoided,
                        isRollChangeMarked: take.IsCamBRollChangeMarked, rollNumber: take.CamBRollNumber);

                    foreach (var camLabel in extraCameras)
                    {
                        var extraCell = take.ExtraCameraRolls?.FirstOrDefault(c => c.Label == camLabel);
                        RenderCameraCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take,
                            showRoll: extraCell?.ShowRoll ?? true,
                            rollVal: extraCell?.Roll ?? "",
                            isNoRoll: extraCell?.IsNoRoll ?? false,
                            isVoided: extraCell?.IsVoided ?? false,
                            isRollChangeMarked: extraCell?.IsRollChangeMarked ?? false,
                            rollNumber: extraCell?.RollNumber ?? "");
                    }

                    RenderSoundCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take);
                    RenderEpisodeCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take);
                    RenderSceneCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take);
                    RenderShotCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take);
                    RenderTakeCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take);
                    RenderNotesCell(table.Cell().Element(c => ApplyCellBorderStyle(c, take)), take);
                }

                if (_day.IsFinalized)
                {
                    int count = _day.Takes.Count;
                    int remaining = 12 - (count % 12);
                    if (remaining == 12 && count > 0) remaining = 0;

                    float fillerHeight = Math.Max(50f, remaining * 36f);

                    table.Cell().ColumnSpan((uint)totalColumnsCount)
                         .Element(c => c.Border(0.5f).BorderColor(Colors.Black).MinHeight(fillerHeight))
                         .Layers(layers =>
                         {
                             layers.Layer().Svg(CrossHatchSvg);
                             layers.PrimaryLayer().AlignCenter().AlignMiddle()
                                   .Border(1.5f).BorderColor(Colors.Black).Background(Colors.White)
                                   .PaddingHorizontal(20).PaddingVertical(8)
                                   .Text($"END DAY {_day.ShootDayNumber}").FontSize(14).Bold();
                         });
                }
            }
            else
            {
                table.Cell().ColumnSpan((uint)totalColumnsCount)
                     .Element(c => c.Border(0.5f).BorderColor(Colors.Black).MinHeight(54).Padding(10).AlignCenter().AlignMiddle())
                     .Text(t => t.Span("Δεν υπάρχουν λήψεις για αυτή την ημέρα.").Italic().FontColor(Colors.Grey.Medium));
            }
        });
    }

    private IContainer ApplyCellBorderStyle(IContainer container, TakeViewModel take)
    {
        float minH = take.HasVoidedCameras ? 16f : 36f;
        var c = container.Border(0.5f).BorderColor(Colors.Black);
        if (take.IsGroupStart)
        {
            c = c.BorderTop(2f).BorderColor(Colors.Black);
        }
        return c.MinHeight(minH).Padding(2);
    }

    private void RenderCameraCell(IContainer cell, TakeViewModel take, bool showRoll, string rollVal, bool isNoRoll, bool isVoided, bool isRollChangeMarked, string rollNumber)
    {
        if (take.HasVoidedCameras)
        {
            if (isVoided)
            {
                cell.AlignCenter().AlignMiddle().Text("ΑΚΥΡΟ CLIP").Bold().FontSize(8f).FontColor(Colors.Red.Medium);
            }
            else if (isNoRoll)
            {
                cell.Svg(NoRollSlashSvg);
            }
            else
            {
                cell.AlignCenter().AlignMiddle().Text("XXXXXXXXXXXXXXXXXXXX").FontSize(7f).FontColor(Colors.Grey.Darken1);
            }
            return;
        }

        if (isNoRoll)
        {
            cell.Svg(NoRollSlashSvg);
            return;
        }

        cell.AlignCenter().AlignMiddle().Column(col =>
        {
            if (isRollChangeMarked && !string.IsNullOrWhiteSpace(rollNumber))
            {
                col.Item().AlignCenter().Text(rollNumber).Bold().Underline().FontSize(8f);
            }

            if (showRoll && !string.IsNullOrWhiteSpace(rollVal))
            {
                col.Item().AlignCenter().Text(rollVal).FontSize(8.5f);
            }
            else if (!showRoll)
            {
                col.Item().AlignCenter().Text("—″—").FontSize(8.5f);
            }
        });
    }

    private void RenderSoundCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras)
        {
            cell.AlignCenter().AlignMiddle().Text("XXXXXXXXXXXXXXXXXXXX").FontSize(7f).FontColor(Colors.Grey.Darken1);
            return;
        }

        if (take.IsSoundNoRoll)
        {
            cell.Svg(NoRollSlashSvg);
            return;
        }

        cell.AlignCenter().AlignMiddle().Element(c =>
        {
            c.Text(take.ShowSoundNotes ? (take.SoundNotes ?? "") : "—″—").FontSize(8.5f);
        });
    }

    private void RenderEpisodeCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras || take.IsSoundOnlyRow || !take.ShowEpisode) return;
        cell.AlignCenter().AlignMiddle().Text(take.Episode ?? "").FontSize(8.5f);
    }

    private void RenderSceneCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras || take.IsSoundOnlyRow || !take.ShowScene) return;
        cell.AlignCenter().AlignMiddle().Text(take.Scene ?? "").FontSize(8.5f);
    }

    private void RenderShotCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras || take.IsSoundOnlyRow || !take.ShowShot) return;
        string shotStr = take.Shot > 0 ? take.Shot.ToString() : "";
        cell.AlignCenter().AlignMiddle().Text(shotStr).FontSize(8.5f);
    }

    private void RenderTakeCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras || take.IsSoundOnlyRow || take.TakeNumber <= 0) return;

        string takeText = take.TakeNumber.ToString();
        if (take.IsPickup) takeText += " PU";

        if (take.IsCircled)
        {
            string circledSvg =
                $"<svg viewBox='0 0 36 36' preserveAspectRatio='xMidYMid meet' xmlns='http://www.w3.org/2000/svg'>" +
                $"<ellipse cx='18' cy='18' rx='16' ry='16' fill='none' stroke='black' stroke-width='1.5'/>" +
                $"<text x='18' y='21.5' text-anchor='middle' font-family='Roboto Condensed' font-size='10' font-weight='500'>{takeText}</text>" +
                $"</svg>";
            cell.AlignCenter().AlignMiddle().Svg(circledSvg);
        }
        else if (take.IsFailed)
        {
            // Grey X for failed take
            string failedSvg =
                $"<svg viewBox='0 0 30 30' preserveAspectRatio='xMidYMid meet' xmlns='http://www.w3.org/2000/svg'>" +
                $"<line x1='6' y1='6' x2='24' y2='24' stroke='#666666' stroke-width='2'/>" +
                $"<line x1='24' y1='6' x2='6' y2='24' stroke='#666666' stroke-width='2'/>" +
                $"<text x='15' y='20.5' text-anchor='middle' font-family='Roboto Condensed' font-size='10' font-weight='500'>{takeText}</text>" +
                $"</svg>";
            cell.AlignCenter().AlignMiddle().Svg(failedSvg);
        }
        else
        {
            cell.AlignCenter().AlignMiddle().Text(takeText).Bold().FontSize(8.5f);
        }
    }

    private void RenderNotesCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras)
        {
            cell.AlignCenter().AlignMiddle().Text("XXXXXXXXXXXXXXXXXXXX").FontSize(7f).FontColor(Colors.Grey.Darken1);
            return;
        }

        cell.PaddingHorizontal(4).AlignMiddle().Row(row =>
        {
            row.RelativeItem().AlignMiddle().Row(innerRow =>
            {
                if (take.FalseStartCount > 0 || take.IsLongStart)
                {
                    innerRow.AutoItem().AlignMiddle().PaddingRight(5).Row(badgeRow =>
                    {
                        if (take.FalseStartCount > 0)
                        {
                            string fsText = take.FalseStartCount == 1 ? "FS" : $"FS x{take.FalseStartCount}";
                            badgeRow.AutoItem().AlignMiddle().PaddingRight(3)
                                    .Border(0.8f).BorderColor(Colors.Black)
                                    .PaddingHorizontal(3).PaddingVertical(1)
                                    .Text(fsText).FontSize(7.5f).Bold();
                        }
                        if (take.IsLongStart)
                        {
                            badgeRow.AutoItem().AlignMiddle()
                                    .Border(0.8f).BorderColor(Colors.Black)
                                    .PaddingHorizontal(3).PaddingVertical(1)
                                    .Text("LS").FontSize(7.5f).Bold();
                        }
                    });
                }

                innerRow.RelativeItem().AlignMiddle().Text(take.TakeNotes ?? "").FontSize(8.5f);

                if (take.IsEndBoard || take.IsNoBoard)
                {
                    innerRow.AutoItem().AlignRight().AlignMiddle().PaddingLeft(6).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(7.5f).Bold());
                        if (take.IsEndBoard) t.Span("ΚΛΑΚΕΤΑ\nΤΕΛΟΥΣ");
                        else if (take.IsNoBoard) t.Span("ΧΩΡΙΣ\nΚΛΑΚΕΤΑ");
                    });
                }
            });

            if (take.IsBlooper)
            {
                row.AutoItem().AlignRight().AlignMiddle().PaddingLeft(6)
                   .Border(0.8f).BorderColor(Colors.Black)
                   .PaddingVertical(3).PaddingHorizontal(1)
                   .RotateLeft()
                   .Text("BLOOPER").FontSize(7f).Bold().LetterSpacing(0.08f);
            }
        });
    }
}