// <copyright file="IResilienceProvider.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.Core.Interfaces
{
    using Polly;

    /// <summary>
    /// Provides centralized resilience policies for the application.
    /// </summary>
    public interface IResilienceProvider
    {
        /// <summary>
        /// Gets a named resilience policy.
        /// </summary>
        /// <typeparam name="T">The return type of the policy.</typeparam>
        /// <param name="policyName">The name of the policy.</param>
        /// <returns>An async policy.</returns>
        IAsyncPolicy<T> GetPolicy<T>(string policyName);

        /// <summary>
        /// Gets a policy specific to a provider.
        /// </summary>
        /// <typeparam name="T">The return type of the policy.</typeparam>
        /// <param name="providerId">The unique identifier of the provider.</param>
        /// <returns>An async policy tailored for the provider.</returns>
        IAsyncPolicy<T> GetProviderPolicy<T>(string providerId);
    }
}
