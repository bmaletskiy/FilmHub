
using Microsoft.AspNetCore.Mvc;

namespace FilmHub.Controllers;

public class GameController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
