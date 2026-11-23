using Microsoft.Extensions.Options;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Batches;
using WMS.Api.Services.Documents;

namespace WMS.Api.Tests.Infrastructure;

public static class TestServices
{
    public static BatchService Batches(WmsDbContext context, ICurrentUser user, TimeProvider? time = null, int nearExpiryDays = 30)
    {
        time ??= TimeProvider.System;
        return new BatchService(
            context,
            new BatchRepository(context),
            new AuditLogger(context, user, time),
            user,
            time,
            Options.Create(new WarehouseOptions { NearExpiryDays = nearExpiryDays }));
    }
}
