using Microsoft.EntityFrameworkCore;

namespace Sparkify.Features.Message;

public class Models : DbContext
{
    public Models(DbContextOptions<Models> options)
        : base(options)
    {
    }

    public DbSet<Message> Messages => Set<Message>();
}

public record Message
{
    public int Id { get; set; }
    public Guid Guid { get; set; }
    public string Value { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public record MessageDto
{
    public string Value { get; set; } = default!;
}
