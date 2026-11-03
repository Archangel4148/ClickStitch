namespace ClickStitch.Input;

internal readonly struct HotkeyBindingStatus
{
    internal HotkeyBindingStatus(HotkeyRole role, bool registered, int? win32Error)
    {
        Role = role;
        Registered = registered;
        Win32Error = win32Error;
    }

    internal HotkeyRole Role { get; }

    internal bool Registered { get; }

    /// <summary>Set when registration failed. 1409 means another program already owns the chord.</summary>
    internal int? Win32Error { get; }
}

/// <summary>
/// Registers each role, remembers which ids succeeded, and turns a hotkey id into one target call.
/// Emergency stop calls only <see cref="IHotkeyTarget.EmergencyStop"/>. It does not call start, pause, or ordinary stop.
/// A chord Windows rejects is reported and the other roles are still registered.
/// </summary>
internal sealed class HotkeyBook
{
    internal const int AlreadyRegistered = 1409;

    private readonly IHotkeyRegistry _registry;
    private readonly Dictionary<int, HotkeyRole> _registered = [];
    private readonly Dictionary<int, HotkeyChord> _chords = [];

    internal HotkeyBook(IHotkeyRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    internal IReadOnlyDictionary<int, HotkeyRole> Registered => _registered;

    internal IReadOnlyList<HotkeyBindingStatus> Register(HotkeyBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        HotkeyRole[] roles = [HotkeyRole.Start, HotkeyRole.PauseResume, HotkeyRole.Stop, HotkeyRole.EmergencyStop];
        var statuses = new HotkeyBindingStatus[roles.Length];
        for (int index = 0; index < roles.Length; index++)
        {
            HotkeyRole role = roles[index];
            HotkeyChord chord = bindings.ChordFor(role);
            int id = (int)role;
            bool registered = _registry.Register(id, chord.NativeModifiers, chord.VirtualKey, out int win32Error);
            if (registered)
            {
                _registered[id] = role;
                _chords[id] = chord;
                statuses[index] = new HotkeyBindingStatus(role, true, null);
            }
            else
            {
                statuses[index] = new HotkeyBindingStatus(role, false, win32Error);
            }
        }

        return statuses;
    }

    internal void Unregister()
    {
        int[] ids = [.. _registered.Keys];
        foreach (int id in ids)
        {
            if (_registry.Unregister(id, out _))
            {
                _registered.Remove(id);
                _chords.Remove(id);
            }
        }
    }

    /// <summary>
    /// Drops one role and registers a new chord. Call this on the thread that owns the hotkeys.
    /// If the new registration fails, that role stays unregistered.
    /// If the old registration cannot be removed, the new chord is not registered.
    /// </summary>
    internal HotkeyBindingStatus Replace(HotkeyRole role, HotkeyChord chord)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        chord = HotkeyBindings.Validate(chord);
        int id = (int)role;
        foreach (KeyValuePair<int, HotkeyChord> existing in _chords)
        {
            if (existing.Key != id && existing.Value == chord)
            {
                throw new ArgumentException("Two hotkeys use the same chord.");
            }
        }

        if (_registered.ContainsKey(id) && !_registry.Unregister(id, out int removeError))
        {
            return new HotkeyBindingStatus(role, true, removeError);
        }

        _registered.Remove(id);
        _chords.Remove(id);
        bool registered = _registry.Register(id, chord.NativeModifiers, chord.VirtualKey, out int win32Error);
        if (!registered)
        {
            return new HotkeyBindingStatus(role, false, win32Error);
        }

        _registered[id] = role;
        _chords[id] = chord;
        return new HotkeyBindingStatus(role, true, null);
    }

    internal void Dispatch(int id, IHotkeyTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!_registered.TryGetValue(id, out HotkeyRole role))
        {
            return;
        }

        switch (role)
        {
            case HotkeyRole.Start:
                target.Start();
                break;
            case HotkeyRole.PauseResume:
                target.PauseOrResume();
                break;
            case HotkeyRole.Stop:
                target.Stop();
                break;
            case HotkeyRole.EmergencyStop:
                target.EmergencyStop();
                break;
        }
    }
}
