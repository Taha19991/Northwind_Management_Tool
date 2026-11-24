# Northwind Management Panel

A layered C# / WPF desktop management application backed by the `northwind` database on PostgreSQL 16.

## Requirements

- **Operating System:** Windows 10 or Windows 11
- **.NET SDK:** .NET 10 SDK (verify with `dotnet --version`)
- **Database:** Pre-populated `northwind` database on PostgreSQL 16 (initialized with `01_northwind.sql`, `02_app_extensions.sql`, `03_sample_data.sql`, `05_app_role.sql`, and `06_console_role.sql` under `docs/db/`)

## Setup and Password Configuration

The application reads connection settings from `Northwind.App/appsettings.json` and local overrides from `Northwind.App/appsettings.Development.json`. `appsettings.Development.json` is excluded via `.gitignore` and is not committed to Git.

Before running the application, create or open `Northwind.App/appsettings.Development.json` and set the passwords for the `nw_app` and `nw_readonly` database roles:

```json
{
  "ConnectionStrings": {
    "Northwind": "Host=localhost;Port=5432;Database=northwind;Username=nw_app;Password=YOUR_REAL_PASSWORD",
    "NorthwindReadOnly": "Host=localhost;Port=5432;Database=northwind;Username=nw_readonly;Password=YOUR_READONLY_PASSWORD"
  },
  "Reports": {
    "ReferenceDate": "1998-05-06"
  }
}
```

> `Reports:ReferenceDate` defaults to `"1998-05-06"` so new orders added with today's date do not skew the "Inactive Customers (Last 6 Months)" report (`"auto"` is still supported and resolves to `MAX(order_date)`).

> CSV exports use `Export:CsvCulture` from `appsettings.json` (defaults to `"en-US"` with `,` delimiter and `.` decimal; set `"tr-TR"` for `;` delimiter and `,` decimal in localized Excel, both with UTF-8 BOM).

## Build and Run

```powershell
dotnet build Northwind.sln
dotnet run --project Northwind.App
```

## Demo Accounts

Password for all demo accounts: **`Demo123!`**

| Username | Role | Accessible Screens |
|---|---|---|
| `manager` | Manager | All screens (including Dashboard, Employees, and Reports) |
| `sales` | Sales | Customers, Products, Orders, New Order |
| `logistics` | Logistics | Products, Shipping, Returns |
| `support` | Support | Support Tickets, Returns, Customers |

## Project Layers

The solution has three projects with dependency direction **App -> Core <- Data**. `Northwind.Core` is a pure domain layer with no external NuGet or project references; it holds models, enums with `EnumText` display/database converters, report row DTOs, filters, `UserFriendlyException`, and repository interfaces. `Northwind.Data` references only `Northwind.Core`; it implements all parameterized async SQL queries via `Npgsql` and `Dapper`, embedded report SQL files (`Sql/Reports/*.sql`), `DateOnly` Dapper type handlers, `PostgresErrorMapper`, and concrete repositories. `Northwind.App` contains the WPF UI, `CommunityToolkit.Mvvm` ViewModels, MaterialDesign styling, LiveCharts2 charts (`ChartFactory`), role-based navigation/session/auth/CSV services, and Generic Host DI registration; it contains no SQL and references `Northwind.Data` only in `App.xaml.cs` startup registration.
