namespace Accounting.Application.StockMovements.Commands.Transfer;

public record StockTransferDetailDto(
    bool Success,
    int OutMovementId,
    int InMovementId,
    string Message
);

public record StockTransferListItemDto(
    bool Success,
    int OutMovementId,
    int InMovementId,
    string Message
);
