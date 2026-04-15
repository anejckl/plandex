using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public interface ITimeTrackingService
{
    Task<TimeEntryDto?> StartAsync(int cardId, int userId);
    Task<TimeEntryDto?> StopAsync(int cardId, int userId);
    Task<IReadOnlyList<TimeEntryDto>?> ListForCardAsync(int cardId, int userId);
    Task<bool> DeleteAsync(int entryId, int userId);
    Task<ActiveTimerDto?> GetActiveAsync(int userId);
}

public record ActiveTimerDto(int EntryId, int CardId, DateTime StartedAt);

public class TimeTrackingService : ITimeTrackingService
{
    private readonly PlandexDbContext _db;
    public TimeTrackingService(PlandexDbContext db) => _db = db;

    public async Task<TimeEntryDto?> StartAsync(int cardId, int userId)
    {
        var card = await _db.Cards
            .Include(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null || card.List.Board.OwnerId != userId) return null;

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Serialize concurrent StartAsync calls for the same user — holding a
        // transaction-scoped Postgres advisory lock ensures the "at most one open
        // entry per user" invariant even under parallel requests.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({userId})");

        var now = DateTime.UtcNow;

        var openEntries = await _db.TimeEntries
            .Where(t => t.UserId == userId && t.EndedAt == null)
            .ToListAsync();

        foreach (var open in openEntries)
        {
            open.EndedAt = now;
            open.DurationSeconds = (int)Math.Max(0, (now - open.StartedAt).TotalSeconds);
        }

        var entry = new TimeEntry
        {
            CardId = cardId,
            UserId = userId,
            StartedAt = now,
            EndedAt = null
        };
        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Map(entry);
    }

    public async Task<TimeEntryDto?> StopAsync(int cardId, int userId)
    {
        var card = await _db.Cards
            .Include(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null || card.List.Board.OwnerId != userId) return null;

        var entry = await _db.TimeEntries
            .FirstOrDefaultAsync(t => t.CardId == cardId && t.UserId == userId && t.EndedAt == null);
        if (entry is null) return null;

        var now = DateTime.UtcNow;
        entry.EndedAt = now;
        entry.DurationSeconds = (int)Math.Max(0, (now - entry.StartedAt).TotalSeconds);
        await _db.SaveChangesAsync();

        return Map(entry);
    }

    public async Task<IReadOnlyList<TimeEntryDto>?> ListForCardAsync(int cardId, int userId)
    {
        var card = await _db.Cards
            .Include(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null || card.List.Board.OwnerId != userId) return null;

        return await _db.TimeEntries
            .Where(t => t.CardId == cardId)
            .OrderByDescending(t => t.StartedAt)
            .Select(t => new TimeEntryDto(t.Id, t.CardId, t.UserId, t.StartedAt, t.EndedAt, t.DurationSeconds))
            .ToListAsync();
    }

    public async Task<bool> DeleteAsync(int entryId, int userId)
    {
        var entry = await _db.TimeEntries
            .Include(t => t.Card).ThenInclude(c => c.List).ThenInclude(l => l.Board)
            .FirstOrDefaultAsync(t => t.Id == entryId);
        if (entry is null) return false;
        if (entry.UserId != userId && entry.Card.List.Board.OwnerId != userId) return false;

        _db.TimeEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ActiveTimerDto?> GetActiveAsync(int userId)
    {
        var entry = await _db.TimeEntries
            .Where(t => t.UserId == userId && t.EndedAt == null)
            .OrderByDescending(t => t.StartedAt)
            .FirstOrDefaultAsync();
        return entry is null ? null : new ActiveTimerDto(entry.Id, entry.CardId, entry.StartedAt);
    }

    private static TimeEntryDto Map(TimeEntry t) =>
        new(t.Id, t.CardId, t.UserId, t.StartedAt, t.EndedAt, t.DurationSeconds);
}
