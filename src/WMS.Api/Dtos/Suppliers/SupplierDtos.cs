using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Dtos.Suppliers;

public class CreateSupplierRequestDto
{
    [Required(ErrorMessage = "Mã nhà cung cấp là bắt buộc.")]
    [RegularExpression(@"^[A-Za-z0-9._-]{2,30}$", ErrorMessage = "Mã nhà cung cấp chỉ gồm chữ, số, . _ - (2-30 ký tự).")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên nhà cung cấp là bắt buộc.")]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [RegularExpression(@"^\+?[0-9]{8,15}$", ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }
}

public class UpdateSupplierRequestDto : CreateSupplierRequestDto
{
    public bool IsActive { get; set; } = true;
}

public class SupplierResponseDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
