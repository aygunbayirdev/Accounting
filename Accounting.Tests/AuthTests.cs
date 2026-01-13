using Accounting.Domain.Entities;
using Accounting.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace Accounting.Tests;

public class AuthTests
{
    private readonly JwtTokenGenerator _jwtGenerator;
    private readonly JwtSettings _jwtSettings;

    public AuthTests()
    {
        _jwtSettings = new JwtSettings
        {
            Secret = "SuperSecretKeyForTestingPurpose123456789", // Must be long enough for HMACSHA256
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationSeconds = 3600,
            RefreshTokenExpirationSeconds = 7200
        };

        var options = Options.Create(_jwtSettings);
        _jwtGenerator = new JwtTokenGenerator(options);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidToken()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            BranchId = 1,
            Branch = new Branch { Id = 1, Name = "Main", IsHeadquarters = true }
        };
        var permissions = new List<string> { "CanView", "CanEdit" };

        // Act
        var token = _jwtGenerator.GenerateAccessToken(user, permissions);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Equal(_jwtSettings.Issuer, jwtToken.Issuer);
        Assert.Equal(_jwtSettings.Audience, jwtToken.Audiences.First());
        Assert.Contains(jwtToken.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "1");
        Assert.Contains(jwtToken.Claims, c => c.Type == "permission" && c.Value == "CanView");
        Assert.Contains(jwtToken.Claims, c => c.Type == "isHeadquarters" && c.Value == "true");
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnTokenAndExpiry()
    {
        // Act
        var (token, expiry) = _jwtGenerator.GenerateRefreshToken();

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.True(expiry > DateTime.UtcNow);
    }
    
    [Fact]
    public void PasswordHasher_ShouldHashAndVerify()
    {
        var hasher = new PasswordHasher();
        var password = "SecurePassword123!";
        
        var hash = hasher.HashPassword(password);
        Assert.NotEqual(password, hash);
        
        var isValid = hasher.VerifyPassword(hash, password);
        Assert.True(isValid);
        
        var isInvalid = hasher.VerifyPassword(hash, "WrongPassword");
        Assert.False(isInvalid);
    }
}
