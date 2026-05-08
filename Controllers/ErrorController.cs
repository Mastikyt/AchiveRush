using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class ErrorController : Controller
    {
        [Route("error/{statusCode:int}")]
        public IActionResult StatusCodePage(int statusCode)
        {
            if (statusCode == 404 || statusCode == 403)
                return View("~/Views/Shared/NotFound.cshtml", "Страница не найдена или недоступна.");

            return View("~/Views/Shared/NotFound.cshtml", $"Ошибка {statusCode}.");
        }
    }
}
