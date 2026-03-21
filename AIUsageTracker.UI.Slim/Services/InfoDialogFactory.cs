// <copyright file="InfoDialogFactory.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.UI.Slim.Services;

public sealed class InfoDialogFactory : IInfoDialogFactory
{
    private readonly ILogger<InfoDialog> _logger;
    private readonly IAppPathProvider _pathProvider;

    public InfoDialogFactory(ILogger<InfoDialog> logger, IAppPathProvider pathProvider)
    {
        this._logger = logger;
        this._pathProvider = pathProvider;
    }

    public InfoDialog Create()
    {
        return new InfoDialog(this._logger, this._pathProvider);
    }
}
