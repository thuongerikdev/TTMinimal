#nullable enable

namespace Smartstore.Split3D.Models;

/// <summary>
/// The studio "workbench" that replaces the core admin dashboard (see <c>StudioDashboardController</c>).
/// Money values are in the primary store currency.
/// </summary>
public class StudioDashboardModel
{
    public string? UserName { get; set; }

    /// <summary>Now in the time zone of the current user.</summary>
    public DateTime Now { get; set; }

    /// <summary>Work waiting for the studio, in the order it should be done.</summary>
    public List<StudioDashboardTodo> Todos { get; set; } = [];

    public List<StudioDashboardKpi> Kpis { get; set; } = [];

    /// <summary>Revenue and order count per local day, oldest first, ending today.</summary>
    public List<StudioDashboardDay> Days { get; set; } = [];

    /// <summary>Orders per hour of the day (0–23) over the last 90 days.</summary>
    public int[] Hours { get; set; } = new int[24];

    public decimal TodayRevenue { get; set; }

    public int TodayOrders { get; set; }

    public decimal YesterdayRevenue { get; set; }

    public decimal MonthRevenue { get; set; }

    /// <summary>Month-to-date revenue extrapolated to the whole month.</summary>
    public decimal MonthForecast { get; set; }

    public decimal LastMonthRevenue { get; set; }

    /// <summary>Revenue of the last 30 days split by business line.</summary>
    public List<StudioDashboardShare> Mix { get; set; } = [];

    public List<StudioDashboardJob> Jobs { get; set; } = [];

    /// <summary>Grams of filament of the print jobs finished this month.</summary>
    public int GramsThisMonth { get; set; }

    public int GramsLastMonth { get; set; }

    public List<StudioDashboardOrder> LatestOrders { get; set; } = [];

    public List<StudioDashboardProduct> TopProducts { get; set; } = [];

    public int ActiveKeys { get; set; }

    public int DevicesOnline { get; set; }

    public int DevicesToday { get; set; }

    public int DevicesActive { get; set; }

    /// <summary>Most used addon versions among the active devices.</summary>
    public List<StudioDashboardShare> AddonVersions { get; set; } = [];

    public List<StudioDashboardKey> ExpiringKeys { get; set; } = [];
}

public class StudioDashboardTodo
{
    public string Title { get; set; } = string.Empty;

    public string? Hint { get; set; }

    public int Count { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Icon { get; set; } = "bi-check2";

    /// <summary>Card color: pink, yellow, mint, lilac or sky.</summary>
    public string Tone { get; set; } = "yellow";
}

public class StudioDashboardKpi
{
    public string Label { get; set; } = string.Empty;

    public decimal Value { get; set; }

    /// <summary>Value of the period compared against, null if there is none.</summary>
    public decimal? Previous { get; set; }

    public bool IsMoney { get; set; }

    public string? Caption { get; set; }

    public string Icon { get; set; } = "bi-graph-up";

    public string Tone { get; set; } = "yellow";

    public string? Url { get; set; }

    /// <summary>Small trend line, oldest first.</summary>
    public List<decimal> Spark { get; set; } = [];
}

public class StudioDashboardDay
{
    /// <summary>Local date as yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;

    public decimal Revenue { get; set; }

    public int Orders { get; set; }
}

public class StudioDashboardShare
{
    public string Label { get; set; } = string.Empty;

    public decimal Value { get; set; }

    public string Tone { get; set; } = "yellow";
}

public class StudioDashboardJob
{
    public int Id { get; set; }

    public string? Code { get; set; }

    public string? Customer { get; set; }

    public int StatusId { get; set; }

    public int Grams { get; set; }

    public int Pieces { get; set; }

    public string? Material { get; set; }

    /// <summary>Local date the customer needs the print, if any.</summary>
    public DateTime? DesiredOn { get; set; }

    public string Url { get; set; } = string.Empty;
}

public class StudioDashboardOrder
{
    public string Number { get; set; } = string.Empty;

    public string Customer { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public DateTime CreatedOn { get; set; }

    public string Status { get; set; } = string.Empty;

    /// <summary>pending, processing, complete or cancelled.</summary>
    public string StatusKey { get; set; } = string.Empty;

    public bool Paid { get; set; }

    /// <summary>Business line of the order: "In 3D", "Sản phẩm", "Công cụ 3D" or a combination.</summary>
    public string? Kind { get; set; }

    public string Url { get; set; } = string.Empty;
}

public class StudioDashboardProduct
{
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Revenue { get; set; }

    public string Url { get; set; } = string.Empty;
}

public class StudioDashboardKey
{
    public string Customer { get; set; } = string.Empty;

    public string? Product { get; set; }

    public int DaysLeft { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }
}
