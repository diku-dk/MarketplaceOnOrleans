using System.Net;
using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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