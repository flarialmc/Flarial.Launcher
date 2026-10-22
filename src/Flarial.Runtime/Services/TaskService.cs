using System;
using System.Threading.Tasks;

namespace Flarial.Runtime.Services;

[Obsolete("Use 'Task.Run()' instead.", true)]
static class TaskService
{
    internal delegate T TaskFunc<T, TArgs>(in TArgs args);
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

    sealed class TaskState<T, TArgs>
    {
        readonly TArgs _args;
        readonly TaskFunc<T, TArgs> _function;

        internal TaskState(TaskFunc<T, TArgs> function, in TArgs args)
        {
            _args = args;
            _function = function;
        }

        internal T Invoke() => _function(_args);
    }

    extension(Task)
    {
        [Obsolete("Use 'Task.Run()' instead.", true)]
        internal static Task Run<TArgs>(TaskAction<TArgs> action, in TArgs args)
        {
            TaskState<TArgs> state = new(action, args);

            static void Action(object? state)
            {
                ((TaskState<TArgs>)state!).Invoke();
            }

            return Task.Factory.StartNew(Action, state, default, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        [Obsolete("Use 'Task.Run()' instead.", true)]
        internal static Task Run<T, TArgs>(TaskFunc<T, TArgs> function, in TArgs args)
        {
            TaskState<T, TArgs> state = new(function, args);

            static T Function(object? state)
            {
                return ((TaskState<T, TArgs>)state!).Invoke();
            }

            return Task.Factory.StartNew(Function, state, default, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }
    }
}