using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SpinEngine.Domain.Entities;

namespace SpinEngine.Api.Middlewares
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ErrorHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (InsufficientFundsException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";
                var errorObj = new { error = "INSUFFICIENT_FUNDS", message = ex.Message };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorObj));
            }
            catch (InvalidOperationException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                context.Response.ContentType = "application/json";
                var errorObj = new { error = "CONFLICT", message = ex.Message };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorObj));
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";
                var errorObj = new { error = "INTERNAL_SERVER_ERROR", message = ex.Message };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorObj));
            }
        }
    }
}
