using FoodLoop.API.Common;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Application.Features.Orders.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.API.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public OrdersController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// POST /orders — place a checkout order.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest(ApiResponse.Fail("Cart items are required."));
        }

        var items = request.Items.Select(i => new CheckoutItemRequest(i.ProductId, i.Quantity)).ToList();
        var command = new CreateOrderCommand(UserId, items, HttpContext.Connection.RemoteIpAddress?.ToString());
        
        var result = await _mediator.Send(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(ApiResponse.Fail(result.Message ?? "Checkout failed", result.Errors));
        }

        return Ok(ApiResponse<OrderDto>.Ok(result.Data!));
    }

    /// <summary>
    /// GET /orders — get customer's order history.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyOrders(CancellationToken cancellationToken)
    {
        var query = new GetCustomerOrdersQuery(UserId);
        var orders = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrderDto>>.Ok(orders));
    }

    /// <summary>
    /// GET /orders/{id} — get detailed order.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderDetail(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetOrderDetailQuery(id, UserId);
        var order = await _mediator.Send(query, cancellationToken);
        return Ok(ApiResponse<OrderDto>.Ok(order));
    }

    /// <summary>
    /// GET /orders/{id}/tracking — customer-facing real-time order status and progress steps.
    /// </summary>
    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> GetOrderTracking(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOrderTrackingQuery(id, UserId), cancellationToken);
        return Ok(ApiResponse<OrderTrackingDto>.Ok(result));
    }
}

public class CheckoutRequest
{
    public List<CheckoutItemRequestDto> Items { get; set; } = new();
}

public class CheckoutItemRequestDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
