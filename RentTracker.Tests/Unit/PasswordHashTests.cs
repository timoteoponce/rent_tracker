using Xunit;
using static RentTracker.Web.Program;

namespace RentTracker.Tests.Unit;

public class PasswordHashTests
{
    [Fact]
    public void HashPassword_SameInput_SameOutput()
    {
        var hash1 = HashPassword("testpassword");
        var hash2 = HashPassword("testpassword");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashPassword_DifferentInput_DifferentOutput()
    {
        var hash1 = HashPassword("password1");
        var hash2 = HashPassword("password2");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = HashPassword("correcthorsebatterystaple");
        Assert.True(VerifyPassword("correcthorsebatterystaple", hash));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = HashPassword("correcthorsebatterystaple");
        Assert.False(VerifyPassword("wrongpassword", hash));
    }

    [Fact]
    public void VerifyPassword_CaseSensitive()
    {
        var hash = HashPassword("Password123");
        Assert.False(VerifyPassword("password123", hash));
    }

    [Fact]
    public void HashPassword_EmptyString_ReturnsHash()
    {
        var hash = HashPassword("");
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.True(VerifyPassword("", hash));
    }
}
