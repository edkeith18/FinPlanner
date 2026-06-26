namespace FinPlanner.Data;

public class Expense
{
    public int YearStart { get; set; };
    public int YearEnd { get; set; };
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime LastUpdated { get; set; }
}
