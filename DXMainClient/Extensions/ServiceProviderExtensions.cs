using System;
using Microsoft.Extensions.DependencyInjection;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Just a convenience and/or assurance function to "get a control" rather than using "get service" to get a control.
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <typeparam name="T">The type control to get</typeparam>
    /// <returns></returns>
    public static T GetControl<T>(this IServiceProvider serviceProvider) where T : XNAControl
        => serviceProvider.GetService<T>();
}