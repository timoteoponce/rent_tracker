using Microsoft.AspNetCore.Identity;

namespace RentTracker.Models;

public class ApplicationUser : IdentityUser
{
    public required string FullName { get; set; }
    public bool RequiresPasswordChange { get; set; } = true;
    
    // Navigation properties
    public virtual ICollection<PropertyOwner> OwnedProperties { get; set; } = [];
    public virtual ICollection<Rental> RentalsAsTenant { get; set; } = [];
}
