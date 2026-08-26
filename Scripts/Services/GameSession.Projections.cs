using System;
using System.Collections.Generic;
using Voidling.Application.Creatures;

namespace VoidlingGame;

public partial class GameSession
{
    private VoidlingProfileProjectionService? _voidlingProfiles;

    public void ConfigureReadModels(VoidlingProfileProjectionService voidlingProfiles)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("Read-model dependencies must be configured before GameSession enters the scene tree.");

        _voidlingProfiles = voidlingProfiles ?? throw new ArgumentNullException(nameof(voidlingProfiles));
    }

    public VoidlingProfileProjection? CreateVoidlingProfileProjection(string creatureId)
    {
        if (_voidlingProfiles == null)
            throw new InvalidOperationException("VoidlingProfileProjectionService was not configured by Bootstrap.");

        return _voidlingProfiles.Create(State, creatureId);
    }

    public IReadOnlyList<VoidlingProfileProjection> CreateActiveVoidlingProfileProjections()
    {
        if (_voidlingProfiles == null)
            throw new InvalidOperationException("VoidlingProfileProjectionService was not configured by Bootstrap.");

        return _voidlingProfiles.CreateActive(State);
    }
}
