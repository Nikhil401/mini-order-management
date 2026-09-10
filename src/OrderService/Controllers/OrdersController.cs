using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Clients;
using OrderService.Messaging;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(
    IInventoryClient inventoryClient,
    OrderDbContext dbContext,
    IRabbitMqPublisher rabbitMqPublisher,
    ILogger<OrdersController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Order>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Order create request received. ProductId={ProductId}, Quantity={Quantity}, TraceId={TraceId}",
            request.ProductId,
            request.Quantity,
            HttpContext.TraceIdentifier);

        InventoryItemResponse? inventoryItem;

        try
        {
            logger.LogInformation(
                "Calling InventoryService for ProductId={ProductId}. TraceId={TraceId}",
                request.ProductId,
                HttpContext.TraceIdentifier);

            inventoryItem = await inventoryClient.GetByProductIdAsync(
                request.ProductId,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            logger.LogError(
                "InventoryService request failed for ProductId={ProductId}. TraceId={TraceId}",
                request.ProductId,
                HttpContext.TraceIdentifier);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "InventoryService is currently unavailable." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                "InventoryService timed out for ProductId={ProductId}. TraceId={TraceId}",
                request.ProductId,
                HttpContext.TraceIdentifier);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "InventoryService did not respond in time." });
        }

        if (inventoryItem is null)
        {
            logger.LogWarning(
                "Order rejected because ProductId={ProductId} does not exist. TraceId={TraceId}",
                request.ProductId,
                HttpContext.TraceIdentifier);

            return BadRequest(new
            {
                message = $"Product {request.ProductId} does not exist in inventory."
            });
        }

        if (inventoryItem.Quantity < request.Quantity)
        {
            logger.LogWarning(
                "Order rejected because inventory is insufficient. ProductId={ProductId}, RequestedQuantity={RequestedQuantity}, AvailableQuantity={AvailableQuantity}, TraceId={TraceId}",
                request.ProductId,
                request.Quantity,
                inventoryItem.Quantity,
                HttpContext.TraceIdentifier);

            return Conflict(new
            {
                message = $"Insufficient inventory for product {request.ProductId}.",
                requestedQuantity = request.Quantity,
                availableQuantity = inventoryItem.Quantity
            });
        }

        var order = new Order
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            Status = "Created",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Order created successfully. OrderId={OrderId}, ProductId={ProductId}, Quantity={Quantity}, TraceId={TraceId}",
            order.Id,
            order.ProductId,
            order.Quantity,
            HttpContext.TraceIdentifier);

        await rabbitMqPublisher.PublishOrderCreatedAsync(
            new OrderCreatedEvent(order.Id, order.ProductId, order.Quantity, order.CreatedAtUtc),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet]
    public ActionResult<IEnumerable<Order>> GetAll()
    {
        var orders = dbContext.Orders
            .OrderBy(order => order.Id)
            .AsNoTracking()
            .ToList();

        logger.LogInformation(
            "Order list requested. Count={OrderCount}, TraceId={TraceId}",
            orders.Count,
            HttpContext.TraceIdentifier);

        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> GetById(int id)
    {
        logger.LogInformation(
            "Order lookup started. OrderId={OrderId}, TraceId={TraceId}",
            id,
            HttpContext.TraceIdentifier);

        var order = await dbContext.Orders.AsNoTracking().FirstOrDefaultAsync(order => order.Id == id);

        if (order is null)
        {
            logger.LogWarning(
                "Order lookup failed. OrderId={OrderId}, TraceId={TraceId}",
                id,
                HttpContext.TraceIdentifier);

            return NotFound(new { message = $"Order {id} was not found." });
        }

        logger.LogInformation(
            "Order lookup succeeded. OrderId={OrderId}, TraceId={TraceId}",
            id,
            HttpContext.TraceIdentifier);

        return Ok(order);
    }
}
