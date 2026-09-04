using System.Threading.Tasks;

namespace Flarial.Runtime.Services;

static class TaskService
{
    internal delegate void TaskAction<TArgs>(in TArgs args);
    internal delegate TResult TaskFunc<TArgs, TResult>(in TArgs args);

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

    sealed class TaskState<TArgs, TResult>
    {
        readonly TArgs _args;
        readonly TaskFunc<TArgs, TResult> _function;

        internal TaskState(TaskFunc<TArgs, TResult> function, in TArgs args)
        {
            _args = args;
            _function = function;
        }

        internal TResult Invoke() => _function(_args);
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

        internal static Task<TResult> Run<TArgs, TResult>(TaskFunc<TArgs, TResult> function, in TArgs args)
        {
            TaskState<TArgs, TResult> state = new(function, args);

            static TResult Func(object? state)
            {
                return ((TaskState<TArgs, TResult>)state!).Invoke();
            }

            return Task.Factory.StartNew(Func, state, default, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }
    }
}