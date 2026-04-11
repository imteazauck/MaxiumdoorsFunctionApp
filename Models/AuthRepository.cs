using MaxiumDoorsFunctionApp;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;


public sealed class AuthRepository : IAuthRepository
{
    private readonly Container _usersContainer;

    public AuthRepository(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseName = configuration["CosmosDb:DatabaseId"]
                           ?? throw new InvalidOperationException("CosmosDb:DatabaseId is missing.");

        var usersContainerName = configuration["CosmosDb:UsersContainerId"] ?? "Users";
        _usersContainer = cosmosClient.GetContainer(databaseName, usersContainerName);
    }

    public async Task<AuthUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
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
}