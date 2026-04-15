using System.ComponentModel.DataAnnotations;

namespace Plandex.Api.DTOs;

public record BoardSummaryDto(int Id, string Name, DateTime CreatedAt);

public record BoardDetailDto(
    int Id,
    string Name,
    DateTime CreatedAt,
    List<ListDto> Lists,
    List<LabelDto> Labels);

public record ListDto(
    int Id,
    int BoardId,
    string Name,
    int Position,
    List<CardDto> Cards);

public record CardDto(
    int Id,
    int ListId,
    string Title,
    string? Description,
    int Position,
    DateTime? DueDate,
    List<LabelDto> Labels,
    int ChecklistTotal,
    int ChecklistDone,
    int TotalLoggedSeconds,
    DateTime? ActiveTimerStartedAt);

public record CardDetailDto(
    int Id,
    int ListId,
    string Title,
    string? Description,
    int Position,
    DateTime? DueDate,
    List<LabelDto> Labels,
    List<ChecklistDto> Checklists,
    List<TimeEntryDto> TimeEntries,
    int TotalLoggedSeconds,
    DateTime? ActiveTimerStartedAt);

public record LabelDto(int Id, int BoardId, string Name, string Color);

public record ChecklistDto(int Id, int CardId, string Title, List<ChecklistItemDto> Items);
public record ChecklistItemDto(int Id, int ChecklistId, string Text, bool IsDone, int Position);

public record TimeEntryDto(int Id, int CardId, int UserId, DateTime StartedAt, DateTime? EndedAt, int? DurationSeconds);

public record CreateBoardDto([Required, StringLength(200)] string Name);
public record UpdateBoardDto([Required, StringLength(200)] string Name);

public record CreateListDto([Required, StringLength(200)] string Name, int? Position);
public record UpdateListDto([StringLength(200)] string? Name, int? Position);

public record CreateCardDto(
    [Required, StringLength(500)] string Title,
    string? Description,
    DateTime? DueDate,
    int? Position);

public record UpdateCardDto(
    [StringLength(500)] string? Title,
    string? Description,
    DateTime? DueDate,
    bool ClearDueDate = false,
    int? ListId = null,
    int? Position = null);

public record CreateLabelDto(
    [Required, StringLength(100)] string Name,
    [Required, StringLength(20)] string Color);

public record CreateChecklistDto([Required, StringLength(200)] string Title);

public record CreateChecklistItemDto(
    [Required, StringLength(1000)] string Text,
    int? Position);

public record UpdateChecklistItemDto(
    [StringLength(1000)] string? Text,
    bool? IsDone,
    int? Position);
