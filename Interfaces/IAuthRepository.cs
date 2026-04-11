namespace MaxiumDoorsFunctionApp;

public interface IAuthRepository
{
    Task<AuthUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
}