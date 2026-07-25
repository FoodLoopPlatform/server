using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Auth;

/// <summary>
/// Backs a single POST /auth/register call that serves both the plain customer signup and
/// the first step of the business wizard (business_signup_step_1 / create_account_account_type_selection).
/// When Role is Merchant or Charity, BusinessName is required and a draft Store is
/// created alongside the user; location + documents (step 2) and status (step 3) are handled
/// afterwards by /stores/me/* endpoints. Admin accounts cannot be self-registered — see
/// UsersController for admin-managed user creation.
/// </summary>
public class RegisterRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty; // full name (customer) or owner name (business)

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Customer / Merchant / Charity, matching the account type dropdown at signup. Defaults to Customer.</summary>
    public string Role { get; set; } = AppRole.Customer;

    /// <summary>Required when Role is Merchant or Charity (store_name on business_signup_step_1).</summary>
    [MaxLength(150)]
    public string? BusinessName { get; set; }

    /// <summary>Optional business_type dropdown from business_signup_step_1 (supermarket, restaurant, etc.).</summary>
    public BusinessCategory? BusinessCategory { get; set; }
}
