namespace PizzaDeli.DTOs;

/// <summary>
/// DTO chuẩn SePay webhook — hỗ trợ cả định dạng gốc lẫn định dạng tùy chỉnh.
/// </summary>
public class SepayWebhookData
{
    public string?  gateway            { get; set; }
    public string?  transactionDate    { get; set; }
    public string?  accountNumber      { get; set; }
    public string?  subAccount         { get; set; }
    public decimal? transferAmount     { get; set; }
    public decimal? amountIn           { get; set; }
    public decimal? amountOut          { get; set; }
    public decimal? accumulated        { get; set; }
    public string?  code               { get; set; }
    public string?  transactionContent { get; set; }
    public string?  referenceNumber    { get; set; }
    public string?  body               { get; set; }

    // Alias hỗ trợ cả định dạng tùy chỉnh
    public string?  content            { get; set; }
    public decimal? amount             { get; set; }
}
