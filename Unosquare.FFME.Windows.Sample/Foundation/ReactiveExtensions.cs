namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Primitives;

    internal static class ReactiveExtensions
    {
        /// <summary>
        /// Contains a list of subscriptions Subscriptions[Publisher][PropertyName].List of subscriber-action pairs
        /// </summary>
        private static readonly Dictionary<INotifyPropertyChanged, SubscriptionSet> Subscriptions
            = new Dictionary<INotifyPropertyChanged, SubscriptionSet>();

        private static readonly ISyncLocker Locker = SyncLockerFactory.Create(useSlim: true);

        internal static void OnChange(this object subscriber, INotifyPropertyChanged publisher, string propertyName, Action callback)
        {
            var bindPropertyChanged = false;

            using (Locker.AcquireWriterLock())
            {
                if (Subscriptions.ContainsKey(publisher) == false)
                {
                    Subscriptions[publisher] = new SubscriptionSet();
                    bindPropertyChanged = true;
                }

                if (Subscriptions[publisher].ContainsKey(propertyName) == false)
                    Subscriptions[publisher][propertyName] = new List<SubscriberCallback>();

                Subscriptions[publisher][propertyName].Add(new SubscriberCallback(subscriber, callback));
            }

            if (bindPropertyChanged == false) return;

            // Finally, bind to proety changed
            publisher.PropertyChanged += (s, e) =>
            {
                if (Subscriptions[publisher].ContainsKey(e.PropertyName) == false)
                    return;

                var deadSubscriptions = new List<SubscriberCallback>();
                var aliveSubscriptions = new List<SubscriberCallback>();

                using (Locker.AcquireReaderLock())
                {
                    aliveSubscriptions.AddRange(Subscriptions[publisher][e.PropertyName]);
                }

                foreach (var aliveSubscription in aliveSubscriptions)
                {
                    if (aliveSubscription.Subscriber.IsAlive == false)
                    {
                        deadSubscriptions.Add(aliveSubscription);
                        continue;
                    }

                    aliveSubscription.Callback?.Invoke();
                }

                if (deadSubscriptions.Count == 0) return;

                using (Locker.AcquireWriterLock())
                {
                    foreach (var deadSubscriber in deadSubscriptions)
                        Subscriptions[publisher][e.PropertyName].Remove(deadSubscriber);
                }
            };
        }

        internal class SubscriptionSet : Dictionary<string, List<SubscriberCallback>> { }

        internal class SubscriberCallback
        {
            internal SubscriberCallback(object subscriber, Action callback)
            {
                Callback = callback;
                Subscriber = new WeakReference(subscriber, false);
            }

            public Action Callback { get; }

            public WeakReference Subscriber { get; }
        }
    }
}
