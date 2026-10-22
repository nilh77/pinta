using Microsoft.Extensions.DependencyInjection;

namespace Pinta.SharedKernel.Events;

/// <summary>
/// Something a module tells the others (CLAUDE.md rule 5). Defined in the publisher's Contracts; handled in-process by
/// any module. Slow or failure-prone work (emails, Stripe) is queued in Hangfire from the handler.
/// </summary>
public interface IIntegrationEvent;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

public interface IEventPublisher
{
    /// <summary>Runs every handler in the caller's scope, after the caller has saved its own changes.</summary>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}

internal sealed class InProcessEventPublisher(IServiceProvider services) : IEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        foreach (var handler in services.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}

public static class IntegrationEventsExtensions
{
    public static IServiceCollection AddIntegrationEvents(this IServiceCollection services)
    {
        services.AddScoped<IEventPublisher, InProcessEventPublisher>();
        return services;
    }

    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent> =>
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
}
