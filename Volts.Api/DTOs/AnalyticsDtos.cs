namespace Volts.Api.DTOs;

public class DashboardSummaryDto
{
    public long TotalUsers { get; set; }
    public long InternalUsers { get; set; }
    public long PortalAccounts { get; set; }
    public long TotalCustomers { get; set; }
    public long TotalInstitutions { get; set; }

    public long TotalQuotes { get; set; }
    public long PendingQuotes { get; set; }
    public long ApprovedQuotes { get; set; }
    public long ConvertedQuotes { get; set; }

    public long TotalOrders { get; set; }
    public long PendingOrders { get; set; }
    public long AwaitingProductionOrders { get; set; }
    public long ReadyForSaleOrders { get; set; }

    public long TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal CurrentMonthRevenue { get; set; }

    public long TotalLicenses { get; set; }
    public long AvailableLicenses { get; set; }
    public long ActiveLicenses { get; set; }
    public long ExpiredLicenses { get; set; }
    public long RevokedLicenses { get; set; }

    public long TotalProducts { get; set; }
    public long LowFinishedStockProducts { get; set; }
    public long TotalRawMaterials { get; set; }
    public long LowRawMaterialStock { get; set; }

    public long TotalProductionOrders { get; set; }
    public long ActiveProductionOrders { get; set; }

    public long TotalSupportTickets { get; set; }
    public long OpenSupportTickets { get; set; }
    public long PendingComments { get; set; }
    public long ApprovedComments { get; set; }

    public List<AnalyticsPointDto> MonthlyRevenue { get; set; } = new();
    public List<AnalyticsPointDto> MonthlySales { get; set; } = new();
    public List<AnalyticsCategoryDto> TopProducts { get; set; } = new();
}

public class AnalyticsOverviewDto
{
    public CommercialFunnelDto Funnel { get; set; } = new();

    public List<AnalyticsPointDto> MonthlyRevenue { get; set; } = new();
    public List<AnalyticsPointDto> MonthlySales { get; set; } = new();
    public List<AnalyticsPointDto> MonthlyPurchases { get; set; } = new();

    public List<AnalyticsCategoryDto> QuoteStatuses { get; set; } = new();
    public List<AnalyticsCategoryDto> OrderStatuses { get; set; } = new();
    public List<AnalyticsCategoryDto> LicenseStatuses { get; set; } = new();
    public List<AnalyticsCategoryDto> ProductionStatuses { get; set; } = new();
    public List<AnalyticsCategoryDto> WasteClassifications { get; set; } = new();

    public List<AnalyticsCategoryDto> TopProducts { get; set; } = new();
    public List<AnalyticsCategoryDto> TopCustomers { get; set; } = new();

    public decimal TotalRevenue { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal GrossCommercialMargin { get; set; }
    public decimal AverageTicket { get; set; }
}

public class CommercialFunnelDto
{
    public long Quotes { get; set; }
    public long ApprovedQuotes { get; set; }
    public long ConvertedQuotes { get; set; }
    public long Orders { get; set; }
    public long SoldOrders { get; set; }
    public long Sales { get; set; }

    public decimal ApprovalRate { get; set; }
    public decimal QuoteToOrderRate { get; set; }
    public decimal OrderToSaleRate { get; set; }
}

public class AnalyticsPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
}

public class AnalyticsCategoryDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
}
