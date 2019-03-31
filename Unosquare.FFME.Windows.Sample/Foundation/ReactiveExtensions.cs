namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    /// <summary>
    /// A very simple set of extensions to more easily handle UI state changes based on
    /// notification properties. The main idea is to bind to the PropertyChanged event
    /// for a publisher only one and add a set of callbacks with matching property names
    /// when the publisher raises the event.
    /// </summary>
    internal static class ReactiveExtensions
    {
        /// <summary>
        /// Contains a list of subscriptions Subscriptions[Publisher][PropertyName].List of subscriber-action pairs.
        /// </summary>
        private static readonly Dictionary<INotifyPropertyChanged, SubscriptionSet> Subscriptions
            = new Dictionary<INotifyPropertyChanged, SubscriptionSet>();

        private static readonly object SyncLock = new object();

        // The pinned actions (action that don't get removed if the reference is lost.
        private static readonly Dictionary<Action, bool> PinnedActions = new Dictionary<Action, bool>();

        /// <summary>
        /// Specifies a callback when properties change.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <param name="publisher">The publisher.</param>
        /// <param name="propertyNames">The property names.</param>
        internal static void WhenChanged(this Action callback, INotifyPropertyChanged publisher, params string[] propertyNames)
        {
            var bindPropertyChanged = false;

            lock (SyncLock)
            {
                // Create the subscription set for the publisher if it does not exist.
                if (Subscriptions.ContainsKey(publisher) == false)
                {
                    Subscriptions[publisher] = new SubscriptionSet();

                    // if it did not exist before, we need to bind to the
                    // PropertyChanged event of the publisher.
                    bindPropertyChanged = true;
                }

                // Save the Action reference so that the weak reference is not lost
                // TODO: The references will never be detected as dead if we pin them
                // but for the purposes of this sample app we don't need to remove the
                // dead references.
                PinnedActions[callback] = true;

                foreach (var propertyName in propertyNames)
                {
                    // Create the set of callback references for the publisher's property if it does not exist.
                    if (Subscriptions[publisher].ContainsKey(propertyName) == false)
                        Subscriptions[publisher][propertyName] = new CallbackReferenceSet();

                    // Add the callback for the publisher's property changed
                    Subscriptions[publisher][propertyName].Add(new CallbackReference(callback));
                }
            }

            // No need to bind to the PropertyChanged event if we are already bound to it.
            if (bindPropertyChanged == false)
                return;

            // Finally, bind to property changed
            publisher.PropertyChanged += (s, e) =>
            {
                var deadCallbacks = new CallbackReferenceSet();
                var aliveCallbacks = new CallbackReferenceSet();

                lock (SyncLock)
                {
                    // we don't need to perform any action if there are no subscriptions to
                    // this property name.
                    if (Subscriptions[publisher].ContainsKey(e.PropertyName) == false)
                        return;

                    // Get the list of alive subscriptions for this property name
                    aliveCallbacks.AddRange(Subscriptions[publisher][e.PropertyName]);
                }

                // Call the subscription's callbacks
                foreach (var aliveSubscription in aliveCallbacks)
                {
                    // Check if the subscription reference is alive.
                    if (aliveSubscription.IsAlive == false)
                    {
                        deadCallbacks.Add(aliveSubscription);
                        continue;
                    }

                    // if the subscription is alive, invoke the matching action
                    aliveSubscription.Target?.Invoke();
                }

                // Skip over if we don't have dead subscriptions
                if (deadCallbacks.Count == 0)
                    return;

                // Remove dead subscriptions
                lock (SyncLock)
                {
                    foreach (var deadSubscriber in deadCallbacks)
                        Subscriptions[publisher][e.PropertyName].Remove(deadSubscriber);
                }
            };
        }

        internal sealed class SubscriptionSet : Dictionary<string, CallbackReferenceSet> { }

        internal sealed class CallbackReferenceSet : List<CallbackReference>
        {
            public CallbackReferenceSet()
                : base(32)
            {
                // placeholder
            }
        }

        internal sealed class CallbackReference : WeakReference
        {
            public CallbackReference(Action action)
                : base(action, false)
            {
                // placeholder
            }

            public new Action Target => IsAlive ? base.Target as Action : null;
        }
    }
}
