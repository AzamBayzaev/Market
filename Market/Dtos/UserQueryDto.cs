namespace Market.Dtos;

public class UserQueryDto
{
    public int PageNumber { get; set; } = 1;
    public string? SortBy { get; set; } = "name";
}