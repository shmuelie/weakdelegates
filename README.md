# Weak Delegates for .NET

While there are many systems out there for weak events/delegates in .NET they
generally suffer from one or more of the following flaws:

1. They only work with `System.EventHander` and `System.EventHander<T>`.
2. The leak memory that is only cleaned up sometimes if at all.
3. Their syntax/usage is extremely different from using strong delegates.

Not liking these issue I've created this experiment repository where I can try
create a system that has none of those issues. Currently issue #1 is completely
solved, this system with work with any delegate type. Issue #2 is mostly there,
though there is work to be done. Issue #3 is sadly still unsolved.

## API

```csharp
namespace WeakDelegates
{
    // Usage is the same as System.Delegate.Combine(Delegate, Delegate);
    public static T Combine<T>(T a, T b) where T : class;
    // Usage is the same as System.Delegate.Remove(Delegate, Delegate);
    public static T Remove<T>(T source, T value) where T : class;
    // Allows removing weak delegates when you don't have access to the delegate field directly.
    public static void Remove<T>(object eventContainer, string eventName, T value) where T : class
}
```

Documentation for the same named static methods on `System.Delegate` should be
the same. Only real difference is that I use generics and enforce that it must
be a delegate type at run-time instead of forcing you to do lots of casting.

The `Remove<T>(object eventContainer, string eventName, T value) where T :
class` method can be used to unsubscribe weak delegates from events.

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
            // To subscribe
            collection.CollectionChanged += WeakDelegates.WeakDelegate.Combine<NotifyCollectionChangedEventHandler>(null, testInstance.Handle);
            // to unsubscribe
            WeakDelegates.WeakDelegate.Remove<NotifyCollectionChangedEventHandler>(collection, nameof(collection.CollectionChanged), testInstance.Handle);
        }
    }
}

```

## Why?

See [The Problem With
Delegates](https://web.archive.org/web/20150327023026/http://diditwith.net/PermaLink,guid,fcf59145-3973-468a-ae66-aaa8df9161c7.aspx)
by [Dustin Campbell](https://twitter.com/dcampbell).

## Issues

1. Runtime code generation means that this will not work with .NET Native. This
   can be solved in theory by having the dynamic methods created at compile
   time.
2. Slower than strong delegates. Not much can be done here without writing raw
   IL instead of C#.
