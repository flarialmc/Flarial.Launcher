using System;

namespace Flarial.Runtime.Core;

partial class FlarialClient
{
    public abstract partial class Private<T> : Private where T :  Private<T>, new()
    {
        public static readonly T _ = new();

        private protected  Private()
        {
            if (_ is null) return;
            throw new InvalidOperationException();
        }
    }

    public abstract partial class Public<T> : Public where T : Public<T>, new()
    {
        public static readonly T _ = new();

        private protected Public()
        {
            if (_ is null) return;
            throw new InvalidOperationException();
        }
    }
}