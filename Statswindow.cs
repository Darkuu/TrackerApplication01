using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Tracker
{
    public sealed class StatsWindow : Window
    {
        private sealed class PersonCounts
        {
            public int Person1 { get; set; }
            public int Person2 { get; set; }
        }

        private sealed class ChartBucket
        {
            public string Label { get; set; } = "";
            public int Person1 { get; set; }
            public int Person2 { get; set; }
        }

        private readonly DateTime selectedMonth;
        private readonly Color person1Color;
        private readonly Color person2Color;
        private readonly List<string> categories;
        private readonly List<SavedCellPaint> entries;

        private readonly ComboBox periodSelector = new ComboBox();
        private readonly StackPanel reportPanel = new StackPanel();

        public StatsWindow(
            DateTime selectedMonth,
            Color person1Color,
            Color person2Color,
            List<string> categories,
            List<SavedCellPaint> entries)
        {
            this.selectedMonth = new DateTime(
                selectedMonth.Year,
                selectedMonth.Month,
                1);

            this.person1Color = person1Color;
            this.person2Color = person2Color;
            this.categories = categories;
            this.entries = entries;

            Title = "Tracker Statistics";
            Width = 1000;
            Height = 760;
            MinWidth = 760;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = MakeBrush("#F3F6FB");
            FontFamily = new FontFamily("Segoe UI");

            Content = BuildLayout();
            RenderReport();
        }

        private UIElement BuildLayout()
        {
            var page = new DockPanel
            {
                Margin = new Thickness(22)
            };

            var header = new DockPanel
            {
                LastChildFill = false,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var titleStack = new StackPanel();

            titleStack.Children.Add(new TextBlock
            {
                Text = "Statistics",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = MakeBrush("#202B40")
            });

            titleStack.Children.Add(new TextBlock
            {
                Text = $"Selected month: {selectedMonth:MMMM yyyy}",
                FontSize = 12,
                Foreground = MakeBrush("#68758A"),
                Margin = new Thickness(0, 4, 0, 0)
            });

            header.Children.Add(titleStack);

            var periodPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Height = 36
            };

            periodPanel.Children.Add(new TextBlock
            {
                Text = "Show:",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = MakeBrush("#68758A"),
                Margin = new Thickness(0, 0, 8, 0)
            });

            AddPeriodOption("Month");
            AddPeriodOption("Year");
            AddPeriodOption("All time");

            periodSelector.Width = 130;
            periodSelector.Height = 36;
            periodSelector.VerticalAlignment = VerticalAlignment.Center;
            periodSelector.VerticalContentAlignment = VerticalAlignment.Center;
            periodSelector.Padding = new Thickness(7, 0, 7, 0);
            periodSelector.SelectedIndex = 0;
            
            periodPanel.Children.Add(periodSelector);

            DockPanel.SetDock(periodPanel, Dock.Right);
            header.Children.Add(periodPanel);

            DockPanel.SetDock(header, Dock.Top);
            page.Children.Add(header);

            var reportScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = reportPanel
            };

            page.Children.Add(reportScroll);
            return page;
        }

        private void AddPeriodOption(string text)
        {
            periodSelector.Items.Add(new ComboBoxItem
            {
                Content = text,
                Tag = text
            });
        }

        private string SelectedPeriod =>
            (periodSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? "Month";

        private void RenderReport()
        {
            reportPanel.Children.Clear();

            List<SavedCellPaint> filtered = GetFilteredEntries();
            (DateTime? start, DateTime? end) = GetDateRange(filtered);

            int dayCount = GetRangeDayCount(start, end);
            var dailyCounts = GetDailyCounts(filtered);

            int person1Total = dailyCounts.Values.Sum(x => x.Person1);
            int person2Total = dailyCounts.Values.Sum(x => x.Person2);

            long possibleMarks = (long)categories.Count * dayCount * 2;
            double completion = possibleMarks == 0
                ? 0
                : (person1Total + person2Total) * 100.0 / possibleMarks;

            AddSectionTitle(GetPeriodTitle());

            var cards = new UniformGrid
            {
                Columns = 3,
                Margin = new Thickness(0, 0, 0, 16)
            };

            cards.Children.Add(CreateSummaryCard(
                "Person 1 marks",
                person1Total.ToString(),
                person1Color,
                "Solid and split cells"));

            cards.Children.Add(CreateSummaryCard(
                "Person 2 marks",
                person2Total.ToString(),
                person2Color,
                "Solid and split cells"));

            cards.Children.Add(CreateSummaryCard(
                "Average completion",
                $"{completion:0.#}%",
                MakeBrush("#5265E8").Color,
                "Across both people and all category-days"));

            reportPanel.Children.Add(cards);

            var averageGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 16)
            };
            averageGrid.ColumnDefinitions.Add(new ColumnDefinition());
            averageGrid.ColumnDefinitions.Add(new ColumnDefinition());

            averageGrid.Children.Add(CreateSummaryCard(
                "Person 1 average",
                AverageMarks(person1Total, dayCount),
                person1Color,
                "Categories marked per day"));

            var person2Average = CreateSummaryCard(
                "Person 2 average",
                AverageMarks(person2Total, dayCount),
                person2Color,
                "Categories marked per day");

            Grid.SetColumn(person2Average, 1);
            averageGrid.Children.Add(person2Average);
            reportPanel.Children.Add(averageGrid);

            AddSectionTitle("Activity over time");

            List<ChartBucket> buckets = GetActivityBuckets(filtered);
            AddLegend();
            AddChartCard("Marks by period", CreateGroupedBarChart(buckets));
            AddChartCard("Cumulative marks", CreateCumulativeChart(buckets));

            AddSectionTitle("Category comparison");
            AddChartCard("Marks by category", CreateCategoryChart(filtered));

            AddSectionTitle("Weekday patterns");
            AddChartCard(
                "Average categories marked by weekday",
                CreateWeekdayChart(filtered, start, end));

            reportPanel.Children.Add(new TextBlock
            {
                Text = "A split cell counts once for each person. Solid cells are assigned to a person when their saved color matches that person's current color setting.",
                FontSize = 11,
                Foreground = MakeBrush("#68758A"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 12, 2, 4)
            });
        }

        private void AddLegend()
        {
            var legend = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2, -4, 0, 10)
            };

            AddLegendItem(legend, person1Color, "Person 1");
            AddLegendItem(legend, person2Color, "Person 2");
            reportPanel.Children.Add(legend);
        }

        private static void AddLegendItem(
            Panel panel,
            Color color,
            string label)
        {
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 18, 0)
            };

            item.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            item.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = MakeBrush("#68758A"),
                VerticalAlignment = VerticalAlignment.Center
            });

            panel.Children.Add(item);
        }

        private string GetPeriodTitle()
        {
            return SelectedPeriod switch
            {
                "Year" => $"Year: {selectedMonth:yyyy}",
                "All time" => "All saved tracker data",
                _ => $"Month: {selectedMonth:MMMM yyyy}"
            };
        }

        private List<SavedCellPaint> GetFilteredEntries()
        {
            return SelectedPeriod switch
            {
                "Year" => entries.Where(e => e.Year == selectedMonth.Year).ToList(),
                "All time" => entries.ToList(),
                _ => entries.Where(e =>
                    e.Year == selectedMonth.Year &&
                    e.Month == selectedMonth.Month).ToList()
            };
        }

        private (DateTime? Start, DateTime? End) GetDateRange(
            List<SavedCellPaint> filtered)
        {
            if (SelectedPeriod == "Month")
            {
                DateTime start = selectedMonth;
                DateTime end = new DateTime(
                    selectedMonth.Year,
                    selectedMonth.Month,
                    DateTime.DaysInMonth(selectedMonth.Year, selectedMonth.Month));

                return (start, end);
            }

            if (SelectedPeriod == "Year")
            {
                return (
                    new DateTime(selectedMonth.Year, 1, 1),
                    new DateTime(selectedMonth.Year, 12, 31));
            }

            var dates = filtered
                .Select(ToDate)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .OrderBy(d => d)
                .ToList();

            return dates.Count == 0
                ? (null, null)
                : (dates.First(), dates.Last());
        }

        private static int GetRangeDayCount(DateTime? start, DateTime? end)
        {
            if (start == null || end == null || end < start)
                return 0;

            return (end.Value.Date - start.Value.Date).Days + 1;
        }

        private Dictionary<DateTime, PersonCounts> GetDailyCounts(
            List<SavedCellPaint> filtered)
        {
            var result = new Dictionary<DateTime, PersonCounts>();

            foreach (SavedCellPaint entry in filtered)
            {
                DateTime? date = ToDate(entry);
                if (date == null)
                    continue;

                if (!result.TryGetValue(date.Value, out PersonCounts? counts))
                {
                    counts = new PersonCounts();
                    result[date.Value] = counts;
                }

                if (PaintContains(entry, person1Color))
                    counts.Person1++;

                if (PaintContains(entry, person2Color))
                    counts.Person2++;
            }

            return result;
        }

        private List<ChartBucket> GetActivityBuckets(
            List<SavedCellPaint> filtered)
        {
            var daily = GetDailyCounts(filtered);

            if (SelectedPeriod == "Month")
            {
                int days = DateTime.DaysInMonth(
                    selectedMonth.Year,
                    selectedMonth.Month);

                return Enumerable.Range(1, days)
                    .Select(day =>
                    {
                        DateTime date = new DateTime(
                            selectedMonth.Year,
                            selectedMonth.Month,
                            day);

                        daily.TryGetValue(date, out PersonCounts? counts);

                        return new ChartBucket
                        {
                            Label = day.ToString(),
                            Person1 = counts?.Person1 ?? 0,
                            Person2 = counts?.Person2 ?? 0
                        };
                    })
                    .ToList();
            }

            if (SelectedPeriod == "Year")
            {
                return Enumerable.Range(1, 12)
                    .Select(month =>
                    {
                        var date = new DateTime(selectedMonth.Year, month, 1);

                        List<PersonCounts> monthCounts = daily
                            .Where(pair =>
                                pair.Key.Year == selectedMonth.Year &&
                                pair.Key.Month == month)
                            .Select(pair => pair.Value)
                            .ToList();

                        return new ChartBucket
                        {
                            Label = date.ToString("MMM"),
                            Person1 = monthCounts.Sum(x => x.Person1),
                            Person2 = monthCounts.Sum(x => x.Person2)
                        };
                    })
                    .ToList();
            }

            return filtered
                .Select(e => e.Year)
                .Distinct()
                .OrderBy(year => year)
                .Select(year =>
                {
                    List<PersonCounts> yearCounts = daily
                        .Where(pair => pair.Key.Year == year)
                        .Select(pair => pair.Value)
                        .ToList();

                    return new ChartBucket
                    {
                        Label = year.ToString(),
                        Person1 = yearCounts.Sum(x => x.Person1),
                        Person2 = yearCounts.Sum(x => x.Person2)
                    };
                })
                .ToList();
        }

        private UIElement CreateGroupedBarChart(List<ChartBucket> buckets)
        {
            if (buckets.Count == 0)
                return EmptyChartMessage();

            int maxValue = Math.Max(
                1,
                buckets.Max(b => Math.Max(b.Person1, b.Person2)));

            double chartHeight = 210;
            double barAreaHeight = 160;
            double bucketWidth = SelectedPeriod == "Month" ? 38 : 58;
            double chartWidth = Math.Max(650, buckets.Count * bucketWidth);

            var canvas = new Canvas
            {
                Width = chartWidth,
                Height = chartHeight,
                Background = Brushes.White
            };

            AddHorizontalGridLines(canvas, chartWidth, barAreaHeight);

            for (int i = 0; i < buckets.Count; i++)
            {
                ChartBucket bucket = buckets[i];
                double xCenter = i * bucketWidth + bucketWidth / 2;
                double barWidth = SelectedPeriod == "Month" ? 8 : 14;

                double height1 = bucket.Person1 == 0
                    ? 2
                    : barAreaHeight * bucket.Person1 / maxValue;

                double height2 = bucket.Person2 == 0
                    ? 2
                    : barAreaHeight * bucket.Person2 / maxValue;

                AddBar(canvas, xCenter - barWidth - 1, barAreaHeight,
                    barWidth, height1, person1Color, bucket.Person1);

                AddBar(canvas, xCenter + 1, barAreaHeight,
                    barWidth, height2, person2Color, bucket.Person2);

                AddCanvasText(canvas, bucket.Label, xCenter - 18,
                    barAreaHeight + 8, 36, 20, 10);
            }

            return new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = canvas
            };
        }

        private UIElement CreateCumulativeChart(List<ChartBucket> buckets)
        {
            if (buckets.Count == 0)
                return EmptyChartMessage();

            double width = Math.Max(650, buckets.Count * 42);
            double height = 230;
            double left = 38;
            double right = width - 16;
            double top = 18;
            double bottom = height - 35;

            // Use the final cumulative total as the chart scale.
            int max = Math.Max(
                1,
                Math.Max(
                    buckets.Sum(b => b.Person1),
                    buckets.Sum(b => b.Person2)));

            var canvas = new Canvas
            {
                Width = width,
                Height = height,
                Background = Brushes.White
            };

            AddHorizontalGridLines(canvas, width, bottom - top, top);

            var line1 = new Polyline
            {
                Stroke = new SolidColorBrush(person1Color),
                StrokeThickness = 3
            };

            var line2 = new Polyline
            {
                Stroke = new SolidColorBrush(person2Color),
                StrokeThickness = 3
            };

            int cumulative1 = 0;
            int cumulative2 = 0;

            for (int i = 0; i < buckets.Count; i++)
            {
                cumulative1 += buckets[i].Person1;
                cumulative2 += buckets[i].Person2;

                double x = buckets.Count == 1
                    ? (left + right) / 2
                    : left + (right - left) * i / (buckets.Count - 1);

                double y1 = bottom - (bottom - top) * cumulative1 / max;
                double y2 = bottom - (bottom - top) * cumulative2 / max;

                line1.Points.Add(new Point(x, y1));
                line2.Points.Add(new Point(x, y2));

                AddCanvasText(canvas, buckets[i].Label, x - 20,
                    bottom + 8, 40, 18, 9);
            }

            canvas.Children.Add(line1);
            canvas.Children.Add(line2);

            return new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = canvas
            };
        }

        private UIElement CreateCategoryChart(List<SavedCellPaint> filtered)
        {
            if (categories.Count == 0)
                return EmptyChartMessage();

            var categoryCounts = categories.Select(category =>
            {
                List<SavedCellPaint> categoryEntries = filtered
                    .Where(e => e.Category == category)
                    .ToList();

                return (
                    Category: category,
                    Person1: categoryEntries.Count(e =>
                        PaintContains(e, person1Color)),
                    Person2: categoryEntries.Count(e =>
                        PaintContains(e, person2Color)));
            }).ToList();

            int max = Math.Max(
                1,
                categoryCounts.Max(x => Math.Max(x.Person1, x.Person2)));

            var rows = new StackPanel();

            foreach (var item in categoryCounts)
            {
                var row = new StackPanel
                {
                    Margin = new Thickness(0, 5, 0, 8)
                };

                var topLine = new DockPanel();

                topLine.Children.Add(new TextBlock
                {
                    Text = item.Category,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = MakeBrush("#26334A"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                var totals = new TextBlock
                {
                    Text = $"Person 1: {item.Person1}    Person 2: {item.Person2}",
                    Foreground = MakeBrush("#68758A"),
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                DockPanel.SetDock(totals, Dock.Right);
                topLine.Children.Add(totals);
                row.Children.Add(topLine);

                row.Children.Add(CreateComparisonBar(
                    item.Person1,
                    item.Person2,
                    max));

                rows.Children.Add(row);
            }

            return rows;
        }

        private UIElement CreateComparisonBar(int person1, int person2, int max)
        {
            const double availableWidth = 650;
            double firstWidth = availableWidth * person1 / max;
            double secondWidth = availableWidth * person2 / max;

            var bars = new StackPanel
            {
                Width = availableWidth
            };

            bars.Children.Add(new Border
            {
                Width = Math.Max(2, firstWidth),
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(person1Color),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 3, 0, 2)
            });

            bars.Children.Add(new Border
            {
                Width = Math.Max(2, secondWidth),
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(person2Color),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 2, 0, 3)
            });

            return new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = bars
            };
        }

        private UIElement CreateWeekdayChart(
            List<SavedCellPaint> filtered,
            DateTime? start,
            DateTime? end)
        {
            if (start == null || end == null)
                return EmptyChartMessage();

            var counts = GetDailyCounts(filtered);
            var weekdays = new double[7];
            var occurrences = new int[7];

            for (DateTime date = start.Value.Date;
                 date <= end.Value.Date;
                 date = date.AddDays(1))
            {
                int index = (int)date.DayOfWeek;
                occurrences[index]++;

                if (counts.TryGetValue(date, out PersonCounts? day))
                    weekdays[index] += day.Person1 + day.Person2;
            }

            var averages = new double[7];

            for (int i = 0; i < 7; i++)
            {
                averages[i] = occurrences[i] == 0
                    ? 0
                    : weekdays[i] / occurrences[i];
            }

            double max = Math.Max(1, averages.Max());
            double barHeight = 140;
            double columnWidth = 76;

            var canvas = new Canvas
            {
                Width = 7 * columnWidth,
                Height = 185,
                Background = Brushes.White
            };

            AddHorizontalGridLines(canvas, canvas.Width, barHeight);

            for (int i = 0; i < 7; i++)
            {
                double height = averages[i] == 0
                    ? 2
                    : barHeight * averages[i] / max;

                double x = i * columnWidth + 24;

                AddBar(canvas, x, barHeight, 28, height,
                    MakeBrush("#5265E8").Color,
                    averages[i].ToString("0.0"));

                AddCanvasText(
                    canvas,
                    CultureInfo.CurrentCulture.DateTimeFormat
                        .AbbreviatedDayNames[i],
                    i * columnWidth + 5,
                    barHeight + 8,
                    70,
                    20,
                    10);
            }

            return canvas;
        }

        private static void AddBar(
            Canvas canvas,
            double x,
            double baseline,
            double width,
            double height,
            Color color,
            object tooltip)
        {
            double visibleHeight = Math.Max(2, height);

            var bar = new Rectangle
            {
                Width = width,
                Height = visibleHeight,
                Fill = new SolidColorBrush(color),
                RadiusX = 3,
                RadiusY = 3,
                ToolTip = tooltip.ToString()
            };

            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, baseline - visibleHeight);
            canvas.Children.Add(bar);
        }

        private static void AddHorizontalGridLines(
            Canvas canvas,
            double width,
            double height,
            double top = 0)
        {
            for (int i = 0; i <= 4; i++)
            {
                double y = top + height * i / 4;

                var line = new Line
                {
                    X1 = 0,
                    X2 = width,
                    Y1 = y,
                    Y2 = y,
                    Stroke = MakeBrush("#E8ECF2"),
                    StrokeThickness = 1
                };

                canvas.Children.Add(line);
            }
        }

        private static void AddCanvasText(
            Canvas canvas,
            string text,
            double x,
            double y,
            double width,
            double height,
            double fontSize)
        {
            var label = new TextBlock
            {
                Text = text,
                Width = width,
                Height = height,
                TextAlignment = TextAlignment.Center,
                FontSize = fontSize,
                Foreground = MakeBrush("#68758A")
            };

            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
        }

        private void AddSectionTitle(string title)
        {
            reportPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = MakeBrush("#26334A"),
                Margin = new Thickness(2, 2, 0, 10)
            });
        }

        private void AddChartCard(string title, UIElement chart)
        {
            var content = new StackPanel();

            content.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = MakeBrush("#26334A"),
                Margin = new Thickness(0, 0, 0, 10)
            });

            content.Children.Add(chart);

            reportPanel.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = MakeBrush("#E0E6EF"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 14),
                Child = content
            });
        }

        private static Border CreateSummaryCard(
            string title,
            string value,
            Color color,
            string subtitle)
        {
            var content = new StackPanel();

            content.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = MakeBrush("#68758A")
            });

            content.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 27,
                FontWeight = FontWeights.Bold,
                Foreground = MakeBrush("#26334A"),
                Margin = new Thickness(0, 4, 0, 0)
            });

            content.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 10,
                Foreground = MakeBrush("#68758A"),
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var cardContent = new Grid();
            cardContent.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(4) });
            cardContent.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var accent = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 12, 0)
            };

            Grid.SetColumn(accent, 0);
            cardContent.Children.Add(accent);

            Grid.SetColumn(content, 1);
            cardContent.Children.Add(content);

            return new Border
            {
                Background = Brushes.White,
                BorderBrush = MakeBrush("#E0E6EF"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 10, 0),
                Child = cardContent
            };
        }

        private static UIElement EmptyChartMessage()
        {
            return new TextBlock
            {
                Text = "No tracker data to show for this range yet.",
                Foreground = MakeBrush("#68758A"),
                Padding = new Thickness(8)
            };
        }

        private static string AverageMarks(int marks, int days)
        {
            if (days == 0)
                return "0.0";

            return (marks / (double)days).ToString("0.0");
        }

        private static DateTime? ToDate(SavedCellPaint entry)
        {
            try
            {
                return new DateTime(entry.Year, entry.Month, entry.Day);
            }
            catch
            {
                return null;
            }
        }

        private static bool PaintContains(SavedCellPaint entry, Color color)
        {
            return ColorMatches(entry.SolidColor, color) ||
                   ColorMatches(entry.FirstColor, color) ||
                   ColorMatches(entry.SecondColor, color);
        }

        private static bool ColorMatches(string? text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                object? converted = ColorConverter.ConvertFromString(text);

                return converted is Color parsed &&
                       parsed == color;
            }
            catch
            {
                return false;
            }
        }

        private static SolidColorBrush MakeBrush(string color)
        {
            return new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(color));
        }
    }
}