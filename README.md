# Habit Tracker

A desktop habit tracker built with C# and Windows Presentation Foundation (WPF). 
Track categories day by day, review activity in a statistics window, and save or export your data.

## Features

- Create, rename, and delete tracking categories.
- Track entries on a monthly calendar grid.
- Assign marks to two people using separate colors:
  - **Left-click:** mark Person 1.
  - **Right-click:** mark Person 2.
  - **Click the opposite person’s mark:** turn it into a split mark.
  - **Click a marked color again:** clear the mark.
  - **Middle-click:** toggle a split mark.
- View monthly, yearly, and all-time statistics, including activity charts and category comparisons.
- Export tracker entries as CSV or JSON backups.
- Automatically save tracker data and selected display settings locally.
- Change Person colour via HEX codes.

## Requirements

- Windows
- .NET SDK compatible with the project’s target framework
- Visual Studio with the **.NET desktop development** workload, or another compatible .NET IDE.

## Data and exports

The application stores its tracker data in JSON under the current user’s local application data folder: <br />
%LOCALAPPDATA%\HabitTracker\tracker.json (or tracker.CSV)

## Usage

Add a category using the controls at the top of the window. Select a month, then click a category’s day cell to record an entry. The cell’s color indicates who was marked; a split cell represents both people.

The statistics window can display data for the selected month, year, or all saved time.
Built with

    C#
    WPF
    .NET
    System.Text.Json

## Run the application

1. Clone or download this repository.
2. Open the project or solution in Visual Studio.
3. Build and run the project.
