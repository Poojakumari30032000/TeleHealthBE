using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Vitality.Models.Repos.Services.Audit;

namespace Vitality.Filters
{

    public class AuditActionFilter : IAsyncActionFilter
    {
        private readonly IAuditService _auditService;

        public AuditActionFilter(IAuditService auditService)
        {
            _auditService = auditService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var request = httpContext.Request;
            var user = httpContext.User;

            long? userId = null;
            long? patientId = null;
            long? organizationId = null;
            long? facilityId = null;

            var userIdClaim = user.FindFirst("UserId");
            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var patientIdClaim = user.FindFirst("PatientId");
            if (patientIdClaim != null && long.TryParse(patientIdClaim.Value, out var parsedPatientId))
            {
                patientId = parsedPatientId;
            }

            var orgIdClaim = user.FindFirst("OrganizationId");
            if (orgIdClaim != null && long.TryParse(orgIdClaim.Value, out var parsedOrgId))
            {
                organizationId = parsedOrgId;
            }

            var facilityIdClaim = user.FindFirst("FacilityId");
            if (facilityIdClaim != null && long.TryParse(facilityIdClaim.Value, out var parsedFacilityId))
            {
                facilityId = parsedFacilityId;
            }

            object? requestData = null;
            if (request.Method == "POST" || request.Method == "PUT" || request.Method == "PATCH")
            {
                if (context.ActionArguments != null && context.ActionArguments.Any())
                {

                    var firstArg = context.ActionArguments.Values.FirstOrDefault();
                    if (firstArg != null)
                    {

                        requestData = SanitizeRequestData(firstArg);
                    }
                }
            }

            var executedContext = await next();

            string status = "Success";
            string? errorMessage = null;

            if (executedContext.Exception != null)
            {
                status = "Error";
                errorMessage = executedContext.Exception.Message;
            }
            else if (executedContext.Result is ObjectResult objectResult)
            {

                if (objectResult.StatusCode >= 400)
                {
                    status = "Failed";
                    errorMessage = objectResult.Value?.ToString();
                }
            }

            var entry = new AuditLogEntry
            {
                Action = "API_Request",
                RequestMethod = request.Method,
                RequestPath = request.Path.Value ?? "",
                UserId = userId,
                PatientId = patientId,
                OrganizationId = organizationId,
                FacilityId = facilityId,
                AdditionalData = requestData,
                Status = status,
                ErrorMessage = errorMessage,
                Description = $"{request.Method} {request.Path.Value}"
            };

            await _auditService.LogAsync(entry);
        }

        private object? SanitizeRequestData(object data)
        {

            try
            {
                var type = data.GetType();
                var properties = type.GetProperties();

                var sanitized = new Dictionary<string, object?>();

                foreach (var prop in properties)
                {
                    var propName = prop.Name.ToLower();

                    if (propName.Contains("password") ||
                        propName.Contains("token") ||
                        propName.Contains("secret") ||
                        propName.Contains("ssn") ||
                        propName.Contains("creditcard") ||
                        propName.Contains("cvv"))
                    {
                        sanitized[prop.Name] = "***REDACTED***";
                    }
                    else
                    {
                        var value = prop.GetValue(data);
                        sanitized[prop.Name] = value;
                    }
                }

                return sanitized;
            }
            catch
            {

                return null;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuditAttribute : Attribute
    {
        public string? Description { get; set; }
        public bool LogRequestData { get; set; } = true;
    }
}
