using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Core.Interfaces;

namespace SyncServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpPost("begin")]
    public async Task<ActionResult<TransactionResponse>> BeginTransaction(CancellationToken ct)
    {
        var clientId = User.FindFirst("client_id")?.Value ?? "unknown";
        var transactionId = await _transactionService.BeginTransactionAsync(clientId, ct);

        return Ok(new TransactionResponse
        {
            TransactionId = transactionId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });
    }

    [HttpPost("{transactionId}/commit")]
    public async Task<IActionResult> CommitTransaction(string transactionId, CancellationToken ct)
    {
        await _transactionService.CommitTransactionAsync(transactionId, ct);
        return Ok();
    }

    [HttpPost("{transactionId}/rollback")]
    public async Task<IActionResult> RollbackTransaction(string transactionId, CancellationToken ct)
    {
        await _transactionService.RollbackTransactionAsync(transactionId, ct);
        return Ok();
    }
}

public class TransactionResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}