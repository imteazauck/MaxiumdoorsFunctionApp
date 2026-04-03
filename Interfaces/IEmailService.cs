namespace MaxiumDoorsFunctionApp.Interfaces
{

    public interface IEmailService
    {
        Task SendOrderConfirmationAsync(
            string toEmail,
            string customerName,
            string? companyName,
            string orderNumber,
            string quoteRef,
            string createdAt,
            CancellationToken cancellationToken = default);
    }
}
