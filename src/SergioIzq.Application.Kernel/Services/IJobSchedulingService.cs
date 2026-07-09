using System.Linq.Expressions;

namespace SergioIzq.Application.Kernel.Services;

public interface IJobSchedulingService
{
    string Enqueue(Expression<Func<Task>> methodCall);

    string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay);

    string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt);

    void AddOrUpdateRecurring(string recurringJobId, Expression<Func<Task>> methodCall, string cronExpression);

    void RemoveRecurringIfExists(string recurringJobId);

    bool Delete(string jobId);

    bool Requeue(string jobId);
}
