namespace DistributedSis.application.DTOs
{
    public record CreateUserRequest(
        string Name,
        string LastName,
        string Email,
        string Password,
        string Document,
        string Address,
        string Phone,
        string Image
        );
    public record LoginRequest(
        string Email,
        string Password
        );

    public record LoginResponse(
        string Token,
        string Message
        );
    public record UpdateUserRequest(
      string Name,
      string LastName,
      string Email,
      string Password,
      string Document,
      string Address,
      string Phone,
      string Image
      );
    public record uploadImageRequest(
        string Image,
        string FileType
        );

}
