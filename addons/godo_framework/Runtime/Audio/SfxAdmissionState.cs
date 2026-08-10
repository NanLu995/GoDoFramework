using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>非空间与空间 SFX 池共用的容量、优先级和资源键准入状态。</summary>
internal sealed class SfxAdmissionState<TVoice> where TVoice : class
{
    private readonly Dictionary<TVoice, ActivePlayback> _activeVoices;
    private readonly Dictionary<ulong, TVoice> _voicesByHandle;
    private readonly Dictionary<ulong, PendingPlayback> _pendingRequests;
    private readonly HashSet<ulong> _preemptedPendingRequests;
    private readonly Dictionary<ResourceKey, int> _reservedPerKey;
    private readonly int _maxVoices;
    private ulong _nextPlaybackId;
    private ulong _nextSequence;

    public int ActiveCount => _activeVoices.Count;
    public int PendingCount => _pendingRequests.Count;
    public long RejectedCount { get; private set; }
    public long PreemptedCount { get; private set; }

    public SfxAdmissionState(int maxVoices)
    {
        if (maxVoices <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxVoices));

        _maxVoices = maxVoices;
        _activeVoices = new Dictionary<TVoice, ActivePlayback>(
            maxVoices,
            ReferenceEqualityComparer.Instance);
        _voicesByHandle = new Dictionary<ulong, TVoice>(maxVoices);
        _pendingRequests = new Dictionary<ulong, PendingPlayback>(maxVoices);
        _preemptedPendingRequests = new HashSet<ulong>(maxVoices);
        _reservedPerKey = new Dictionary<ResourceKey, int>(maxVoices);
    }

    public AdmissionDecision Reserve(
        ResourceKey key,
        SfxPriority priority,
        int maxConcurrentPerKey,
        bool allowStealLowerPriority)
    {
        if (maxConcurrentPerKey > 0 && GetReserved(key) >= maxConcurrentPerKey)
        {
            RejectedCount++;
            return AdmissionDecision.Rejected(SfxPlaybackStatus.PerKeyLimitReached);
        }

        TVoice? preemptedVoice = null;
        ulong preemptedPendingId = 0;
        if (_activeVoices.Count + _pendingRequests.Count >= _maxVoices)
        {
            if (!allowStealLowerPriority ||
                !TryPreemptLowerPriority(
                    priority,
                    out preemptedVoice,
                    out preemptedPendingId))
            {
                RejectedCount++;
                return AdmissionDecision.Rejected(SfxPlaybackStatus.GlobalCapacityReached);
            }
        }

        ulong playbackId = NextPlaybackId();
        ulong sequence = ++_nextSequence;
        _pendingRequests.Add(
            playbackId,
            new PendingPlayback(key, priority, sequence));
        IncrementReserved(key);
        return AdmissionDecision.Admitted(
            playbackId,
            sequence,
            preemptedVoice,
            preemptedPendingId);
    }

    public void RecordRejection()
    {
        RejectedCount++;
    }

    public bool Activate(ulong playbackId, TVoice voice)
    {
        if (!_pendingRequests.Remove(playbackId, out PendingPlayback pending))
            return false;

        _activeVoices.Add(
            voice,
            new ActivePlayback(pending.Key, pending.Priority, pending.Sequence, playbackId));
        _voicesByHandle.Add(playbackId, voice);
        return true;
    }

    public bool TryGetVoice(ulong playbackId, out TVoice? voice) =>
        _voicesByHandle.TryGetValue(playbackId, out voice);

    public bool Release(TVoice voice)
    {
        if (!_activeVoices.Remove(voice, out ActivePlayback active))
            return false;

        _voicesByHandle.Remove(active.PlaybackId);
        DecrementReserved(active.Key);
        return true;
    }

    public bool ConsumePreempted(ulong playbackId) =>
        _preemptedPendingRequests.Remove(playbackId);

    public void AbandonPending(ulong playbackId)
    {
        if (_pendingRequests.Remove(playbackId, out PendingPlayback pending))
            DecrementReserved(pending.Key);
        else
            _preemptedPendingRequests.Remove(playbackId);
    }

    public void CancelPending()
    {
        foreach (PendingPlayback pending in _pendingRequests.Values)
            DecrementReserved(pending.Key);

        _pendingRequests.Clear();
        _preemptedPendingRequests.Clear();
    }

    public TVoice[] GetActiveSnapshot()
    {
        var voices = new TVoice[_activeVoices.Count];
        _activeVoices.Keys.CopyTo(voices, 0);
        return voices;
    }

    private bool TryPreemptLowerPriority(
        SfxPriority incomingPriority,
        out TVoice? preemptedVoice,
        out ulong preemptedPendingId)
    {
        bool found = false;
        SfxPriority selectedPriority = SfxPriority.Critical;
        ulong selectedSequence = ulong.MaxValue;
        TVoice? selectedVoice = null;
        ulong selectedPendingId = 0;

        foreach (KeyValuePair<TVoice, ActivePlayback> pair in _activeVoices)
        {
            ActivePlayback active = pair.Value;
            if (!IsBetterPreemptionCandidate(
                    active.Priority,
                    active.Sequence,
                    incomingPriority,
                    found,
                    selectedPriority,
                    selectedSequence))
                continue;

            found = true;
            selectedPriority = active.Priority;
            selectedSequence = active.Sequence;
            selectedVoice = pair.Key;
            selectedPendingId = 0;
        }

        foreach (KeyValuePair<ulong, PendingPlayback> pair in _pendingRequests)
        {
            PendingPlayback pending = pair.Value;
            if (!IsBetterPreemptionCandidate(
                    pending.Priority,
                    pending.Sequence,
                    incomingPriority,
                    found,
                    selectedPriority,
                    selectedSequence))
                continue;

            found = true;
            selectedPriority = pending.Priority;
            selectedSequence = pending.Sequence;
            selectedVoice = null;
            selectedPendingId = pair.Key;
        }

        if (!found)
        {
            preemptedVoice = null;
            preemptedPendingId = 0;
            return false;
        }

        if (selectedVoice != null)
        {
            if (!Release(selectedVoice))
            {
                preemptedVoice = null;
                preemptedPendingId = 0;
                return false;
            }

            preemptedVoice = selectedVoice;
            preemptedPendingId = 0;
        }
        else if (_pendingRequests.Remove(selectedPendingId, out PendingPlayback pending))
        {
            DecrementReserved(pending.Key);
            _preemptedPendingRequests.Add(selectedPendingId);
            preemptedVoice = null;
            preemptedPendingId = selectedPendingId;
        }
        else
        {
            preemptedVoice = null;
            preemptedPendingId = 0;
            return false;
        }

        PreemptedCount++;
        return true;
    }

    private static bool IsBetterPreemptionCandidate(
        SfxPriority candidatePriority,
        ulong candidateSequence,
        SfxPriority incomingPriority,
        bool found,
        SfxPriority selectedPriority,
        ulong selectedSequence)
    {
        if (candidatePriority >= incomingPriority)
            return false;
        if (!found || candidatePriority < selectedPriority)
            return true;
        return candidatePriority == selectedPriority && candidateSequence < selectedSequence;
    }

    private int GetReserved(ResourceKey key) =>
        _reservedPerKey.TryGetValue(key, out int count) ? count : 0;

    private void IncrementReserved(ResourceKey key) =>
        _reservedPerKey[key] = GetReserved(key) + 1;

    private void DecrementReserved(ResourceKey key)
    {
        if (!_reservedPerKey.TryGetValue(key, out int count))
            return;

        if (count <= 1)
            _reservedPerKey.Remove(key);
        else
            _reservedPerKey[key] = count - 1;
    }

    private ulong NextPlaybackId()
    {
        do
        {
            _nextPlaybackId++;
        }
        while (_nextPlaybackId == 0 ||
               _pendingRequests.ContainsKey(_nextPlaybackId) ||
               _preemptedPendingRequests.Contains(_nextPlaybackId) ||
               _voicesByHandle.ContainsKey(_nextPlaybackId));

        return _nextPlaybackId;
    }

    private readonly record struct ActivePlayback(
        ResourceKey Key,
        SfxPriority Priority,
        ulong Sequence,
        ulong PlaybackId);

    private readonly record struct PendingPlayback(
        ResourceKey Key,
        SfxPriority Priority,
        ulong Sequence);

    public readonly record struct AdmissionDecision(
        bool Accepted,
        SfxPlaybackStatus Status,
        ulong PlaybackId,
        ulong Sequence,
        TVoice? PreemptedVoice,
        ulong PreemptedPendingId)
    {
        public static AdmissionDecision Admitted(
            ulong playbackId,
            ulong sequence,
            TVoice? preemptedVoice,
            ulong preemptedPendingId) =>
            new(
                true,
                SfxPlaybackStatus.None,
                playbackId,
                sequence,
                preemptedVoice,
                preemptedPendingId);

        public static AdmissionDecision Rejected(SfxPlaybackStatus status) =>
            new(false, status, 0, 0, null, 0);
    }
}
