using Microsoft.AspNetCore.Mvc;
using PizzaDeli.Services;

namespace PizzaDeli.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ContactChatController : ControllerBase
{
    private readonly ContactRequestService _contactService;
    public ContactChatController(ContactRequestService contactService)
    {
         _contactService = contactService;
    }

    [HttpGet("{ticketId}")]
    public async Task<IActionResult> GetMessages(int ticketId)
    {
        var msgs = await _contactService.GetChatMessagesAsync(ticketId);
        return Ok(msgs.Select(m => new { sender = m.Sender, content = m.Content, time = m.CreatedAt.ToString("HH:mm") }));
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromForm] int ticketId, [FromForm] string sender, [FromForm] string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return BadRequest(new { success = false, message = "Empty content" });
        var msg = await _contactService.SendChatMessageAsync(ticketId, sender, content);
        if (msg == null) return NotFound(new { success = false });

        return Ok(new { success = true, sender = msg.Sender, content = msg.Content, time = msg.CreatedAt.ToString("HH:mm") });
    }
}
