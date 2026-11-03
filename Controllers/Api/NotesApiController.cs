using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace FilmHub.Controllers.Api;

public sealed record NoteDocumentRequest(JsonElement Blocks, string? ClientId);

[ApiController]
[Route("api/notes")]
public sealed class NotesApiController : ControllerBase
{
    private const int MaxBytes = 200_000;
    private static readonly object Gate = new();
    private static string _blocksJson = "[]";
    private static int _revision;
    private static string? _clientId;

    [HttpGet]
    public IActionResult Get([FromQuery] int since = -1)
    {
        lock (Gate)
        {
            if (since == _revision) return NoContent();
            var json = $"{{\"revision\":{_revision},\"clientId\":{JsonSerializer.Serialize(_clientId)},\"blocks\":{_blocksJson}}}";
            return Content(json, "application/json");
        }
    }

    [HttpPut]
    public IActionResult Put(NoteDocumentRequest request)
    {
        if (request.Blocks.ValueKind != JsonValueKind.Array)
            return BadRequest(new { error = "blocks must be an array" });

        var raw = request.Blocks.GetRawText();
        if (raw.Length > MaxBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "document is too large" });

        lock (Gate)
        {
            _blocksJson = raw;
            _clientId = request.ClientId;
            _revision++;
            return Ok(new { revision = _revision });
        }
    }
}