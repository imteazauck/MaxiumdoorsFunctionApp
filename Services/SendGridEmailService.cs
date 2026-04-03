using MaxiumDoorsFunctionApp.Interfaces;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net.Mail;

namespace MaxiumDoorsFunctionApp;

public sealed class SendGridEmailService : IEmailService
{
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailService(IConfiguration configuration)
    {
        _apiKey = GetRequiredSetting(configuration, "SendGrid:ApiKey");
        _fromEmail = GetRequiredSetting(configuration, "SendGrid:FromEmail");
        _fromName = configuration["SendGrid:FromName"]
            ?? configuration["Values:SendGrid:FromName"]
            ?? "Maxium Doors";
    }

    public async Task SendOrderConfirmationAsync(
        string toEmail,
        string customerName,
        string companyName,
        string orderNumber,
        string quoteRef,
        string createdAt,
        CancellationToken cancellationToken = default)
    {
        var client = new SendGridClient(_apiKey);

        var from = new EmailAddress(_fromEmail, _fromName);
        var to = new EmailAddress(toEmail, customerName);

        var subject = $"Order Confirmation - {orderNumber}";

        var plainText = $"""
            Hi {customerName},

            Thank you for your order with Maxium Doors.

            Order number: {orderNumber}
            Quote reference: {quoteRef}
            Created at: {createdAt}

            We have received your order and will be in touch shortly.

            Regards,
            Maxium Doors
            """;

        var html = $"""
            <p>Hi {System.Net.WebUtility.HtmlEncode(customerName)},</p>
            <p>Thank you for your order with <strong>Maxium Doors</strong>.</p>
            <p>
                <strong>Order number:</strong> {System.Net.WebUtility.HtmlEncode(orderNumber)}<br />
                <strong>Quote reference:</strong> {System.Net.WebUtility.HtmlEncode(quoteRef)}<br />
                <strong>Created at:</strong> {System.Net.WebUtility.HtmlEncode(createdAt)}
            </p>
            <p>We have received your order and will be in touch shortly.</p>
            <p>Regards,<br />Maxium Doors</p>
            """;

        var message = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);

        var response = await client.SendEmailAsync(message, cancellationToken);

        if ((int)response.StatusCode >= 400)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to send confirmation email. Status: {(int)response.StatusCode}. Body: {body}");
        }
    }

    private static string GetRequiredSetting(IConfiguration configuration, string key)
    {
        return configuration[key]
            ?? configuration[key.Replace(":", "__", StringComparison.Ordinal)]
            ?? configuration[$"Values:{key}"]
            ?? configuration[$"Values:{key.Replace(":", "__", StringComparison.Ordinal)}"]
            ?? throw new InvalidOperationException($"Configuration value '{key}' is missing.");
    }
}