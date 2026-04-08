using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RncPlatform.Api.Filters;

public class LocalOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var remoteIp = context.HttpContext.Connection.RemoteIpAddress;

        // Si es nulo o no es loopback, bloqueamos
        if (remoteIp == null || !IPAddress.IsLoopback(remoteIp))
        {
            context.Result = new ForbidResult();
            return;
        }

        base.OnActionExecuting(context);
    }
}
