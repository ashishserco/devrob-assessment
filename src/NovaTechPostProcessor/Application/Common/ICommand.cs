using NovaTechPostProcessor.Domain.Common;

namespace NovaTechPostProcessor.Application.Common
{
    /// <summary>
    /// CQRS Command interface for commands that don't return data.
    /// Follows Command Query Responsibility Segregation pattern.
    /// </summary>
    public interface ICommand
    {
    }

    /// <summary>
    /// CQRS Command interface for commands that return data.
    /// Generic type parameter ensures type safety for command results.
    /// </summary>
    public interface ICommand<TResult> : ICommand
    {
    }

    /// <summary>
    /// Command handler interface following CQRS pattern.
    /// Enables dependency injection and testability.
    /// </summary>
    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Command handler interface for commands with results.
    /// </summary>
    public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
    {
        Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }
}