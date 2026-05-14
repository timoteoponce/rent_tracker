using RentTracker.Web.Helpers;
using RentTracker.Web.Models;
using System.Security.Claims;
using Xunit;

namespace RentTracker.Tests.Unit;

public class AuthorizationHelperTests
{
    private ClaimsPrincipal MakeUser(Guid? userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId?.ToString() ?? Guid.Empty.ToString()),
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }

    [Fact]
    public void GetCurrentUserId_ReturnsGuid_WhenClaimExists()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id, UserRoles.Owner);
        var result = AuthorizationHelper.GetCurrentUserId(user);
        Assert.Equal(id, result);
    }

    [Fact]
    public void GetCurrentUserId_ReturnsNull_WhenNoClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var result = AuthorizationHelper.GetCurrentUserId(user);
        Assert.Null(result);
    }

    [Fact]
    public void CanViewProperty_Admin_CanViewEverything()
    {
        var property = new Property { IsPrivate = true, LastEditedById = Guid.NewGuid() };
        var admin = MakeUser(Guid.NewGuid(), UserRoles.Administrator);
        var result = AuthorizationHelper.CanViewProperty(property, Guid.NewGuid(), true);
        Assert.True(result);
    }

    [Fact]
    public void CanViewProperty_Owner_CanViewPublicProperty()
    {
        var property = new Property { IsPrivate = false, LastEditedById = Guid.NewGuid() };
        var ownerId = Guid.NewGuid();
        var result = AuthorizationHelper.CanViewProperty(property, ownerId, false);
        Assert.True(result);
    }

    [Fact]
    public void CanViewProperty_Owner_CannotViewOthersPrivateProperty()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var property = new Property { IsPrivate = true, LastEditedById = otherId };
        var result = AuthorizationHelper.CanViewProperty(property, ownerId, false);
        Assert.False(result);
    }

    [Fact]
    public void CanViewProperty_Owner_CanViewOwnPrivateProperty()
    {
        var ownerId = Guid.NewGuid();
        var property = new Property { IsPrivate = true, LastEditedById = ownerId };
        var result = AuthorizationHelper.CanViewProperty(property, ownerId, false);
        Assert.True(result);
    }

    [Fact]
    public void CanTogglePrivacy_Admin_AlwaysTrue()
    {
        var property = new Property { LastEditedById = Guid.NewGuid() };
        var result = AuthorizationHelper.CanTogglePrivacy(property, Guid.NewGuid(), true);
        Assert.True(result);
    }

    [Fact]
    public void CanTogglePrivacy_Owner_OnlyIfLastEditor()
    {
        var ownerId = Guid.NewGuid();
        var property = new Property { LastEditedById = ownerId };
        var result = AuthorizationHelper.CanTogglePrivacy(property, ownerId, false);
        Assert.True(result);
    }

    [Fact]
    public void CanTogglePrivacy_Owner_FalseIfNotLastEditor()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var property = new Property { LastEditedById = otherId };
        var result = AuthorizationHelper.CanTogglePrivacy(property, ownerId, false);
        Assert.False(result);
    }

    [Fact]
    public void CanTogglePrivacy_FalseIfLastEditedByIdIsNull()
    {
        var property = new Property { LastEditedById = null };
        var result = AuthorizationHelper.CanTogglePrivacy(property, Guid.NewGuid(), false);
        Assert.False(result);
    }

    [Fact]
    public void CanViewLease_Tenant_OnlyOwnLease()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        // Private property: only the lease tenant should see it
        var property = new Property { IsPrivate = true, LastEditedById = Guid.NewGuid() };
        var lease = new Lease { TenantId = tenantId, Property = property };
        
        // Verify preconditions
        Assert.True(property.IsPrivate);
        Assert.NotEqual(tenantId, otherTenantId);
        Assert.NotEqual(property.LastEditedById, otherTenantId);
        Assert.False(AuthorizationHelper.CanViewProperty(property, otherTenantId, false));
        
        var result = AuthorizationHelper.CanViewLease(lease, otherTenantId, false, true);
        Assert.False(result);
    }

    [Fact]
    public void CanViewPayment_NullLease_ReturnsFalse()
    {
        var payment = new Payment { Lease = null! };
        var result = AuthorizationHelper.CanViewPayment(payment, Guid.NewGuid(), false, false);
        Assert.False(result);
    }
}
