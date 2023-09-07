using Microsoft.AspNetCore.Mvc;

namespace Order.Controllers;

[ApiController]
public class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> logger;

    public OrderController(ILogger<OrderController> logger)
    {
        this.logger = logger;
    }



}