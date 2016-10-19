# Weak Delegates for .NET

While there are many systems out there for weak events/delegates in .NET they generally suffer from one more flaws:

1. They only work with `System.EventHander` and `System.EventHander<T>`.
2. They leave bing memory that may never be cleaned up.
3. They're syntax/usage is extremely different from using strong delegates.

Not liking these issue I've created this expirement repository where I can try create a system that has none of those issues. Currently issue #1 is completely solved, this system with work with any delegate type. Issue #2 is mostly there, though there is work to be done. Issue #3 is sadly still unsolved, though I have some thoughts on what I can do.

## API

```csharp
namespace WeakDelegates
{
    // Usage is the same as System.Delegate.Combine(Delegate, Delegate);
    public static T Combine<T>(T a, T b) where T : class;
    // Usage is the same as System.Delegate.Remove(Delegate, Delegate);
    public static T Remove<T>(T source, T value) where T : class;
}
```

Documentation for the same named static methods on `System.Delegate` should be the same. Only real difference is that I use generics and inforce that it must be a delegate type at run-time instead of forcing you to do lots of casting.