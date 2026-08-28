namespace MobileGadgets.Infrastructure.Auth;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "MobileGadgets";
    public string Audience { get; set; } = "MobileGadgetsApp";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}
