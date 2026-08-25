using System.Threading.Tasks;

namespace Flarial.Runtime.Services;

static class TaskService
{
    internal delegate void TaskAction<TArgs>(in TArgs args);

    sealed class TaskState<TArgs>
    {
        readonly TArgs _args;
        readonly TaskAction<TArgs> _action;

        internal TaskState(TaskAction<TArgs> action, in TArgs args)
        {
            _args = args;
            _action = action;
        }

        internal void Invoke() => _action(_args);
    }

    extension(Task)
    {
        internal static Task Run<TArgs>(TaskAction<TArgs> action, in TArgs args)
        {
            TaskState<TArgs> state = new(action, args);

            static void Action(object? state)
            {
                ((TaskState<TArgs>)state!).Invoke();
            }

            return Task.Factory.StartNew(Action, state, default, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }
    }
}