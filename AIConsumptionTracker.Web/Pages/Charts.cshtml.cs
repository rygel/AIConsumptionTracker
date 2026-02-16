using AIConsumptionTracker.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIConsumptionTracker.Web.Pages;

public class ChartsModel : PageModel
{
    private readonly WebDatabaseService _dbService;

    public ChartsModel(WebDatabaseService dbService)
    {
        _dbService = dbService;
    }

    public List<ChartDataPoint>? ChartData { get; set; }
    public bool IsDatabaseAvailable => _dbService.IsDatabaseAvailable();

    public async Task OnGetAsync(int hours = 24)
    {
        if (IsDatabaseAvailable)
        {
            ChartData = await _dbService.GetChartDataAsync(hours);
        }
    }
}
