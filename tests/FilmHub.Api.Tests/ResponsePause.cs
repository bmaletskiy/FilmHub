using Microsoft.AspNetCore.Mvc.Filters;

namespace FilmHub.Api.Tests;

// Deterministically reproduces a GET whose old response is stored after a write commits.
public sealed class ResponsePause : IAsyncResultFilter
{
    private int armed;
    public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Arm() => Interlocked.Exchange(ref armed, 1);

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        await next();
        if (context.HttpContext.Request.Path == "/api/movies" && Interlocked.Exchange(ref armed, 0) == 1)
        {
            Ready.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }
}
