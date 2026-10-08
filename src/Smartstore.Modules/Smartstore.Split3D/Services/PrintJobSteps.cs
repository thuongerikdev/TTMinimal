#nullable enable

namespace Smartstore.Split3D.Services;

/// <summary>
/// The studio workflow of a job (<see cref="PrintOrder"/>) as offered on the admin pages: the steps in order and the
/// step the studio usually takes next. Labels are resource keys, so print jobs and goods can word them differently.
/// </summary>
public static class PrintJobSteps
{
    /// <summary>
    /// The regular path of a job, from "waiting for the money" to "handed over".
    /// </summary>
    public static readonly PrintOrderStatus[] Path =
    [
        PrintOrderStatus.AwaitingPayment,
        PrintOrderStatus.Paid,
        PrintOrderStatus.Confirmed,
        PrintOrderStatus.Printing,
        PrintOrderStatus.Ready,
        PrintOrderStatus.Completed
    ];

    /// <summary>
    /// Gets the next step after <paramref name="status"/> and the resource key of its button label,
    /// or <c>(null, null)</c> if the job is finished or cancelled.
    /// </summary>
    public static (int? Status, string? Label) Next(PrintOrderStatus status, bool isGoods) => status switch
    {
        PrintOrderStatus.Draft or PrintOrderStatus.AwaitingPayment => ((int)PrintOrderStatus.Paid, "Plugins.Split3D.PrintJob.Action.MarkPaid"),
        PrintOrderStatus.Paid => ((int)PrintOrderStatus.Confirmed, "Plugins.Split3D.PrintJob.Action.Confirm"),
        PrintOrderStatus.Confirmed => ((int)PrintOrderStatus.Printing, isGoods ? "Plugins.Split3D.PrintJob.Action.StartMaking" : "Plugins.Split3D.PrintJob.Action.StartPrinting"),
        PrintOrderStatus.Printing => ((int)PrintOrderStatus.Ready, isGoods ? "Plugins.Split3D.PrintJob.Action.Made" : "Plugins.Split3D.PrintJob.Action.Ready"),
        PrintOrderStatus.Ready => ((int)PrintOrderStatus.Completed, "Plugins.Split3D.PrintJob.Action.Complete"),
        _ => (null, null)
    };

    /// <summary>
    /// Resource key of the name of a step, with the goods wording for the production steps.
    /// </summary>
    public static string StatusKey(PrintOrderStatus status, bool isGoods)
        => isGoods && status is PrintOrderStatus.Printing or PrintOrderStatus.Ready
            ? "Plugins.Split3D.PrintJob.GoodsStatus." + status
            : "Plugins.Split3D.PrintJob.Status." + status;
}
