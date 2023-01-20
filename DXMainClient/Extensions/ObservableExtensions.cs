using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DTAClient.Extensions;

public static class ObservableExtensions
{
    public static IObservable<T> ObserveOnCurrent<T>(this IObservable<T> source)
        => source.ObserveOn(CurrentThreadScheduler.Instance);
}