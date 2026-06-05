using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace WebApplication1.Infrastructure
{
    public static class InputSizeLimits
    {
        public const long MaxRequestBodyBytes = 6 * 1024 * 1024;
        public const int MaxQueryStringLength = 4096;
        public const int MaxFieldNameLength = 128;
        public const int MaxFieldValueLength = 4096;
        public const int MaxTotalFormValueLength = 20000;
        public const int MaxFormValueCount = 80;
        public const int MaxSearchLength = 120;
        public const int MaxSteamIdLength = 64;
        public const int MaxAdminPasswordLength = 128;
    }

    public class InputSizeLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly FormOptions _formOptions;

        public InputSizeLimitMiddleware(
            RequestDelegate next,
            IOptions<FormOptions> formOptions)
        {
            _next = next;
            _formOptions = formOptions.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.ContentLength > InputSizeLimits.MaxRequestBodyBytes)
            {
                await RejectAsync(context, StatusCodes.Status413PayloadTooLarge, "Слишком большой запрос.");
                return;
            }

            if (context.Request.QueryString.HasValue &&
                context.Request.QueryString.Value!.Length > InputSizeLimits.MaxQueryStringLength)
            {
                await RejectAsync(context, StatusCodes.Status414UriTooLong, "Слишком длинная строка запроса.");
                return;
            }

            foreach (var pair in context.Request.Query)
            {
                if (pair.Key.Length > InputSizeLimits.MaxFieldNameLength ||
                    pair.Value.Any(value => value?.Length > InputSizeLimits.MaxFieldValueLength))
                {
                    await RejectAsync(context, StatusCodes.Status400BadRequest, "Слишком длинное значение параметра.");
                    return;
                }
            }

            if (context.Request.HasFormContentType)
            {
                var formFeature = context.Features.Get<IFormFeature>();
                if (formFeature == null)
                    context.Features.Set<IFormFeature>(new FormFeature(context.Request, _formOptions));

                IFormCollection form;
                try
                {
                    form = await context.Request.ReadFormAsync();
                }
                catch (InvalidDataException)
                {
                    await RejectAsync(context, StatusCodes.Status413PayloadTooLarge, "Слишком большая форма.");
                    return;
                }

                if (form.Count > InputSizeLimits.MaxFormValueCount)
                {
                    await RejectAsync(context, StatusCodes.Status400BadRequest, "Слишком много полей формы.");
                    return;
                }

                var totalValueLength = 0;
                foreach (var pair in form)
                {
                    if (pair.Key.Length > InputSizeLimits.MaxFieldNameLength)
                    {
                        await RejectAsync(context, StatusCodes.Status400BadRequest, "Слишком длинное имя поля.");
                        return;
                    }

                    foreach (var value in pair.Value)
                    {
                        var length = value?.Length ?? 0;
                        if (length > InputSizeLimits.MaxFieldValueLength)
                        {
                            await RejectAsync(context, StatusCodes.Status400BadRequest, "Слишком длинное значение поля.");
                            return;
                        }

                        totalValueLength += length;
                        if (totalValueLength > InputSizeLimits.MaxTotalFormValueLength)
                        {
                            await RejectAsync(context, StatusCodes.Status400BadRequest, "Слишком большой объем текста в форме.");
                            return;
                        }
                    }
                }
            }

            await _next(context);
        }

        private static async Task RejectAsync(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(message);
        }
    }
}
