using System;

namespace Flarial.Runtime.Core;

partial class FlarialClient
{
    public abstract partial class Unstable<T> : Unstable where T : Unstable<T>, new()
    {
        public static readonly T _ = new();

        private protected Unstable()
        {
            if (_ is null) return;
            throw new InvalidOperationException();
        }
    }

    public abstract partial class Stable<T> : FlarialClient where T : Stable<T>, new()
    {
        public static readonly T _ = new();

        private protected Stable()
        {
            if (_ is null) return;
            throw new InvalidOperationException();
        }
    }
}