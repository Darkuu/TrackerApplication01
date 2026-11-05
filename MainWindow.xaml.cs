using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Tracker
{
    public sealed class TrackerSaveData
    {
        public List<string> Categories { get; set; } = new();
        public List<SavedCellPaint> CellPaints { get; set; } = new();

        public int DisplayYear { get; set; }
        public int DisplayMonth { get; set; }

        public string Person1Color { get; set; } = "#90EE90";
        public string Person2Color { get; set; } = "#ADD8E6";

        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public double WindowWidth { get; set; } = 1100;
        public double WindowHeight { get; set; } = 720;
        public bool WasMaximized { get; set; }
    }

    public sealed class SavedCellPaint
    {
        public string Category { get; set; } = "";
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }

        public string? SolidColor { get; set; }
        public string? FirstColor { get; set; }
        public string? SecondColor { get; set; }
    }

    public partial class MainWindow : Window
    {
        private sealed class CellPaint
        {
            public string? SolidColor { get; set; }
            public string? FirstColor { get; set; }
            public string? SecondColor { get; set; }
        }

        private readonly List<string> categories = new();

        private readonly Dictionary<
            (string Category, int Year, int Month, int Day),
            CellPaint> cellPaints = new();

        private readonly string saveFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HabitTracker",
            "tracker.json");

        private DateTime displayedMonth =
            new(DateTime.Today.Year, DateTime.Today.Month, 1);

        private double savedWindowLeft = 100;
        private double savedWindowTop = 100;
        private double savedWindowWidth = 1100;
        private double savedWindowHeight = 720;
        private bool savedWasMaximized;
        private DispatcherTimer? saveTimer;

        public MainWindow()
        {
            InitializeComponent();

            saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(450)
            };

            saveTimer.Tick += (_, _) =>
            {
                saveTimer.Stop();
                SaveData(false);
            };

            LoadData();
            RestoreWindowSettings();
            PopulateMonthSelector();

            MonthSelector.SelectionChanged += MonthSelector_SelectionChanged;

            RefreshCategorySelector();
            RefreshColorPreviews();
            RefreshGrid();
        }

        private void PopulateMonthSelector()
        {
            MonthSelector.Items.Clear();

            int firstYear = Math.Min(
                Math.Max(1, DateTime.Today.Year - 10),
                displayedMonth.Year);

            int lastYear = Math.Max(
                Math.Min(9999, DateTime.Today.Year + 10),
                displayedMonth.Year);

            for (int year = firstYear; year <= lastYear; year++)
            {
                MonthSelector.Items.Add(new ComboBoxItem
                {
                    IsEnabled = false,
                    Padding = new Thickness(6, 5, 6, 5),
                    Background = Brushes.LightGray,
                    Content = new TextBlock
                    {
                        Text = year.ToString(),
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Black
                    }
                });

                for (int month = 1; month <= 12; month++)
                {
                    DateTime date = new(year, month, 1);

                    var item = new ComboBoxItem
                    {
                        Tag = date,
                        Padding = new Thickness(18, 4, 6, 4),
                        Content = date.ToString("MMMM")
                    };

                    MonthSelector.Items.Add(item);

                    if (year == displayedMonth.Year &&
                        month == displayedMonth.Month)
                    {
                        MonthSelector.SelectedItem = item;
                    }
                }
            }
        }

        private void MonthSelector_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (MonthSelector.SelectedItem is ComboBoxItem item &&
                item.Tag is DateTime selectedMonth)
            {
                displayedMonth = selectedMonth;
                RefreshGrid();
                ScheduleSave();
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            string name = CategoryInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
                return;

            if (categories.Any(c =>
                    string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "That category already exists.",
                    "Duplicate category",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            categories.Add(name);
            CategoryInput.Clear();

            RefreshCategorySelector();
            RefreshGrid();
            ScheduleSave();
        }

        private void RenameCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryToManage.SelectedItem is not string oldName)
            {
                MessageBox.Show(
                    "Choose a category to rename.",
                    "No category selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string newName = CategoryInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show(
                    "Enter the new category name in the category name box.",
                    "Name required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (categories.Any(c =>
                    !string.Equals(c, oldName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c, newName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "A category with that name already exists.",
                    "Duplicate category",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int index = categories.IndexOf(oldName);
            if (index < 0)
                return;

            categories[index] = newName;

            var oldEntries = cellPaints
                .Where(entry => entry.Key.Category == oldName)
                .ToList();

            foreach (var entry in oldEntries)
            {
                cellPaints.Remove(entry.Key);

                var newKey = (
                    Category: newName,
                    Year: entry.Key.Year,
                    Month: entry.Key.Month,
                    Day: entry.Key.Day);

                cellPaints[newKey] = entry.Value;
            }

            CategoryInput.Clear();
            RefreshCategorySelector();
            RefreshGrid();
            ScheduleSave();
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryToManage.SelectedItem is not string name)
            {
                MessageBox.Show(
                    "Choose a category to delete.",
                    "No category selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                $"Delete '{name}' and all of its saved entries?",
                "Delete category",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            categories.Remove(name);

            var keysToRemove = cellPaints.Keys
                .Where(key => key.Category == name)
                .ToList();

            foreach (var key in keysToRemove)
                cellPaints.Remove(key);

            RefreshCategorySelector();
            RefreshGrid();
            ScheduleSave();
        }

        private void RefreshCategorySelector()
        {
            CategoryToManage.ItemsSource = null;
            CategoryToManage.ItemsSource = categories.ToList();
            CategoryToManage.SelectedIndex = -1;
        }

        private void ColorInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (LeftColorPreview == null || RightColorPreview == null)
                return;

            RefreshColorPreviews();
            ScheduleSave();
        }

        private void RefreshColorPreviews()
        {
            if (TryReadColor(LeftColorInput.Text, out Color left))
            {
                LeftColorPreview.Background = new SolidColorBrush(left);
                LeftColorPreview.ToolTip = left.ToString();
            }
            else
            {
                LeftColorPreview.Background = Brushes.White;
                LeftColorPreview.ToolTip = "Invalid color";
            }

            if (TryReadColor(RightColorInput.Text, out Color right))
            {
                RightColorPreview.Background = new SolidColorBrush(right);
                RightColorPreview.ToolTip = right.ToString();
            }
            else
            {
                RightColorPreview.Background = Brushes.White;
                RightColorPreview.ToolTip = "Invalid color";
            }
        }

        private void RefreshGrid()
        {
            TrackerGrid.Children.Clear();
            TrackerGrid.RowDefinitions.Clear();
            TrackerGrid.ColumnDefinitions.Clear();

            // Use a valid one-cell layout for the empty state.
            if (categories.Count == 0)
            {
                TrackerGrid.ColumnDefinitions.Add(new ColumnDefinition());
                TrackerGrid.RowDefinitions.Add(
                    new RowDefinition { Height = GridLength.Auto });

                var emptyState = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 253)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(230, 234, 241)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20),
                    Margin = new Thickness(8),
                    Child = new TextBlock
                    {
                        Text = "No categories yet. Add one above to start tracking.",
                        Foreground = new SolidColorBrush(Color.FromRgb(104, 117, 138)),
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap
                    }
                };

                Grid.SetRow(emptyState, 0);
                Grid.SetColumn(emptyState, 0);
                TrackerGrid.Children.Add(emptyState);
                return;
            }

            int daysInMonth = DateTime.DaysInMonth(
                displayedMonth.Year,
                displayedMonth.Month);

            TrackerGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(190) });

            for (int day = 1; day <= daysInMonth; day++)
            {
                TrackerGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(42) });
            }

            TrackerGrid.RowDefinitions.Add(
                new RowDefinition { Height = new GridLength(48) });

            for (int row = 0; row < categories.Count; row++)
            {
                TrackerGrid.RowDefinitions.Add(
                    new RowDefinition { Height = new GridLength(54) });
            }

            AddHeader("Category", 0, false, false);

            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime date = new(
                    displayedMonth.Year,
                    displayedMonth.Month,
                    day);

                bool isWeekend =
                    date.DayOfWeek == DayOfWeek.Saturday ||
                    date.DayOfWeek == DayOfWeek.Sunday;

                bool isToday = date.Date == DateTime.Today;

                AddHeader($"{day}\n{date:ddd}", day, isWeekend, isToday);
            }

            for (int row = 0; row < categories.Count; row++)
            {
                string category = categories[row];
                int completedDays = CountPaintedDays(category, displayedMonth);
                int summaryDayCount = GetSummaryDayCount(
                    displayedMonth,
                    daysInMonth);

                double percent = summaryDayCount == 0
                    ? 0
                    : completedDays * 100.0 / summaryDayCount;

                AddCategoryLabel(
                    category,
                    $"{completedDays} of {summaryDayCount} tracked days · {percent:0}%",
                    row + 1);

                for (int day = 1; day <= daysInMonth; day++)
                {
                    var key = (
                        Category: category,
                        Year: displayedMonth.Year,
                        Month: displayedMonth.Month,
                        Day: day);

                    DateTime date = new(
                        displayedMonth.Year,
                        displayedMonth.Month,
                        day);

                    bool isWeekend =
                        date.DayOfWeek == DayOfWeek.Saturday ||
                        date.DayOfWeek == DayOfWeek.Sunday;

                    bool isToday = date.Date == DateTime.Today;

                    var cell = new Button
                    {
                        Margin = new Thickness(2),
                        Padding = new Thickness(0),
                        BorderBrush = isToday
                            ? new SolidColorBrush(Color.FromRgb(82, 101, 232))
                            : new SolidColorBrush(Color.FromRgb(221, 227, 236)),
                        BorderThickness = isToday
                            ? new Thickness(2)
                            : new Thickness(1),
                        Background = isWeekend
                            ? new SolidColorBrush(Color.FromRgb(245, 247, 251))
                            : new SolidColorBrush(Color.FromRgb(250, 251, 253)),
                        Focusable = true,
                        IsTabStop = true,
                        ToolTip = $"{category} — {displayedMonth:MMMM} {day}\n" +
                                  "Left-click: Person 1\n" +
                                  "Right-click: Person 2\n" +
                                  "Middle-click: split"
                    };

                    AutomationProperties.SetName(
                        cell,
                        $"{category}, {displayedMonth:MMMM} {day}. " +
                        "Left-click marks Person 1. " +
                        "Right-click marks Person 2. " +
                        "Middle-click toggles a split.");

                    if (cellPaints.TryGetValue(key, out CellPaint? paint))
                        ApplyPaint(cell, paint);

                    // MB1: Person 1.
                    cell.PreviewMouseLeftButtonDown += (_, e) =>
                    {
                        e.Handled = true;
                        cell.Focus();
                        TogglePerson1(key);
                    };

                    // MB2: Person 2.
                    cell.PreviewMouseRightButtonDown += (_, e) =>
                    {
                        e.Handled = true;
                        cell.Focus();
                        TogglePerson2(key);
                    };

                    // Middle-click: create a split, or clear an existing split.
                    cell.PreviewMouseDown += (_, e) =>
                    {
                        if (e.ChangedButton == MouseButton.Middle)
                        {
                            e.Handled = true;
                            cell.Focus();
                            ToggleSplit(key);
                        }
                    };

                    // Keyboard activation toggles Person 1.
                    cell.PreviewKeyDown += (_, e) =>
                    {
                        if (e.Key == Key.Enter || e.Key == Key.Space)
                        {
                            e.Handled = true;
                            TogglePerson1(key);
                        }
                    };

                    Grid.SetRow(cell, row + 1);
                    Grid.SetColumn(cell, day);
                    TrackerGrid.Children.Add(cell);
                }
            }
        }

        private static int GetSummaryDayCount(DateTime month, int daysInMonth)
        {
            if (month.Year == DateTime.Today.Year &&
                month.Month == DateTime.Today.Month)
            {
                return DateTime.Today.Day;
            }

            return daysInMonth;
        }

        private int CountPaintedDays(string category, DateTime month)
        {
            return cellPaints.Count(entry =>
                entry.Key.Category == category &&
                entry.Key.Year == month.Year &&
                entry.Key.Month == month.Month);
        }

        private void AddHeader(
            string text,
            int column,
            bool weekend,
            bool isToday)
        {
            var border = new Border
            {
                Margin = new Thickness(1),
                Padding = new Thickness(2),
                Background = isToday
                    ? new SolidColorBrush(Color.FromRgb(232, 237, 255))
                    : weekend
                        ? new SolidColorBrush(Color.FromRgb(239, 242, 247))
                        : Brushes.White,
                BorderBrush = isToday
                    ? new SolidColorBrush(Color.FromRgb(82, 101, 232))
                    : new SolidColorBrush(Color.FromRgb(230, 234, 241)),
                BorderThickness = isToday
                    ? new Thickness(1, 1, 1, 2)
                    : new Thickness(0, 0, 0, 1),
                Child = new TextBlock
                {
                    Text = text,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = column == 0 ? 12 : 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(74, 87, 108))
                }
            };

            Grid.SetRow(border, 0);
            Grid.SetColumn(border, column);
            TrackerGrid.Children.Add(border);
        }

        private void AddCategoryLabel(string name, string summary, int row)
        {
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 2, 4, 2)
            };

            panel.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(38, 51, 74)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            panel.Children.Add(new TextBlock
            {
                Text = summary,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(104, 117, 138)),
                Margin = new Thickness(0, 3, 0, 0)
            });

            Grid.SetRow(panel, row);
            Grid.SetColumn(panel, 0);
            TrackerGrid.Children.Add(panel);
        }

        private void TogglePerson1(
            (string Category, int Year, int Month, int Day) key)
        {
            if (!TryReadColor(LeftColorInput.Text, out Color person1) ||
                !TryReadColor(RightColorInput.Text, out Color person2))
            {
                ShowInvalidColor();
                return;
            }

            TogglePersonColor(key, person1, person2);
        }

        private void TogglePerson2(
            (string Category, int Year, int Month, int Day) key)
        {
            if (!TryReadColor(LeftColorInput.Text, out Color person1) ||
                !TryReadColor(RightColorInput.Text, out Color person2))
            {
                ShowInvalidColor();
                return;
            }

            TogglePersonColor(key, person2, person1);
        }

        private void TogglePersonColor(
            (string Category, int Year, int Month, int Day) key,
            Color clickedColor,
            Color otherPersonColor)
        {
            if (cellPaints.TryGetValue(key, out CellPaint? existing))
            {
                if (IsSplitPaint(existing))
                {
                    // Clicking either person on a split clears the cell.
                    if (ColorMatches(existing.FirstColor, clickedColor) ||
                        ColorMatches(existing.SecondColor, clickedColor))
                    {
                        cellPaints.Remove(key);
                    }
                    else
                    {
                        cellPaints[key] = new CellPaint
                        {
                            SolidColor = clickedColor.ToString()
                        };
                    }
                }
                else if (existing.SolidColor != null &&
                         ColorMatches(existing.SolidColor, clickedColor))
                {
                    // Clicking the same person's mark clears it.
                    cellPaints.Remove(key);
                }
                else if (existing.SolidColor != null &&
                         ColorMatches(existing.SolidColor, otherPersonColor))
                {
                    // Clicking the opposite person turns the cell into a split.
                    if (clickedColor == otherPersonColor)
                    {
                        MessageBox.Show(
                            "Choose different colors for Person 1 and Person 2 to create a split.",
                            "Colors must be different",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }

                    SetSplitPaint(key);
                }
                else
                {
                    // Replace a mark that doesn't match either current color.
                    cellPaints[key] = new CellPaint
                    {
                        SolidColor = clickedColor.ToString()
                    };
                }
            }
            else
            {
                cellPaints[key] = new CellPaint
                {
                    SolidColor = clickedColor.ToString()
                };
            }

            RefreshGrid();
            ScheduleSave();
        }

        private void ToggleSplit(
            (string Category, int Year, int Month, int Day) key)
        {
            if (cellPaints.TryGetValue(key, out CellPaint? existing) &&
                IsSplitPaint(existing))
            {
                cellPaints.Remove(key);
            }
            else
            {
                if (!TryReadColor(LeftColorInput.Text, out Color person1) ||
                    !TryReadColor(RightColorInput.Text, out Color person2))
                {
                    ShowInvalidColor();
                    return;
                }

                if (person1 == person2)
                {
                    MessageBox.Show(
                        "Choose different colors for Person 1 and Person 2 to create a split.",
                        "Colors must be different",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                cellPaints[key] = new CellPaint
                {
                    FirstColor = person1.ToString(),
                    SecondColor = person2.ToString()
                };
            }

            RefreshGrid();
            ScheduleSave();
        }

        private void SetSplitPaint(
            (string Category, int Year, int Month, int Day) key)
        {
            if (!TryReadColor(LeftColorInput.Text, out Color person1) ||
                !TryReadColor(RightColorInput.Text, out Color person2))
            {
                ShowInvalidColor();
                return;
            }

            if (person1 == person2)
            {
                MessageBox.Show(
                    "Choose different colors for Person 1 and Person 2 to create a split.",
                    "Colors must be different",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            cellPaints[key] = new CellPaint
            {
                FirstColor = person1.ToString(),
                SecondColor = person2.ToString()
            };
        }

        private static bool IsSplitPaint(CellPaint paint)
        {
            return !string.IsNullOrWhiteSpace(paint.FirstColor) &&
                   !string.IsNullOrWhiteSpace(paint.SecondColor);
        }

        private void Stats_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadColor(LeftColorInput.Text, out Color person1Color) ||
                !TryReadColor(RightColorInput.Text, out Color person2Color))
            {
                ShowInvalidColor();
                return;
            }

            var savedEntries = cellPaints.Select(entry => new SavedCellPaint
            {
                Category = entry.Key.Category,
                Year = entry.Key.Year,
                Month = entry.Key.Month,
                Day = entry.Key.Day,
                SolidColor = entry.Value.SolidColor,
                FirstColor = entry.Value.FirstColor,
                SecondColor = entry.Value.SecondColor
            }).ToList();

            var statsWindow = new StatsWindow(
                displayedMonth,
                person1Color,
                person2Color,
                categories.ToList(),
                savedEntries)
            {
                Owner = this
            };

            statsWindow.ShowDialog();
        }

        private static void ApplyPaint(Button cell, CellPaint paint)
        {
            if (paint.SolidColor != null &&
                TryReadColor(paint.SolidColor, out Color solid))
            {
                cell.Background = new SolidColorBrush(solid);
                return;
            }

            if (paint.FirstColor != null &&
                paint.SecondColor != null &&
                TryReadColor(paint.FirstColor, out Color first) &&
                TryReadColor(paint.SecondColor, out Color second))
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };

                brush.GradientStops.Add(new GradientStop(first, 0.0));
                brush.GradientStops.Add(new GradientStop(first, 0.5));
                brush.GradientStops.Add(new GradientStop(second, 0.5));
                brush.GradientStops.Add(new GradientStop(second, 1.0));

                cell.Background = brush;
            }
        }

        private static bool ColorMatches(string? savedColor, Color color)
        {
            return savedColor != null &&
                   TryReadColor(savedColor, out Color parsedColor) &&
                   parsedColor == color;
        }

        private static bool TryReadColor(string? text, out Color color)
        {
            try
            {
                object? converted = ColorConverter.ConvertFromString(text ?? "");

                if (converted is Color parsed)
                {
                    color = parsed;
                    return true;
                }
            }
            catch
            {
                // Invalid color text.
            }

            color = Colors.White;
            return false;
        }

        private void ShowInvalidColor()
        {
            MessageBox.Show(
                "Enter a valid color, such as #7C4DFF or CornflowerBlue.",
                "Invalid color",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            saveTimer?.Stop();
            SaveData(true);
        }

        private void Window_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            saveTimer?.Stop();
            SaveData(false);
        }

        private void ScheduleSave()
        {
            if (saveTimer == null)
                return;

            SaveStatus.Text = "Unsaved changes…";
            saveTimer.Stop();
            saveTimer.Start();
        }

        private void SaveData(bool showError)
        {
            string? temporaryPath = null;

            try
            {
                var data = new TrackerSaveData
                {
                    Categories = new List<string>(categories),
                    DisplayYear = displayedMonth.Year,
                    DisplayMonth = displayedMonth.Month,
                    Person1Color = LeftColorInput.Text.Trim(),
                    Person2Color = RightColorInput.Text.Trim(),
                    WasMaximized = WindowState == WindowState.Maximized
                };

                Rect bounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, Width, Height)
                    : RestoreBounds;

                data.WindowLeft = bounds.Left;
                data.WindowTop = bounds.Top;
                data.WindowWidth = bounds.Width;
                data.WindowHeight = bounds.Height;

                foreach (var entry in cellPaints)
                {
                    data.CellPaints.Add(new SavedCellPaint
                    {
                        Category = entry.Key.Category,
                        Year = entry.Key.Year,
                        Month = entry.Key.Month,
                        Day = entry.Key.Day,
                        SolidColor = entry.Value.SolidColor,
                        FirstColor = entry.Value.FirstColor,
                        SecondColor = entry.Value.SecondColor
                    });
                }

                string? directory = Path.GetDirectoryName(saveFilePath);
                if (directory == null)
                    throw new IOException("Could not determine the save folder.");

                Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(
                    data,
                    new JsonSerializerOptions { WriteIndented = true });

                temporaryPath = saveFilePath + ".tmp";
                File.WriteAllText(temporaryPath, json);

                if (File.Exists(saveFilePath))
                    File.Replace(temporaryPath, saveFilePath, null);
                else
                    File.Move(temporaryPath, saveFilePath);

                SaveStatus.Text = $"Saved at {DateTime.Now:t}";
            }
            catch (Exception ex)
            {
                SaveStatus.Text = "Save failed.";

                if (showError)
                {
                    MessageBox.Show(
                        $"Could not save tracker data.\n\n{ex.Message}",
                        "Save error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            finally
            {
                if (temporaryPath != null && File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch
                    {
                        // Ignore temporary-file cleanup failures.
                    }
                }
            }
        }

        private void LoadData()
        {
            try
            {
                if (!File.Exists(saveFilePath))
                    return;

                string json = File.ReadAllText(saveFilePath);

                TrackerSaveData? data =
                    JsonSerializer.Deserialize<TrackerSaveData>(json);

                if (data == null)
                    return;

                categories.Clear();
                categories.AddRange(
                    (data.Categories ?? new List<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));

                cellPaints.Clear();

                foreach (SavedCellPaint saved in
                         data.CellPaints ?? new List<SavedCellPaint>())
                {
                    if (string.IsNullOrWhiteSpace(saved.Category) ||
                        saved.Year < 1 ||
                        saved.Year > 9999 ||
                        saved.Month < 1 ||
                        saved.Month > 12 ||
                        saved.Day < 1 ||
                        saved.Day > DateTime.DaysInMonth(saved.Year, saved.Month))
                    {
                        continue;
                    }

                    var key = (
                        Category: saved.Category,
                        Year: saved.Year,
                        Month: saved.Month,
                        Day: saved.Day);

                    cellPaints[key] = new CellPaint
                    {
                        SolidColor = saved.SolidColor,
                        FirstColor = saved.FirstColor,
                        SecondColor = saved.SecondColor
                    };
                }

                if (data.DisplayYear >= 1 &&
                    data.DisplayYear <= 9999 &&
                    data.DisplayMonth >= 1 &&
                    data.DisplayMonth <= 12)
                {
                    displayedMonth = new DateTime(
                        data.DisplayYear,
                        data.DisplayMonth,
                        1);
                }

                if (TryReadColor(data.Person1Color, out _))
                    LeftColorInput.Text = data.Person1Color;

                if (TryReadColor(data.Person2Color, out _))
                    RightColorInput.Text = data.Person2Color;

                savedWindowLeft = data.WindowLeft;
                savedWindowTop = data.WindowTop;
                savedWindowWidth = data.WindowWidth;
                savedWindowHeight = data.WindowHeight;
                savedWasMaximized = data.WasMaximized;
            }
            catch
            {
                // Keep defaults if the save file cannot be read.
            }
        }

        private void RestoreWindowSettings()
        {
            Width = Math.Max(MinWidth, savedWindowWidth);
            Height = Math.Max(MinHeight, savedWindowHeight);

            double maxLeft = Math.Max(
                0,
                SystemParameters.VirtualScreenWidth - Width);

            double maxTop = Math.Max(
                0,
                SystemParameters.VirtualScreenHeight - Height);

            Left = Math.Max(0, Math.Min(savedWindowLeft, maxLeft));
            Top = Math.Max(0, Math.Min(savedWindowTop, maxTop));

            if (savedWasMaximized)
                WindowState = WindowState.Maximized;
        }

        private void BackupJson_Click(object sender, RoutedEventArgs e)
        {
            saveTimer?.Stop();
            SaveData(false);

            if (!File.Exists(saveFilePath))
                return;

            var dialog = new SaveFileDialog
            {
                Title = "Save tracker backup",
                FileName = $"tracker-backup-{DateTime.Now:yyyy-MM-dd}.json",
                Filter = "JSON files (*.json)|*.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                File.Copy(saveFilePath, dialog.FileName, true);
                SaveStatus.Text = "JSON backup created.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not create backup.\n\n{ex.Message}",
                    "Backup error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export tracker entries",
                FileName = $"tracker-export-{DateTime.Now:yyyy-MM-dd}.csv",
                Filter = "CSV files (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("Category,Date,Paint");

                foreach (var entry in cellPaints
                             .OrderBy(e => e.Key.Year)
                             .ThenBy(e => e.Key.Month)
                             .ThenBy(e => e.Key.Day)
                             .ThenBy(e => e.Key.Category))
                {
                    string date = new DateTime(
                        entry.Key.Year,
                        entry.Key.Month,
                        entry.Key.Day).ToString("yyyy-MM-dd");

                    string paint = entry.Value.SolidColor != null
                        ? entry.Value.SolidColor
                        : $"{entry.Value.FirstColor} / {entry.Value.SecondColor}";

                    csv.AppendLine(
                        $"{Csv(entry.Key.Category)},{Csv(date)},{Csv(paint)}");
                }

                File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
                SaveStatus.Text = "CSV export completed.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not export CSV.\n\n{ex.Message}",
                    "Export error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string Csv(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}