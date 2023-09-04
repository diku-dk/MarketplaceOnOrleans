namespace Common.Driver;

public record TransactionMark(int tid, TransactionType type, int actorId, MarkStatus status, string source);