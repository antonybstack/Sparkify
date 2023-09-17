using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Common.Configuration;

public static class OptionUtil
{
    public static T1 AddConfigAndValidate<T1, T2>(this IHostApplicationBuilder builder)
        where T1 : class where T2 : class, IValidateOptions<T1>
    {
        builder.Services.AddOptions<T1>()
            .BindConfiguration(typeof(T1).Name)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<T1>, T2>();
        return builder.Configuration.GetSection(typeof(T1).Name).Get(typeof(T1)) as T1 ??
               throw new ArgumentNullException(nameof(builder));
    }
}
