namespace StardewInteriorChanger.Core;

public sealed record VariantFingerprint(
    VariantId Id,
    InteriorTarget Target,
    ContentHash ContentHash)
{
    public static VariantFingerprint From(RegisteredInterior interior)
    {
        ArgumentNullException.ThrowIfNull(interior);
        return new VariantFingerprint(interior.Id, interior.Target, interior.ContentHash);
    }
}

public sealed record PeerRegistrySnapshot(
    string PeerId,
    ushort ProtocolMajor,
    ushort ProtocolMinor,
    IReadOnlyCollection<VariantFingerprint> Variants);

public enum PeerCompatibilityIssueCode
{
    DuplicatePeerId,
    DuplicatePeerVariant,
    ProtocolMajorMismatch,
    RequiredVariantMissingFromHost,
    MissingRequiredVariant,
    TargetMismatch,
    ContentHashMismatch
}

public sealed record PeerCompatibilityIssue(
    PeerCompatibilityIssueCode Code,
    string Message,
    string? PeerId = null,
    VariantId? VariantId = null);

public sealed record PeerCompatibilityResult(
    bool IsCompatible,
    ushort? NegotiatedProtocolMinor,
    IReadOnlyList<VariantId> AvailableToAll,
    IReadOnlyList<PeerCompatibilityIssue> Issues);

public static class PeerCompatibilityEvaluator
{
    public static PeerCompatibilityResult Evaluate(
        ushort hostProtocolMajor,
        ushort hostProtocolMinor,
        IEnumerable<VariantFingerprint> hostVariants,
        IEnumerable<VariantId> requiredVariants,
        IEnumerable<PeerRegistrySnapshot> peers)
    {
        ArgumentNullException.ThrowIfNull(hostVariants);
        ArgumentNullException.ThrowIfNull(requiredVariants);
        ArgumentNullException.ThrowIfNull(peers);

        Dictionary<VariantId, VariantFingerprint> host = hostVariants
            .GroupBy(variant => variant.Id)
            .ToDictionary(group => group.Key, group => group.First());

        VariantId[] required = requiredVariants
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();

        PeerRegistrySnapshot[] orderedPeers = peers
            .OrderBy(peer => peer.PeerId, StringComparer.Ordinal)
            .ToArray();

        var issues = new List<PeerCompatibilityIssue>();
        var available = host.Keys.ToHashSet();
        ushort negotiatedMinor = hostProtocolMinor;

        foreach (IGrouping<string, PeerRegistrySnapshot> duplicatePeer in orderedPeers
            .GroupBy(peer => peer.PeerId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            issues.Add(new PeerCompatibilityIssue(
                PeerCompatibilityIssueCode.DuplicatePeerId,
                "The handshake contains the same peer more than once.",
                duplicatePeer.Key));
        }

        foreach (VariantId requiredId in required)
        {
            if (!host.ContainsKey(requiredId))
            {
                issues.Add(new PeerCompatibilityIssue(
                    PeerCompatibilityIssueCode.RequiredVariantMissingFromHost,
                    "The host does not have a required active variant.",
                    VariantId: requiredId));
            }
        }

        foreach (PeerRegistrySnapshot peer in orderedPeers)
        {
            if (peer.ProtocolMajor != hostProtocolMajor)
            {
                issues.Add(new PeerCompatibilityIssue(
                    PeerCompatibilityIssueCode.ProtocolMajorMismatch,
                    $"Peer protocol major {peer.ProtocolMajor} does not match host major {hostProtocolMajor}.",
                    peer.PeerId));
                available.Clear();
                continue;
            }

            negotiatedMinor = Math.Min(negotiatedMinor, peer.ProtocolMinor);

            ILookup<VariantId, VariantFingerprint> peerGroups = peer.Variants.ToLookup(
                variant => variant.Id);

            foreach (IGrouping<VariantId, VariantFingerprint> duplicate in peerGroups.Where(
                group => group.Count() > 1))
            {
                issues.Add(new PeerCompatibilityIssue(
                    PeerCompatibilityIssueCode.DuplicatePeerVariant,
                    "The peer advertised a variant more than once.",
                    peer.PeerId,
                    duplicate.Key));
                available.Remove(duplicate.Key);
            }

            foreach ((VariantId id, VariantFingerprint hostVariant) in host)
            {
                VariantFingerprint[] candidates = peerGroups[id].ToArray();
                if (candidates.Length != 1)
                {
                    available.Remove(id);
                    continue;
                }

                VariantFingerprint candidate = candidates[0];
                if (candidate.Target != hostVariant.Target
                    || candidate.ContentHash != hostVariant.ContentHash)
                {
                    available.Remove(id);
                }
            }

            foreach (VariantId requiredId in required.Where(host.ContainsKey))
            {
                VariantFingerprint[] candidates = peerGroups[requiredId].ToArray();
                if (candidates.Length == 0)
                {
                    issues.Add(new PeerCompatibilityIssue(
                        PeerCompatibilityIssueCode.MissingRequiredVariant,
                        "The peer is missing a required active variant.",
                        peer.PeerId,
                        requiredId));
                    continue;
                }

                if (candidates.Length != 1)
                {
                    continue;
                }

                VariantFingerprint hostVariant = host[requiredId];
                VariantFingerprint candidate = candidates[0];
                if (candidate.Target != hostVariant.Target)
                {
                    issues.Add(new PeerCompatibilityIssue(
                        PeerCompatibilityIssueCode.TargetMismatch,
                        "The peer variant targets a different interior contract.",
                        peer.PeerId,
                        requiredId));
                }
                else if (candidate.ContentHash != hostVariant.ContentHash)
                {
                    issues.Add(new PeerCompatibilityIssue(
                        PeerCompatibilityIssueCode.ContentHashMismatch,
                        "The peer variant has different gameplay content.",
                        peer.PeerId,
                        requiredId));
                }
            }
        }

        PeerCompatibilityIssue[] orderedIssues = issues
            .OrderBy(issue => issue.PeerId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.VariantId?.Value, StringComparer.Ordinal)
            .ToArray();

        return new PeerCompatibilityResult(
            orderedIssues.Length == 0,
            orderedIssues.Any(issue => issue.Code == PeerCompatibilityIssueCode.ProtocolMajorMismatch)
                ? null
                : negotiatedMinor,
            available.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray(),
            orderedIssues);
    }
}
