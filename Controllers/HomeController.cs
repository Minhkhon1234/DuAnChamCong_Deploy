using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DUANCHAMCONG.Models;

namespace DUANCHAMCONG.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Dashboard()
    {
        return View();
    }

    public IActionResult AdminDashboard()
    {
        return View();
    }

    public IActionResult UserManagement()
    {
        return View();
    }

    public IActionResult MonthlyReport()
    {
        return View();
    }

    public IActionResult AttendanceHistory()
    {
        return View();
    }

    public IActionResult LeaveHistory()
    {
        return View();
    }

    public IActionResult Settings()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
