#nullable enable

using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Split3D.Filters;
using Smartstore.Web.Components;

namespace Smartstore.Split3D.Components;

/// <summary>
/// "Quy trình" card on the admin order page (zone order_edit_top, see <see cref="OrderPrintFilesFilter"/>): the studio
/// jobs of the order (print jobs and the job of its products) with their steps and the button for the next one, so the
/// order is worked through right where it is opened. Orders with products but no job yet get a button that creates it.
/// </summary>
public class OrderWorkflowViewComponent : SmartViewComponent
{
    private readonly SmartDbContext _db;
    private readonly StudioOrderClassifier _orderClassifier;

    public OrderWorkflowViewComponent(SmartDbContext db, StudioOrderClassifier orderClassifier)
    {
        _db = db;
        _orderClassifier = orderClassifier;
    }

    public async Task<IViewComponentResult> InvokeAsync(int orderId)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId);
        if (order == null)
        {
            return Empty();
        }

        var jobs = await _db.PrintOrders()
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderBy(x => x.KindId)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var canCreate = order.OrderStatus != OrderStatus.Cancelled
            && order.OrderStatus != OrderStatus.Complete
            && !jobs.Any(x => x.Kind == PrintJobKind.Goods)
            && (await _orderClassifier.GetContentAsync(orderId)).HasFlag(StudioOrderContent.Goods);

        if (jobs.Count == 0 && !canCreate)
        {
            return Empty();
        }

        var model = new OrderWorkflowModel
        {
            OrderId = orderId,
            CanCreate = canCreate,
            CreateUrl = Url.Action("CreateForOrder", "PrintJob", new { orderId, area = "Admin" }),
            Jobs = jobs.Select(x =>
            {
                var isGoods = x.Kind == PrintJobKind.Goods;
                var next = PrintJobSteps.Next(x.Status, isGoods);

                return new OrderWorkflowJob
                {
                    Id = x.Id,
                    Code = x.Code,
                    IsGoods = isGoods,
                    Status = x.Status,
                    Summary = isGoods ? x.Material : x.Technology.HasValue() ? $"{x.Technology} {x.Material}".Trim() : null,
                    NextStatus = next.Status,
                    NextLabel = next.Label,
                    EditUrl = Url.Action("Edit", "PrintJob", new { id = x.Id, area = "Admin" }),
                    StepUrl = Url.Action("OrderStep", "PrintJob", new { jobId = x.Id, area = "Admin" })
                };
            }).ToList()
        };

        return View(model);
    }
}

public class OrderWorkflowModel
{
    public int OrderId { get; set; }

    /// <summary>
    /// Whether the order has products but no goods job yet.
    /// </summary>
    public bool CanCreate { get; set; }

    public string? CreateUrl { get; set; }
    public List<OrderWorkflowJob> Jobs { get; set; } = [];

    /// <summary>
    /// Whether the core "complete" and "cancel" buttons are replaced by the workflow: as long as a job is open.
    /// </summary>
    public bool HasOpenJob => Jobs.Any(x => x.Status != PrintOrderStatus.Completed && x.Status != PrintOrderStatus.Cancelled);
}

public class OrderWorkflowJob
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public bool IsGoods { get; set; }
    public PrintOrderStatus Status { get; set; }
    public string? Summary { get; set; }
    public int? NextStatus { get; set; }
    public string? NextLabel { get; set; }
    public string? EditUrl { get; set; }

    /// <summary>
    /// Target of the step buttons; the job's step is appended as "step" query parameter.
    /// </summary>
    public string? StepUrl { get; set; }
}
