using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Sparkify.Features.Message;

public static class Handlers
{
    public static void MapMessagesApi(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllMessages);
        group.MapGet("/{id}", GetMessage);
        group.MapPost("/", CreateMessage);
        group.MapDelete("/{id}", DeleteMessage);
    }

    private static async Task<Ok<Message[]>> GetAllMessages(Models db)
    {
        return TypedResults.Ok(await db.Messages.ToArrayAsync());
    }

    private static async Task<Results<Ok<Message>, NotFound>> GetMessage(int id, Models db)
    {
        return await db.Messages.FindAsync(id) is Message message
            ? TypedResults.Ok(message)
            : TypedResults.NotFound();
    }

    private static async Task<Created<Message>> CreateMessage(MessageDto messageDto, Models db)
    {
        var message = new Message
        {
            Guid = Guid.NewGuid(),
            Value = messageDto.Value,
            CreatedAt = DateTime.UtcNow
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync();
        return TypedResults.Created($"{message.Id}", message);
    }

    private static async Task<Results<Ok<Message>, NotFound>> DeleteMessage(int id, Models db)
    {
        if (await db.Messages.FindAsync(id) is not Message message) return TypedResults.NotFound();

        db.Messages.Remove(message);
        await db.SaveChangesAsync();
        return TypedResults.Ok(message);
    }
}