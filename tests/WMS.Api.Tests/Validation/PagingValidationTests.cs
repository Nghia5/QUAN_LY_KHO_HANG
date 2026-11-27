using System.ComponentModel.DataAnnotations;
using WMS.Api.Dtos.Inventories;
using WMS.Api.Dtos.Users;
using WMS.Api.Validators;

namespace WMS.Api.Tests.Validation;

public sealed class PagingValidationTests
{
    [Theory]
    [InlineData(21474837, false)]
    [InlineData(21474838, true)]
    [InlineData(int.MaxValue, true)]
    public void Offset_must_fit_in_the_supported_range(int page, bool invalid)
    {
        var userQuery = new UserQuery { Page = page, PageSize = 100 };
        var inventoryQuery = new InventoryQuery { Page = page, PageSize = 100 };

        Assert.Equal(invalid, userQuery.Validate(new ValidationContext(userQuery))
            .Any(error => error.MemberNames.Contains(nameof(userQuery.Page))));
        Assert.Equal(invalid, new InventoryQueryValidator().Validate(inventoryQuery)
            .Errors.Any(error => error.PropertyName == nameof(inventoryQuery.Page)));
    }
}
