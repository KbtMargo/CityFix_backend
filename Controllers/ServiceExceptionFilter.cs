using CityFix_backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CityFix_backend.Controllers
{
    public class ServiceExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            context.Result = context.Exception switch
            {
                NotFoundException => new NotFoundResult(),
                ForbiddenException when context.HttpContext.User.Identity?.IsAuthenticated == true => new ForbidResult(),
                ForbiddenException => new ChallengeResult(),
                _ => context.Result
            };
            context.ExceptionHandled = context.Result != null;
        }
    }
}
