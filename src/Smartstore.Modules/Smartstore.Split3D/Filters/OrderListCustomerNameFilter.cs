#nullable enable

using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Smartstore.Core.Data;
using Smartstore.Web.Models.DataGrid;

namespace Smartstore.Split3D.Filters;

/// <summary>
/// Shows the current name of the customer account in the "Customer" column of the admin order list. Smartstore shows
/// the name of the order's billing address (a copy taken when the order was placed), so renaming a customer never
/// reached existing orders. Rows of customers without a name keep the address name.
/// Registered for Order/OrderList (the grid's data request), see Startup.
/// </summary>
public class OrderListCustomerNameFilter : IAsyncResultFilter
{
    private readonly SmartDbContext _db;

    public OrderListCustomerNameFilter(SmartDbContext db)
    {
        _db = db;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is JsonResult { Value: IGridModel grid } && grid.Rows is System.Collections.IList { Count: > 0 } rows)
        {
            // The row type (OrderOverviewModel) lives in the web project, which modules do not reference.
            var type = rows[0]!.GetType();
            var idProperty = type.GetProperty("CustomerId", BindingFlags.Public | BindingFlags.Instance);
            var nameProperty = type.GetProperty("CustomerName", BindingFlags.Public | BindingFlags.Instance);

            if (idProperty?.PropertyType == typeof(int) && nameProperty?.PropertyType == typeof(string) && nameProperty.CanWrite)
            {
                var ids = rows.Cast<object>().Select(x => (int)idProperty.GetValue(x)!).Where(x => x > 0).Distinct().ToArray();
                var names = await _db.Customers
                    .AsNoTracking()
                    .Where(x => ids.Contains(x.Id) && x.FullName != null && x.FullName != string.Empty)
                    .Select(x => new { x.Id, x.FullName })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName);

                foreach (var row in rows)
                {
                    if (names.TryGetValue((int)idProperty.GetValue(row)!, out var name) && name.HasValue())
                    {
                        nameProperty.SetValue(row, name);
                    }
                }
            }
        }

        await next();
    }
}
