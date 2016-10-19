using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using WeakDelegates;

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
            collection.CollectionChanged += WeakDelegate.Combine(null, new NotifyCollectionChangedEventHandler(testInstance.Handle));
            GC.Collect();
            collection.Add(1);
            testInstance = new TestClass();
            GC.Collect();
            collection.Add(2);
            testInstance.Handle(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, 3));
            Console.ReadLine();
        }
    }
}
