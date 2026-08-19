namespace EbedrendeloApp.Domain.Entities;

public sealed class User
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required string UserName { get; set; }
    public string? KeresztNev { get; set; }
    public string? VezetekNev { get; set; }
    public string? Rf { get; set; }
    public string? SzervKod { get; set; }
    public required int RoleId { get; set; }
    public Role? Role { get; set; }
}
