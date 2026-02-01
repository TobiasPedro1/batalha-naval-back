namespace BatalhaNaval.Application.DTOs;

public record RankingEntryDto(Guid UserId, string Username, int Points, int Wins, string Rank);