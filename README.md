# Weak Delegates for .NET

While there are many systems out there for weak events/delegates in .NET they generally suffer from one or more of the following flaws:

1. They only work with `System.EventHander` and `System.EventHander<T>`.
2. The leak memory that is only cleaned up sometimes if at all.
3. Their syntax/usage is extremely different from using strong delegates.

Not liking these issue I've created this experiment repository where I can try create a system that has none of those issues. Currently issue #1 is completely solved, this system with work with any delegate type. Issue #2 is mostly there, though there is work to be done. Issue #3 is sadly still unsolved, though I have implemented some helper methods to improve the situation.

## API

```csharp
namespace WeakDelegates
{
    // Usage is the same as System.Delegate.Combine(Delegate, Delegate);
    public static T Combine<T>(T a, T b) where T : class;
    // Usage is the same as System.Delegate.Remove(Delegate, Delegate);
    public static T Remove<T>(T source, T value) where T : class;
    // This method is psuedo code.
    // In reality it's a bunch of generated methods for common
    // delegate types of this form.
    public static T Weak<T>(T @delegate) where T : delegate;
}
```

Documentation for the same named static methods on `System.Delegate` should be the same. Only real difference is that I use generics and enforce that it must be a delegate type at run-time instead of forcing you to do lots of casting.

The `T Weak<T>(T @delegate)` methods simply call `T Combine<T>(T a, T b)` with `a` set to `null` and `b` set to `@delegate`. The main advantage of using them is that you don't have to provide type arguments.

### Example

```csharp
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using static System.Collections.Specialized.WeakDelegateHelpers;

namespace WeakTest
{
    class Program
    {
        private class TestClass
        {
            private DateTime created = DateTime.UtcNow;

            public void Handle(object sender, NotifyCollectionChangedEventArgs e)
            {
                Console.WriteLine(e.NewItems[0]);
            }
        }

        public static void Main()
        {
            ObservableCollection<int> collection = new ObservableCollection<int>();
            TestClass testInstance = new TestClass();
            // Using one of the helper methods
            collection.CollectionChanged += Weak(testInstance.Handle);
            // Which is the same as...
            collection.CollectionChanged += WeakDelegates.WeakDelegate.Combine<NotifyCollectionChangedEventHandler>(null, testInstance.Handle);
        }
    }
}

```

## Why?

See [The Problem With Delegates](https://web.archive.org/web/20150327023026/http://diditwith.net/PermaLink,guid,fcf59145-3973-468a-ae66-aaa8df9161c7.aspx) by [Dustin Campbell](https://twitter.com/dcampbell).

## Issues

1. Runtime code generation means that this will not work with .NET Native.
   a. This can be solved in theory by having the dynamic methods created at compile time.
2. Slower than strong delegates. Not much can be done here without writing raw IL instead of C#.