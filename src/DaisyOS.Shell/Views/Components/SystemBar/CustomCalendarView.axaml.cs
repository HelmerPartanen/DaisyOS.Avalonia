using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace DaisyOS.Shell.Views.Components.SystemBar;

public partial class CustomCalendarView : UserControl
{
    private DateTime _displayDate;

    public CustomCalendarView()
    {
        InitializeComponent();
        _displayDate = DateTime.Now;
        RenderCalendar();
        UpdateTodayFooter();
    }

    private void OnPreviousMonthClicked(object? sender, RoutedEventArgs e)
    {
        _displayDate = _displayDate.AddMonths(-1);
        RenderCalendar();
    }

    private void OnNextMonthClicked(object? sender, RoutedEventArgs e)
    {
        _displayDate = _displayDate.AddMonths(1);
        RenderCalendar();
    }

    private void UpdateTodayFooter()
    {
        var now = DateTime.Now;
        TodayDayText.Text = now.ToString("dd");
        TodayMonthText.Text = now.ToString("MMM");
        TodayYearText.Text = now.ToString("yyyy");
    }

    public void RenderCalendar()
    {
        MonthText.Text = _displayDate.ToString("MMMM");
        YearText.Text = _displayDate.ToString("yyyy");

        DaysGrid.Children.Clear();

        var firstDayOfMonth = new DateTime(_displayDate.Year, _displayDate.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(_displayDate.Year, _displayDate.Month);

        // Calculate leading days from previous month (Monday = 1, Sunday = 7)
        int startingDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
        if (startingDayOfWeek == 0) startingDayOfWeek = 7; // Sunday is 0, make it 7
        int leadingDays = startingDayOfWeek - 1;

        var prevMonth = firstDayOfMonth.AddMonths(-1);
        int prevMonthDays = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);

        var today = DateTime.Today;

        // Fill previous month days
        for (int i = 0; i < leadingDays; i++)
        {
            int day = prevMonthDays - leadingDays + i + 1;
            DaysGrid.Children.Add(CreateDayCell(day, isCurrentMonth: false, isToday: false));
        }

        // Fill current month days
        for (int i = 1; i <= daysInMonth; i++)
        {
            bool isToday = _displayDate.Year == today.Year && _displayDate.Month == today.Month && i == today.Day;
            DaysGrid.Children.Add(CreateDayCell(i, isCurrentMonth: true, isToday: isToday));
        }

        // Fill next month days
        int trailingDays = 42 - (leadingDays + daysInMonth);
        for (int i = 1; i <= trailingDays; i++)
        {
            DaysGrid.Children.Add(CreateDayCell(i, isCurrentMonth: false, isToday: false));
        }
    }

    private Control CreateDayCell(int day, bool isCurrentMonth, bool isToday)
    {
        var border = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18)
        };

        var textBlock = new TextBlock
        {
            Text = day.ToString(),
            FontSize = 14,
            FontWeight = isToday ? FontWeight.Bold : FontWeight.Regular,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isToday)
        {
            border.Bind(Border.BackgroundProperty, this.GetResourceObservable("TextPrimaryBrush"));
            textBlock.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("PrimarySurfaceBrush"));
        }
        else if (isCurrentMonth)
        {
            textBlock.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("TextPrimaryBrush"));
        }
        else
        {
            textBlock.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("TextTertiaryBrush"));
        }

        border.Child = textBlock;
        return border;
    }
}
