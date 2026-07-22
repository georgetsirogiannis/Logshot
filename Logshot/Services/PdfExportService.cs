using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

    private const string CrossHatchSvg =
        "<svg viewBox='0 0 400 100' preserveAspectRatio='none' xmlns='http://www.w3.org/2000/svg'>" +
        "<defs>" +
        "<pattern id='hatch' width='12' height='12' patternUnits='userSpaceOnUse'>" +
        "<path d='M0 12L12 0M0 0L12 12' stroke='#888888' stroke-width='0.6'/>" +
        "</pattern>" +
        "</defs>" +
        "<rect width='100%' height='100%' fill='url(#hatch)'/>" +
        "</svg>";

    private const string CircledTakeSvg =
        "<svg viewBox='0 0 36 36' preserveAspectRatio='xMidYMid meet' xmlns='http://www.w3.org/2000/svg'>" +
        "<circle cx='18' cy='18' r='14' fill='none' stroke='black' stroke-width='1.5'/>" +
        "</svg>";

    private const string FailedTakeSvg =
        "<svg viewBox='0 0 36 36' preserveAspectRatio='xMidYMid meet' xmlns='http://www.w3.org/2000/svg'>" +
        "<line x1='8' y1='8' x2='28' y2='28' stroke='#B3B3B3' stroke-width='1.5'/>" +
        "<line x1='28' y1='8' x2='8' y2='28' stroke='#B3B3B3' stroke-width='1.5'/>" +
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
        var c = container.Border(0.5f).BorderColor(Colors.Black);
        if (take.IsGroupStart)
        {
            c = c.BorderTop(2f).BorderColor(Colors.Black);
        }

        if (take.HasVoidedCameras)
        {
            return c.Height(20f);
        }

        return c.MinHeight(40f).Padding(2);
    }

    private static void RenderCrossStitchCell(IContainer cell)
    {
        cell.Svg(size =>
        {
            if (float.IsNaN(size.Width) || float.IsInfinity(size.Width) || size.Width <= 0.1f ||
                float.IsNaN(size.Height) || float.IsInfinity(size.Height) || size.Height <= 0.1f)
            {
                return "<svg viewBox='0 0 1 1' xmlns='http://www.w3.org/2000/svg'></svg>";
            }

            float w = size.Width;
            float h = size.Height;
            float step = 8f;

            var sb = new StringBuilder();
            sb.Append($"<svg viewBox='0 0 {w.ToString(CultureInfo.InvariantCulture)} {h.ToString(CultureInfo.InvariantCulture)}' xmlns='http://www.w3.org/2000/svg'>");

            // 1. Top-left to bottom-right lines (\): x - y = C
            for (float c = -h; c <= w; c += step)
            {
                float x1 = c >= 0 ? c : 0;
                float y1 = c >= 0 ? 0 : -c;

                float x2 = (c + h) <= w ? (c + h) : w;
                float y2 = (c + h) <= w ? h : (w - c);

                if (x2 > x1 && y2 > y1)
                {
                    sb.Append($"<line x1='{x1.ToString(CultureInfo.InvariantCulture)}' y1='{y1.ToString(CultureInfo.InvariantCulture)}' x2='{x2.ToString(CultureInfo.InvariantCulture)}' y2='{y2.ToString(CultureInfo.InvariantCulture)}' stroke='#888888' stroke-width='0.5'/>");
                }
            }

            // 2. Top-right to bottom-left lines (/): x + y = C
            for (float c = 0; c <= w + h; c += step)
            {
                float x1 = c <= w ? c : w;
                float y1 = c <= w ? 0 : (c - w);

                float x2 = (c - h) >= 0 ? (c - h) : 0;
                float y2 = (c - h) >= 0 ? h : c;

                if (x1 > x2 && y2 > y1)
                {
                    sb.Append($"<line x1='{x1.ToString(CultureInfo.InvariantCulture)}' y1='{y1.ToString(CultureInfo.InvariantCulture)}' x2='{x2.ToString(CultureInfo.InvariantCulture)}' y2='{y2.ToString(CultureInfo.InvariantCulture)}' stroke='#888888' stroke-width='0.5'/>");
                }
            }

            sb.Append("</svg>");
            return sb.ToString();
        });
    }

    private static void RenderNoRollCell(IContainer cell, float targetHeight)
    {
        cell.Height(targetHeight).Svg(size =>
        {
            if (float.IsNaN(size.Width) || float.IsInfinity(size.Width) || size.Width <= 0.1f ||
                float.IsNaN(size.Height) || float.IsInfinity(size.Height) || size.Height <= 0.1f)
            {
                return "<svg viewBox='0 0 1 1' xmlns='http://www.w3.org/2000/svg'></svg>";
            }

            float w = size.Width;
            float h = size.Height;

            return $"<svg viewBox='0 0 {w.ToString(CultureInfo.InvariantCulture)} {h.ToString(CultureInfo.InvariantCulture)}' xmlns='http://www.w3.org/2000/svg'>" +
                   $"<line x1='{w.ToString(CultureInfo.InvariantCulture)}' y1='0' x2='0' y2='{h.ToString(CultureInfo.InvariantCulture)}' stroke='black' stroke-width='1.5'/>" +
                   "</svg>";
        });
    }

    private void RenderCameraCell(IContainer cell, TakeViewModel take, bool showRoll, string rollVal, bool isNoRoll, bool isVoided, bool isRollChangeMarked, string rollNumber)
    {
        if (take.HasVoidedCameras)
        {
            if (isVoided)
            {
                cell.Padding(2).AlignCenter().AlignMiddle().Text("ΑΚΥΡΟ CLIP").Bold().FontSize(8f).FontColor(Colors.Red.Medium);
            }
            else if (isNoRoll)
            {
                RenderNoRollCell(cell, 20f);
            }
            else
            {
                RenderCrossStitchCell(cell);
            }
            return;
        }

        if (isNoRoll)
        {
            RenderNoRollCell(cell, 40f);
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
            RenderCrossStitchCell(cell);
            return;
        }

        if (take.IsSoundNoRoll)
        {
            RenderNoRollCell(cell, 40f);
            return;
        }

        cell.AlignCenter().AlignMiddle().Element(c =>
        {
            c.Text(take.ShowSoundNotes ? (take.SoundNotes ?? "") : "—″—").FontSize(8.5f);
        });
    }

    private void RenderEpisodeCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras)
        {
            RenderCrossStitchCell(cell);
            return;
        }

        if (take.IsSoundOnlyRow || !take.ShowEpisode) return;
        cell.AlignCenter().AlignMiddle().Text(take.Episode ?? "").FontSize(8.5f);
    }

    private void RenderSceneCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras)
        {
            RenderCrossStitchCell(cell);
            return;
        }

        if (take.IsSoundOnlyRow || !take.ShowScene) return;
        cell.AlignCenter().AlignMiddle().Text(take.Scene ?? "").FontSize(8.5f);
    }

    private void RenderShotCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras)
        {
            RenderCrossStitchCell(cell);
            return;
        }

        if (take.IsSoundOnlyRow || !take.ShowShot) return;
        string shotStr = take.Shot > 0 ? take.Shot.ToString() : "";
        cell.AlignCenter().AlignMiddle().Text(shotStr).FontSize(8.5f);
    }

    private void RenderTakeCell(IContainer cell, TakeViewModel take)
    {
        if (take.HasVoidedCameras)
        {
            RenderCrossStitchCell(cell);
            return;
        }

        if (take.IsSoundOnlyRow || take.TakeNumber <= 0) return;

        string takeText = take.TakeNumber.ToString();
        if (take.IsPickup) takeText += " PU";

        if (take.IsCircled)
        {
            cell.Layers(layers =>
            {
                layers.Layer().AlignCenter().AlignMiddle().Svg(CircledTakeSvg);
                layers.PrimaryLayer().AlignCenter().AlignMiddle().Text(takeText).Bold().FontSize(8.5f);
            });
        }
        else if (take.IsFailed)
        {
            cell.Layers(layers =>
            {
                layers.Layer().AlignCenter().AlignMiddle().Svg(FailedTakeSvg);
                layers.PrimaryLayer().AlignCenter().AlignMiddle().Text(takeText).Bold().FontSize(8.5f);
            });
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
            RenderCrossStitchCell(cell);
            return;
        }

        cell.Row(row =>
        {
            row.RelativeItem().PaddingLeft(4).PaddingRight(take.IsBlooper ? 0 : 4).AlignMiddle().Row(innerRow =>
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