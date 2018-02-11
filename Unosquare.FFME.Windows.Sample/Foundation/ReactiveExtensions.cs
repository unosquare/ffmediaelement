namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    internal static class ReactiveExtensions
    {
        public static SubscribeTarget Watch(this ViewModelBase subsriber, INotifyPropertyChanged publisher, string propertyName)
        {
            return new SubscribeTarget(subsriber, publisher, propertyName);
        }

        public static ReactTarget OnChange(this SubscribeTarget subscriberTarget, Action<object, string> callback)
        {
            return new ReactTarget(subscriberTarget, callback);
        }

        public static NotifyTarget Notify(this ReactTarget reaction, params string[] propertyNames)
        {
            var subscriber = reaction.Subscription.Subscriber;
            var publisher = reaction.Subscription.Publisher;
            var publisherProperty = reaction.Subscription.PropertyName;
            var registrations = subscriber.Reactions;

            if (registrations.ContainsKey(publisher) == false)
            {
                registrations[publisher] = new Dictionary<string, List<NotifyTarget>>();

                // Bind to the property changed event
                publisher.PropertyChanged += (s, e) =>
                {
                    if (registrations[publisher].ContainsKey(e.PropertyName) == false)
                        return;

                    var notifyTargets = registrations[publisher][e.PropertyName];
                    var notifyPropertyNames = new Dictionary<string, bool>();
                    foreach (var notifyTarget in notifyTargets)
                    {
                        notifyTarget.Reaction.Callback?.Invoke(s, e.PropertyName);
                        foreach (var notifyPropertyName in notifyTarget.PropertyNames)
                            notifyPropertyNames[notifyPropertyName] = true;
                    }

                    foreach (var notifyPropertyName in notifyPropertyNames.Keys)
                        subscriber.NotifyPropertyChanged(notifyPropertyName);
                };
            }

            if (registrations[publisher].ContainsKey(publisherProperty) == false)
                registrations[publisher][publisherProperty] = new List<NotifyTarget>();

            var notification = new NotifyTarget(reaction, propertyNames ?? new string[] { });
            registrations[publisher][publisherProperty].Add(notification);

            return notification;
        }

        public sealed class SubscribeTarget
        {
            public SubscribeTarget(ViewModelBase subscriber, INotifyPropertyChanged publisher, string propertyName)
            {
                Subscriber = subscriber;
                Publisher = publisher;
                PropertyName = propertyName;
            }

            public ViewModelBase Subscriber { get; }
            public INotifyPropertyChanged Publisher { get; }
            public string PropertyName { get; }
        }

        public sealed class ReactTarget
        {
            public ReactTarget(SubscribeTarget subscription, Action<object, string> callback)
            {
                Subscription = subscription;
                Callback = callback;
            }

            public SubscribeTarget Subscription { get; }
            public Action<object, string> Callback { get; }
        }

        public sealed class NotifyTarget
        {
            public NotifyTarget(ReactTarget reaction, string[] propertyNames)
            {
                Reaction = reaction;
                PropertyNames = propertyNames;
            }

            public ReactTarget Reaction { get; }
            public string[] PropertyNames { get; }
        }
    }
}
