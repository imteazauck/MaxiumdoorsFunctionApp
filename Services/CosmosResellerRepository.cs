using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using System.Text.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class CosmosResellerRepository
{
    private readonly Container _resellersContainer;
    private readonly Container _pricingContainer;
    private readonly Container _usersContainer;
    private readonly Lazy<IReadOnlyList<PricingTemplateSeedRow>> _defaultTemplate;

    public CosmosResellerRepository(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseId = GetRequiredSetting(configuration, "CosmosDb:DatabaseId");
        var resellersContainerId = GetSetting(configuration, "CosmosDb:ResellersContainerId") ?? "Resellers";
        var pricingContainerId = GetSetting(configuration, "CosmosDb:PricingContainerId") ?? "ResellerPricing";
        var usersContainerId = GetSetting(configuration, "CosmosDb:UsersContainerId") ?? "Users";

        _resellersContainer = cosmosClient.GetContainer(databaseId, resellersContainerId);
        _pricingContainer = cosmosClient.GetContainer(databaseId, pricingContainerId);
        _usersContainer = cosmosClient.GetContainer(databaseId, usersContainerId);
        _defaultTemplate = new Lazy<IReadOnlyList<PricingTemplateSeedRow>>(LoadDefaultTemplate);
    }

    public async Task<IReadOnlyList<ResellerDocument>> ListResellersAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(@"
            SELECT *
            FROM c
            WHERE c.type = 'reseller'
            ORDER BY c.companyName ASC, c.createdAt DESC");

        var iterator = _resellersContainer.GetItemQueryIterator<ResellerDocument>(query);
        var results = new List<ResellerDocument>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response.Resource.Select(SanitizeReseller));
        }

        return results;
    }

    public async Task<ResellerDocument?> GetResellerAsync(string resellerId, CancellationToken cancellationToken = default)
    {
        var document = await GetResellerInternalAsync(resellerId, cancellationToken);
        return document is null ? null : SanitizeReseller(document);
    }

    public async Task<ResellerDocument> CreateResellerAsync(ResellerUpsertRequestDto payload, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow.ToString("O");
        var resellerId = CreateResellerId(payload.CompanyName, payload.Email);

        var document = new ResellerDocument
        {
            Id = resellerId,
            ResellerId = resellerId,
            CompanyName = payload.CompanyName.Trim(),
            FirstName = payload.FirstName.Trim(),
            LastName = payload.LastName.Trim(),
            BusinessAddress = payload.BusinessAddress.Trim(),
            Tel = payload.Tel.Trim(),
            Fax = payload.Fax.Trim(),
            Mobile = payload.Mobile.Trim(),
            Email = payload.Email.Trim(),
            WebAddress = payload.WebAddress.Trim(),
            Notes = payload.Notes.Trim(),
            IsActive = true,
            PricingInitialized = false,
            SourceTemplateId = "default",
            Credentials = new ResellerCredentialSummaryDto
            {
                LoginEnabled = false,
                LoginEmail = payload.Email.Trim(),
                HasPassword = false,
                PasswordLastSetAt = null,
                PasswordHash = null
            },
            CreatedAt = now,
            UpdatedAt = now
        };

        await _resellersContainer.CreateItemAsync(document, new PartitionKey(document.ResellerId), cancellationToken: cancellationToken);

        try
        {
            await CloneDefaultPricingForResellerAsync(document.ResellerId, cancellationToken);

            document.PricingInitialized = true;
            document.UpdatedAt = DateTime.UtcNow.ToString("O");
            await _resellersContainer.ReplaceItemAsync(document, document.Id, new PartitionKey(document.ResellerId), cancellationToken: cancellationToken);
            return SanitizeReseller(document);
        }
        catch
        {
            await SafeDeleteResellerAsync(document.ResellerId, cancellationToken);
            throw;
        }
    }

    public async Task<ResellerDocument?> UpdateResellerAsync(string resellerId, ResellerUpsertRequestDto payload, CancellationToken cancellationToken = default)
    {
        var existing = await GetResellerInternalAsync(resellerId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.CompanyName = payload.CompanyName.Trim();
        existing.FirstName = payload.FirstName.Trim();
        existing.LastName = payload.LastName.Trim();
        existing.BusinessAddress = payload.BusinessAddress.Trim();
        existing.Tel = payload.Tel.Trim();
        existing.Fax = payload.Fax.Trim();
        existing.Mobile = payload.Mobile.Trim();
        existing.Email = payload.Email.Trim();
        existing.WebAddress = payload.WebAddress.Trim();
        existing.Notes = payload.Notes.Trim();
        existing.UpdatedAt = DateTime.UtcNow.ToString("O");

        existing.Credentials ??= new ResellerCredentialSummaryDto();
        if (string.IsNullOrWhiteSpace(existing.Credentials.LoginEmail))
        {
            existing.Credentials.LoginEmail = existing.Email;
        }

        var response = await _resellersContainer.ReplaceItemAsync(existing, existing.Id, new PartitionKey(existing.ResellerId), cancellationToken: cancellationToken);
        await SyncAuthUserAsync(response.Resource, cancellationToken);
        return SanitizeReseller(response.Resource);
    }

    public async Task<bool> DeleteResellerAsync(string resellerId, CancellationToken cancellationToken = default)
    {
        var existing = await GetResellerInternalAsync(resellerId, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        await DeleteAllPricingForResellerAsync(resellerId, cancellationToken);
        await _resellersContainer.DeleteItemAsync<ResellerDocument>(resellerId, new PartitionKey(resellerId), cancellationToken: cancellationToken);
        await DeleteAuthUserIfExistsAsync(resellerId, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ResellerPricingDocument>> GetPricingAsync(string resellerId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(@"
    SELECT *
    FROM c
    WHERE c.resellerId = @resellerId
      AND (c.type = 'matrixRow' OR c.type = 'optionRow')
    ORDER BY c.type ASC,
             c[""group""] ASC,
             c.label ASC,
             c.doorCategory ASC,
             c.configuration ASC,
             c.heightMin ASC,
             c.widthMin ASC")
    .WithParameter("@resellerId", resellerId);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(resellerId) };
        var iterator = _pricingContainer.GetItemQueryIterator<ResellerPricingDocument>(query, requestOptions: options);
        var results = new List<ResellerPricingDocument>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response.Resource);
        }

        return results;
    }

    public async Task<ResellerPricingDocument?> UpdatePricingItemAsync(string resellerId, string itemId, decimal price, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _pricingContainer.ReadItemAsync<ResellerPricingDocument>(itemId, new PartitionKey(resellerId), cancellationToken: cancellationToken);
            var document = response.Resource;
            document.Price = price;
            document.UpdatedAt = DateTime.UtcNow.ToString("O");

            var updated = await _pricingContainer.ReplaceItemAsync(document, document.Id, new PartitionKey(resellerId), cancellationToken: cancellationToken);
            return updated.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ResellerCredentialSummaryDto?> GetCredentialStatusAsync(string resellerId, CancellationToken cancellationToken = default)
    {
        var reseller = await GetResellerAsync(resellerId, cancellationToken);
        return reseller?.Credentials ?? null;
    }

    public async Task<ResellerCredentialSummaryDto?> UpsertCredentialsAsync(string resellerId, ResellerCredentialUpdateRequestDto payload, CancellationToken cancellationToken = default)
    {
        var reseller = await GetResellerInternalAsync(resellerId, cancellationToken);
        if (reseller is null)
        {
            return null;
        }

        reseller.Credentials ??= new ResellerCredentialSummaryDto();
        reseller.Credentials.LoginEnabled = payload.LoginEnabled;
        reseller.Credentials.LoginEmail = payload.LoginEmail.Trim();

        if (!string.IsNullOrWhiteSpace(payload.Password))
        {
            reseller.Credentials.HasPassword = true;
            reseller.Credentials.PasswordLastSetAt = DateTime.UtcNow.ToString("O");
            reseller.Credentials.PasswordHash = PasswordHasher.HashPassword(payload.Password);
        }

        reseller.UpdatedAt = DateTime.UtcNow.ToString("O");
        var response = await _resellersContainer.ReplaceItemAsync(reseller, reseller.Id, new PartitionKey(reseller.ResellerId), cancellationToken: cancellationToken);
        await SyncAuthUserAsync(response.Resource, cancellationToken);
        return SanitizeReseller(response.Resource).Credentials;
    }

    private async Task CloneDefaultPricingForResellerAsync(string resellerId, CancellationToken cancellationToken)
    {
        var template = _defaultTemplate.Value;
        const int batchLimit = 100;

        foreach (var chunk in template.Chunk(batchLimit))
        {
            var batch = _pricingContainer.CreateTransactionalBatch(new PartitionKey(resellerId));
            foreach (var item in chunk)
            {
                var document = new ResellerPricingDocument
                {
                    Id = BuildPricingItemId(resellerId, item),
                    ResellerId = resellerId,
                    Type = item.Type,
                    SourceTemplateId = item.SourceTemplateId,
                    DoorCategory = item.DoorCategory,
                    Group = item.Group,
                    Label = item.Label,
                    Configuration = item.Configuration,
                    HeightMin = item.HeightMin,
                    HeightMax = item.HeightMax,
                    WidthMin = item.WidthMin,
                    WidthMax = item.WidthMax,
                    Price = item.Price,
                    Currency = string.IsNullOrWhiteSpace(item.Currency) ? "GBP" : item.Currency,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                };

                batch.CreateItem(document);
            }

            var response = await batch.ExecuteAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to clone default pricing template. StatusCode={(int)response.StatusCode}");
            }
        }
    }

    private async Task DeleteAllPricingForResellerAsync(string resellerId, CancellationToken cancellationToken)
    {
        var items = await GetPricingAsync(resellerId, cancellationToken);
        foreach (var item in items)
        {
            await _pricingContainer.DeleteItemAsync<ResellerPricingDocument>(item.Id, new PartitionKey(resellerId), cancellationToken: cancellationToken);
        }
    }

    private async Task SafeDeleteResellerAsync(string resellerId, CancellationToken cancellationToken)
    {
        try
        {
            await _resellersContainer.DeleteItemAsync<ResellerDocument>(resellerId, new PartitionKey(resellerId), cancellationToken: cancellationToken);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private async Task<ResellerDocument?> GetResellerInternalAsync(string resellerId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _resellersContainer.ReadItemAsync<ResellerDocument>(resellerId, new PartitionKey(resellerId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task SyncAuthUserAsync(ResellerDocument reseller, CancellationToken cancellationToken)
    {
        reseller.Credentials ??= new ResellerCredentialSummaryDto();

        var loginEmail = reseller.Credentials.LoginEmail?.Trim();
        var displayName = BuildDisplayName(reseller);
        var shouldBeActive = reseller.IsActive
            && reseller.Credentials.LoginEnabled
            && !string.IsNullOrWhiteSpace(loginEmail)
            && !string.IsNullOrWhiteSpace(reseller.Credentials.PasswordHash);

        var existing = await GetAuthUserByIdAsync(reseller.ResellerId, cancellationToken)
            ?? await GetAuthUserByResellerIdAsync(reseller.ResellerId, cancellationToken)
            ?? await GetAuthUserByEmailAsync(loginEmail, cancellationToken);

        if (existing is null)
        {
            if (!shouldBeActive)
            {
                return;
            }

            var created = new AuthUser
            {
                Id = reseller.ResellerId,
                Email = loginEmail!,
                PasswordHash = reseller.Credentials.PasswordHash!,
                Role = UserRoles.Reseller,
                IsActive = true,
                ResellerId = reseller.ResellerId,
                DisplayName = displayName,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _usersContainer.CreateItemAsync(created, new PartitionKey(created.Id), cancellationToken: cancellationToken);
            return;
        }

        existing.Email = !string.IsNullOrWhiteSpace(loginEmail) ? loginEmail! : existing.Email;
        if (!string.IsNullOrWhiteSpace(reseller.Credentials.PasswordHash))
        {
            existing.PasswordHash = reseller.Credentials.PasswordHash!;
        }

        existing.Role = UserRoles.Reseller;
        existing.IsActive = shouldBeActive;
        existing.ResellerId = reseller.ResellerId;
        existing.DisplayName = displayName;

        await _usersContainer.UpsertItemAsync(existing, new PartitionKey(existing.Id), cancellationToken: cancellationToken);
    }

    private async Task<AuthUser?> GetAuthUserByIdAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _usersContainer.ReadItemAsync<AuthUser>(userId, new PartitionKey(userId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<AuthUser?> GetAuthUserByResellerIdAsync(string resellerId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.resellerId = @resellerId")
            .WithParameter("@resellerId", resellerId);

        using var iterator = _usersContainer.GetItemQueryIterator<AuthUser>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var user = response.Resource.FirstOrDefault();
            if (user is not null)
            {
                return user;
            }
        }

        return null;
    }

    private async Task<AuthUser?> GetAuthUserByEmailAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var query = new QueryDefinition("SELECT * FROM c WHERE LOWER(c.email) = @email")
            .WithParameter("@email", email.Trim().ToLowerInvariant());

        using var iterator = _usersContainer.GetItemQueryIterator<AuthUser>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var user = response.Resource.FirstOrDefault();
            if (user is not null)
            {
                return user;
            }
        }

        return null;
    }

    private async Task DeleteAuthUserIfExistsAsync(string resellerId, CancellationToken cancellationToken)
    {
        var user = await GetAuthUserByIdAsync(resellerId, cancellationToken)
            ?? await GetAuthUserByResellerIdAsync(resellerId, cancellationToken);

        if (user is null)
        {
            return;
        }

        try
        {
            await _usersContainer.DeleteItemAsync<AuthUser>(user.Id, new PartitionKey(user.Id), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already gone.
        }
    }

    private static string BuildDisplayName(ResellerDocument reseller)
    {
        var contactName = string.Join(" ", new[] { reseller.FirstName?.Trim(), reseller.LastName?.Trim() }
            .Where(static value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(contactName) && !string.IsNullOrWhiteSpace(reseller.CompanyName))
        {
            return $"{contactName} ({reseller.CompanyName.Trim()})";
        }

        return !string.IsNullOrWhiteSpace(contactName)
            ? contactName
            : reseller.CompanyName.Trim();
    }

    private static IReadOnlyList<PricingTemplateSeedRow> LoadDefaultTemplate()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "defaultPricingTemplate.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Default pricing template file not found at '{path}'.");
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<PricingTemplateSeedRow>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Default pricing template could not be loaded.");
    }

    private static string BuildPricingItemId(string resellerId, PricingTemplateSeedRow item)
    {
        var raw = string.Join("_", new[]
        {
            resellerId,
            item.Type,
            item.DoorCategory,
            item.Group,
            item.Label,
            item.Configuration,
            item.HeightMin?.ToString(),
            item.HeightMax?.ToString(),
            item.WidthMin?.ToString(),
            item.WidthMax?.ToString()
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return Slugify(raw);
    }

    private static string CreateResellerId(string companyName, string email)
    {
        var seed = !string.IsNullOrWhiteSpace(companyName) ? companyName : email;
        var baseSlug = Slugify(seed);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "reseller";
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(baseSlug.Length + 33, 80)];
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousDash = false;

        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static ResellerDocument SanitizeReseller(ResellerDocument document)
    {
        document.Credentials ??= new ResellerCredentialSummaryDto();
        document.Credentials.LoginEmail ??= string.Empty;
        document.Credentials.PasswordHash = null;
        return document;
    }

    private static string? GetSetting(IConfiguration configuration, string key)
    {
        return configuration[key]
            ?? configuration[key.Replace(":", "__", StringComparison.Ordinal)]
            ?? configuration[$"Values:{key}"]
            ?? configuration[$"Values:{key.Replace(":", "__", StringComparison.Ordinal)}"];
    }

    private static string GetRequiredSetting(IConfiguration configuration, string key) =>
        GetSetting(configuration, key) ?? throw new InvalidOperationException($"Configuration value '{key}' is missing.");
}
